using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using RhodiumVault.Models;
using RhodiumVault.Services;
using Xunit;

namespace RhodiumVault.Tests;

/// <summary>Uses a cheap KDF where possible; the vault tests exercise the real (current) parameters at least once.</summary>
public class CryptoTests
{
    private static readonly KdfParams Cheap = new(8192, 1, 1);

    [Fact]
    public void Kdf_is_deterministic_and_salt_sensitive()
    {
        var salt = CryptoService.GenerateSalt();
        var a = CryptoService.DeriveKey("pw", salt, Cheap);
        var b = CryptoService.DeriveKey("pw", salt, Cheap);
        var c = CryptoService.DeriveKey("pw", CryptoService.GenerateSalt(), Cheap);
        Assert.Equal(32, a.Length);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Kdf_depends_on_parameters()
    {
        var salt = CryptoService.GenerateSalt();
        Assert.NotEqual(CryptoService.DeriveKey("pw", salt, new KdfParams(8192, 1, 1)),
                        CryptoService.DeriveKey("pw", salt, new KdfParams(8192, 2, 1)));
    }

    [Fact]
    public void Encrypt_roundtrips_and_uses_fresh_nonce()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var plain = Encoding.UTF8.GetBytes("secret data");
        var a = CryptoService.Encrypt(plain, key);
        var b = CryptoService.Encrypt(plain, key);
        Assert.NotEqual(a, b);
        Assert.Equal(plain, CryptoService.Decrypt(a, key));
    }

    [Fact]
    public void Wrong_key_is_rejected()
    {
        var blob = CryptoService.Encrypt(new byte[] { 1, 2, 3 }, RandomNumberGenerator.GetBytes(32));
        Assert.ThrowsAny<CryptographicException>(() => CryptoService.Decrypt(blob, RandomNumberGenerator.GetBytes(32)));
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var blob = CryptoService.Encrypt(new byte[] { 1, 2, 3, 4 }, key);
        blob[CryptoService.NonceSize] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => CryptoService.Decrypt(blob, key));
    }

    [Fact]
    public void Associated_data_is_authenticated()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var blob = CryptoService.Encrypt(new byte[] { 9 }, key, new byte[] { 1, 2, 3 });
        Assert.Equal(new byte[] { 9 }, CryptoService.Decrypt(blob, key, new byte[] { 1, 2, 3 }));
        Assert.ThrowsAny<CryptographicException>(() => CryptoService.Decrypt(blob, key, new byte[] { 1, 2, 4 }));
        Assert.ThrowsAny<CryptographicException>(() => CryptoService.Decrypt(blob, key, null));
    }
}

public class VaultTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rv-tests-" + Guid.NewGuid().ToString("N"));
    private string VaultFile => Path.Combine(_dir, "vault.dat");

    public VaultTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static List<VaultEntry> Sample() => new()
    {
        new() { Title = "GitHub", Username = "jonah", Password = "hunter2", IsFavorite = true },
        new() { Title = "Steam", Username = "j", Password = "correcthorse" },
    };

    [Fact]
    public void Create_save_reopen_roundtrip()
    {
        var v = new VaultService(VaultFile);
        v.CreateNew("master-password");
        v.Save(Sample());

        var loaded = new VaultService(VaultFile).Unlock("master-password");
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Count);
        Assert.True(loaded.First(e => e.Title == "GitHub").IsFavorite);
    }

    [Fact]
    public void Wrong_password_returns_null()
    {
        var v = new VaultService(VaultFile);
        v.CreateNew("master-password");
        Assert.Null(new VaultService(VaultFile).Unlock("not-the-password"));
    }

    [Fact]
    public void New_vaults_use_format_v2_with_current_kdf_params_in_the_header()
    {
        new VaultService(VaultFile).CreateNew("master-password");
        var bytes = File.ReadAllBytes(VaultFile);
        Assert.Equal(2, bytes[4]);
        Assert.Equal(KdfParams.Current.MemoryKiB, (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(6, 4)));
        Assert.Equal(KdfParams.Current.Iterations, (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(10, 4)));
        Assert.Equal(KdfParams.Current.Parallelism, (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(14, 4)));
    }

    [Fact]
    public void Tampering_with_the_header_is_detected()
    {
        var v = new VaultService(VaultFile);
        v.CreateNew("master-password");
        v.Save(Sample());

        var bytes = File.ReadAllBytes(VaultFile);
        bytes[10] ^= 0x01; // change the stored iteration count
        File.WriteAllBytes(VaultFile, bytes);
        File.Delete(VaultFile + ".bak"); // make sure this can't be rescued from the backup

        var result = Record.Exception(() => new VaultService(VaultFile).Unlock("master-password"));
        // Either rejected as invalid, or decrypt fails -> null. Never a successful unlock.
        Assert.True(result is InvalidDataException || result is null);
        if (result is null) Assert.Null(new VaultService(VaultFile).Unlock("master-password"));
    }

    [Fact]
    public void Save_is_atomic_leaves_backup_and_no_temp_file()
    {
        var v = new VaultService(VaultFile);
        v.CreateNew("master-password");
        v.Save(Sample());
        v.Save(Sample());

        Assert.True(File.Exists(VaultFile));
        Assert.True(File.Exists(VaultFile + ".bak"));
        Assert.False(File.Exists(VaultFile + ".tmp"));
    }

    [Fact]
    public void Damaged_vault_is_recovered_from_backup()
    {
        var v = new VaultService(VaultFile);
        v.CreateNew("master-password");
        v.Save(Sample()); // first save after create -> .bak holds the (empty) created vault
        v.Save(Sample());

        File.WriteAllBytes(VaultFile, new byte[] { 1, 2, 3 }); // simulate a truncated write

        var recovered = new VaultService(VaultFile);
        var entries = recovered.Unlock("master-password");
        Assert.NotNull(entries);
        Assert.True(recovered.RecoveredFromBackup);
        // and the main file is healthy again
        Assert.NotNull(new VaultService(VaultFile).Unlock("master-password"));
    }

    [Fact]
    public void Unreadable_vault_without_backup_throws_instead_of_pretending_wrong_password()
    {
        File.WriteAllBytes(VaultFile, new byte[] { 1, 2, 3 });
        Assert.Throws<InvalidDataException>(() => new VaultService(VaultFile).Unlock("anything"));
    }

    [Fact]
    public void Legacy_v1_vault_still_opens_and_is_upgraded_to_v2()
    {
        // Build a v1 file exactly as the old app did: magic, version 1, salt, blob, fixed 19 MiB/2/1, no AAD.
        var salt = CryptoService.GenerateSalt();
        var key = CryptoService.DeriveKey("old-password", salt, KdfParams.Legacy);
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Sample());
        var blob = CryptoService.Encrypt(json, key);
        using (var fs = File.Create(VaultFile))
        {
            fs.Write(Encoding.ASCII.GetBytes("RVLT"));
            fs.WriteByte(1);
            fs.Write(salt);
            fs.Write(blob);
        }

        var entries = new VaultService(VaultFile).Unlock("old-password");
        Assert.NotNull(entries);
        Assert.Equal(2, entries!.Count);
        Assert.Equal(2, File.ReadAllBytes(VaultFile)[4]); // upgraded in place
        Assert.Equal(2, new VaultService(VaultFile).Unlock("old-password")!.Count); // and still opens
    }

    [Fact]
    public void Change_master_password_switches_over_completely()
    {
        var v = new VaultService(VaultFile);
        v.CreateNew("first-password");
        var entries = Sample();
        v.Save(entries);
        v.ChangeMasterPassword(entries, "second-password");

        Assert.Null(new VaultService(VaultFile).Unlock("first-password"));
        Assert.Equal(2, new VaultService(VaultFile).Unlock("second-password")!.Count);
    }

    [Fact]
    public void Failed_password_change_leaves_the_session_and_disk_untouched()
    {
        var v = new VaultService(VaultFile);
        v.CreateNew("first-password");
        var entries = Sample();
        v.Save(entries);

        // Block the write: a directory where the temp file must be created.
        Directory.CreateDirectory(VaultFile + ".tmp");
        Assert.ThrowsAny<Exception>(() => v.ChangeMasterPassword(entries, "second-password"));
        Directory.Delete(VaultFile + ".tmp");

        // The old password still works on disk, and the running session can still save with its old key.
        v.Save(entries);
        Assert.Equal(2, new VaultService(VaultFile).Unlock("first-password")!.Count);
    }
}

public class GeneratorAndStrengthTests
{
    [Fact]
    public void Generated_password_has_requested_length_and_all_selected_categories()
    {
        for (int i = 0; i < 50; i++)
        {
            var p = PasswordGeneratorService.Generate(20, true, true, true, true);
            Assert.Equal(20, p.Length);
            Assert.Contains(p, char.IsUpper);
            Assert.Contains(p, char.IsLower);
            Assert.Contains(p, char.IsDigit);
            Assert.Contains(p, c => !char.IsLetterOrDigit(c));
        }
    }

    [Fact]
    public void Strength_orders_obvious_cases_sensibly()
    {
        Assert.True(PasswordStrength.EstimateBits("password") < PasswordStrength.EstimateBits("Tr0ub4dor&3xQ!zP9"));
        Assert.Equal("DangerBrush", PasswordStrength.Describe("abc12345").BrushKey);
        Assert.Equal("SuccessBrush", PasswordStrength.Describe("k9$Lm2#vQx7!pZr4Wt8&").BrushKey);
    }
}
