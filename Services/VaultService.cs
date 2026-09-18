using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RhodiumVault.Models;

namespace RhodiumVault.Services;

/// <summary>
/// Holds the unlocked vault session in memory and reads/writes the encrypted vault.dat file.
/// File layout: [4-byte magic "RVLT"][1-byte version][16-byte salt][nonce||ciphertext||tag from CryptoService].
/// </summary>
public class VaultService
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RVLT");
    private const byte FormatVersion = 1;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private static string VaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RhodiumVault");

    private static string VaultPath => Path.Combine(VaultDirectory, "vault.dat");

    private byte[]? _salt;
    private byte[]? _key;

    public bool IsUnlocked => _key != null;

    public static bool VaultExists() => File.Exists(VaultPath);

    /// <summary>Creates a brand-new, empty vault protected by the given master password.</summary>
    public void CreateNew(string masterPassword)
    {
        Directory.CreateDirectory(VaultDirectory);
        _salt = CryptoService.GenerateSalt();
        _key = CryptoService.DeriveKey(masterPassword, _salt);
        Save(new List<VaultEntry>());
    }

    /// <summary>Attempts to unlock the vault. Returns the entries on success, or null if the master password is wrong.</summary>
    public List<VaultEntry>? Unlock(string masterPassword)
    {
        var fileBytes = File.ReadAllBytes(VaultPath);
        if (fileBytes.Length < Magic.Length + 1 + CryptoService.SaltSize)
            throw new InvalidDataException("Vault file is corrupt or too short.");

        for (int i = 0; i < Magic.Length; i++)
        {
            if (fileBytes[i] != Magic[i])
                throw new InvalidDataException("Not a Rhodium Vault file.");
        }

        var salt = new byte[CryptoService.SaltSize];
        Buffer.BlockCopy(fileBytes, Magic.Length + 1, salt, 0, CryptoService.SaltSize);

        var blobStart = Magic.Length + 1 + CryptoService.SaltSize;
        var blob = new byte[fileBytes.Length - blobStart];
        Buffer.BlockCopy(fileBytes, blobStart, blob, 0, blob.Length);

        var key = CryptoService.DeriveKey(masterPassword, salt);

        byte[] plaintext;
        try
        {
            plaintext = CryptoService.Decrypt(blob, key);
        }
        catch (CryptographicException)
        {
            return null; // wrong master password
        }

        _salt = salt;
        _key = key;
        return JsonSerializer.Deserialize<List<VaultEntry>>(plaintext, JsonOpts) ?? new List<VaultEntry>();
    }

    /// <summary>Re-encrypts and writes the current entries to disk, using a fresh random nonce.</summary>
    public void Save(List<VaultEntry> entries)
    {
        if (_key == null || _salt == null)
            throw new InvalidOperationException("Vault is not unlocked.");

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(entries, JsonOpts);
        var blob = CryptoService.Encrypt(plaintext, _key);

        using var fs = new FileStream(VaultPath, FileMode.Create, FileAccess.Write);
        fs.Write(Magic, 0, Magic.Length);
        fs.WriteByte(FormatVersion);
        fs.Write(_salt, 0, _salt.Length);
        fs.Write(blob, 0, blob.Length);
    }

    /// <summary>Re-encrypts the vault under a brand-new master password (new salt + new key).</summary>
    public void ChangeMasterPassword(List<VaultEntry> entries, string newMasterPassword)
    {
        _salt = CryptoService.GenerateSalt();
        _key = CryptoService.DeriveKey(newMasterPassword, _salt);
        Save(entries);
    }

    /// <summary>Clears the in-memory key on lock/exit. Best-effort only - see plan notes on managed-memory limits.</summary>
    public void Lock()
    {
        if (_key != null) Array.Clear(_key, 0, _key.Length);
        if (_salt != null) Array.Clear(_salt, 0, _salt.Length);
        _key = null;
        _salt = null;
    }
}
