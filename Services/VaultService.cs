using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RhodiumVault.Models;

namespace RhodiumVault.Services;

/// <summary>
/// Holds the unlocked vault session in memory and reads/writes the encrypted vault file.
///
/// Format v2 (current):
///   header = [4 magic "RVLT"][1 version=2][1 kdf id=1 (Argon2id)][4 memory KiB][4 iterations][4 parallelism][16 salt]
///   file   = header || nonce || ciphertext || tag     (the header is authenticated as AES-GCM associated data)
/// Format v1 (legacy, still readable, upgraded to v2 on first unlock):
///   [4 magic][1 version=1][16 salt][nonce || ciphertext || tag], fixed Argon2id 19 MiB / 2 / 1, no associated data.
/// </summary>
public class VaultService
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RVLT");
    private const byte CurrentVersion = 2;
    private const byte KdfArgon2id = 1;
    private const int HeaderV2Size = 4 + 1 + 1 + 4 + 4 + 4 + CryptoService.SaltSize;
    private const int HeaderV1Size = 4 + 1 + CryptoService.SaltSize;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly string _path;
    private string BackupPath => _path + ".bak";
    private string TempPath => _path + ".tmp";

    private byte[]? _salt;
    private byte[]? _key;
    private KdfParams _kdf = KdfParams.Current;

    public VaultService(string? path = null)
    {
        _path = path ?? DefaultPath;
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RhodiumVault", "vault.dat");

    public bool IsUnlocked => _key != null;

    /// <summary>Full path of the vault file (already encrypted, so a plain file copy is a safe backup).</summary>
    public string FilePath => _path;

    /// <summary>True if the last Unlock had to fall back to vault.dat.bak because vault.dat was damaged.</summary>
    public bool RecoveredFromBackup { get; private set; }

    public static bool VaultExists() => File.Exists(DefaultPath);
    public bool Exists() => File.Exists(_path) || File.Exists(BackupPath);

    /// <summary>Creates a brand-new, empty vault protected by the given master password.</summary>
    public void CreateNew(string masterPassword)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var salt = CryptoService.GenerateSalt();
        var kdf = KdfParams.Current;
        var key = CryptoService.DeriveKey(masterPassword, salt, kdf);
        WriteFile(new List<VaultEntry>(), salt, key, kdf);
        Adopt(salt, key, kdf);
    }

    /// <summary>
    /// Attempts to unlock the vault. Returns the entries on success, or null if the master password is wrong.
    /// Throws InvalidDataException / IOException if neither vault.dat nor vault.dat.bak can be read.
    /// </summary>
    public List<VaultEntry>? Unlock(string masterPassword)
    {
        RecoveredFromBackup = false;
        Exception? mainFailure = null;

        if (File.Exists(_path))
        {
            try
            {
                return TryUnlockFile(_path, masterPassword);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException or UnauthorizedAccessException)
            {
                mainFailure = ex; // damaged or locked - fall through to the backup
            }
        }

        if (File.Exists(BackupPath))
        {
            var fromBackup = TryUnlockFile(BackupPath, masterPassword);
            if (fromBackup != null)
            {
                RecoveredFromBackup = true;
                try { WriteFile(fromBackup, _salt!, _key!, _kdf); } catch (IOException) { /* keep running from the backup */ }
            }
            return fromBackup;
        }

        throw mainFailure ?? new FileNotFoundException("No vault file found.", _path);
    }

    private List<VaultEntry>? TryUnlockFile(string file, string masterPassword)
    {
        var bytes = File.ReadAllBytes(file);
        if (bytes.Length < HeaderV1Size || !bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Not a Rhodium Vault file.");

        byte version = bytes[Magic.Length];
        KdfParams kdf;
        byte[] salt = new byte[CryptoService.SaltSize];
        int headerSize;
        byte[]? aad;

        if (version == 1)
        {
            kdf = KdfParams.Legacy;
            headerSize = HeaderV1Size;
            aad = null;
            Buffer.BlockCopy(bytes, Magic.Length + 1, salt, 0, salt.Length);
        }
        else if (version == 2)
        {
            if (bytes.Length < HeaderV2Size) throw new InvalidDataException("Vault header is truncated.");
            if (bytes[5] != KdfArgon2id) throw new InvalidDataException("Unsupported key derivation.");
            kdf = new KdfParams(
                (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(6, 4)),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(10, 4)),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(14, 4)));
            if (!kdf.IsSane) throw new InvalidDataException("Vault header contains invalid key derivation parameters.");
            headerSize = HeaderV2Size;
            aad = bytes.AsSpan(0, HeaderV2Size).ToArray();
            Buffer.BlockCopy(bytes, 18, salt, 0, salt.Length);
        }
        else
        {
            throw new InvalidDataException($"Vault format version {version} is newer than this app understands.");
        }

        var blob = new byte[bytes.Length - headerSize];
        Buffer.BlockCopy(bytes, headerSize, blob, 0, blob.Length);

        var key = CryptoService.DeriveKey(masterPassword, salt, kdf);

        byte[] plaintext;
        try
        {
            plaintext = CryptoService.Decrypt(blob, key, aad);
        }
        catch (CryptographicException)
        {
            Array.Clear(key, 0, key.Length);
            return null; // wrong master password (or a tampered file - indistinguishable by design)
        }

        List<VaultEntry> entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<VaultEntry>>(plaintext, JsonOpts) ?? new List<VaultEntry>();
        }
        finally
        {
            Array.Clear(plaintext, 0, plaintext.Length);
        }

        Adopt(salt, key, kdf);

        // Upgrade old vaults / weaker parameters to the current format in place. Never let a failed upgrade block unlocking.
        if (version != CurrentVersion || kdf != KdfParams.Current)
        {
            try
            {
                var newSalt = CryptoService.GenerateSalt();
                var newKey = CryptoService.DeriveKey(masterPassword, newSalt, KdfParams.Current);
                WriteFile(entries, newSalt, newKey, KdfParams.Current);
                Adopt(newSalt, newKey, KdfParams.Current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return entries;
    }

    /// <summary>Re-encrypts and atomically writes the current entries to disk, using a fresh random nonce.</summary>
    public void Save(List<VaultEntry> entries)
    {
        if (_key == null || _salt == null)
            throw new InvalidOperationException("Vault is not unlocked.");

        WriteFile(entries, _salt, _key, _kdf);
    }

    /// <summary>Re-encrypts the vault under a brand-new master password. In-memory state only changes after the write succeeded.</summary>
    public void ChangeMasterPassword(List<VaultEntry> entries, string newMasterPassword)
    {
        var newSalt = CryptoService.GenerateSalt();
        var newKdf = KdfParams.Current;
        var newKey = CryptoService.DeriveKey(newMasterPassword, newSalt, newKdf);
        WriteFile(entries, newSalt, newKey, newKdf);
        Adopt(newSalt, newKey, newKdf);
    }

    private void Adopt(byte[] salt, byte[] key, KdfParams kdf)
    {
        if (_key != null && !ReferenceEquals(_key, key)) Array.Clear(_key, 0, _key.Length);
        _salt = salt;
        _key = key;
        _kdf = kdf;
    }

    private void WriteFile(List<VaultEntry> entries, byte[] salt, byte[] key, KdfParams kdf)
    {
        var header = new byte[HeaderV2Size];
        Magic.CopyTo(header, 0);
        header[4] = CurrentVersion;
        header[5] = KdfArgon2id;
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(6, 4), (uint)kdf.MemoryKiB);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(10, 4), (uint)kdf.Iterations);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(14, 4), (uint)kdf.Parallelism);
        salt.CopyTo(header, 18);

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(entries, JsonOpts);
        byte[] blob;
        try
        {
            blob = CryptoService.Encrypt(plaintext, key, header);
        }
        finally
        {
            Array.Clear(plaintext, 0, plaintext.Length);
        }

        // Write to a temp file, force it to disk, then swap it in. The previous version is kept as vault.dat.bak,
        // so a crash or full disk can never leave the only copy truncated.
        using (var fs = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(header, 0, header.Length);
            fs.Write(blob, 0, blob.Length);
            fs.Flush(true);
        }

        if (File.Exists(_path))
            File.Replace(TempPath, _path, BackupPath, ignoreMetadataErrors: true);
        else
            File.Move(TempPath, _path);
    }

    /// <summary>Clears the in-memory key on lock/exit. Best-effort only - managed memory can't be wiped with certainty.</summary>
    public void Lock()
    {
        if (_key != null) Array.Clear(_key, 0, _key.Length);
        if (_salt != null) Array.Clear(_salt, 0, _salt.Length);
        _key = null;
        _salt = null;
    }
}
