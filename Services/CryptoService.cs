using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace RhodiumVault.Services;

/// <summary>Argon2id cost parameters. They are stored in the vault header so they can be raised later.</summary>
public readonly record struct KdfParams(int MemoryKiB, int Iterations, int Parallelism)
{
    /// <summary>What every vault created before format v2 used (OWASP minimum).</summary>
    public static readonly KdfParams Legacy = new(19456, 2, 1);

    /// <summary>Default for new vaults and for upgrades.</summary>
    public static readonly KdfParams Current = new(65536, 3, 4);

    public bool IsSane =>
        MemoryKiB is >= 8192 and <= 1048576 && Iterations is >= 1 and <= 20 && Parallelism is >= 1 and <= 16;
}

public static class CryptoService
{
    public const int SaltSize = 16;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    private const int KeySize = 32;

    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    /// <summary>Derives a 256-bit key from the master password using Argon2id.</summary>
    public static byte[] DeriveKey(string masterPassword, byte[] salt, KdfParams kdf)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(masterPassword);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = kdf.Parallelism,
                Iterations = kdf.Iterations,
                MemorySize = kdf.MemoryKiB,
            };
            return argon2.GetBytes(KeySize);
        }
        finally
        {
            Array.Clear(passwordBytes, 0, passwordBytes.Length);
        }
    }

    /// <summary>
    /// Encrypts plaintext with AES-256-GCM. Returns nonce || ciphertext || tag.
    /// <paramref name="associatedData"/> (e.g. the file header) is authenticated but not encrypted.
    /// </summary>
    public static byte[] Encrypt(byte[] plaintext, byte[] key, byte[]? associatedData = null)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);
        return result;
    }

    /// <summary>Decrypts a nonce||ciphertext||tag blob. Throws CryptographicException if the key is wrong or data/header was tampered with.</summary>
    public static byte[] Decrypt(byte[] blob, byte[] key, byte[]? associatedData = null)
    {
        if (blob.Length < NonceSize + TagSize)
            throw new CryptographicException("Encrypted blob is too short.");

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertextLength = blob.Length - NonceSize - TagSize;
        var ciphertext = new byte[ciphertextLength];

        Buffer.BlockCopy(blob, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(blob, NonceSize, ciphertext, 0, ciphertextLength);
        Buffer.BlockCopy(blob, NonceSize + ciphertextLength, tag, 0, TagSize);

        var plaintext = new byte[ciphertextLength];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
        return plaintext;
    }
}
