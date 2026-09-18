using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace RhodiumVault.Services;

public static class CryptoService
{
    public const int SaltSize = 16;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    private const int KeySize = 32;

    public static byte[] GenerateSalt()
    {
        return RandomNumberGenerator.GetBytes(SaltSize);
    }

    /// <summary>Derives a 256-bit key from the master password using Argon2id (OWASP baseline params).</summary>
    public static byte[] DeriveKey(string masterPassword, byte[] salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(masterPassword);
        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            DegreeOfParallelism = 1,
            Iterations = 2,
            MemorySize = 19456, // KiB (~19 MiB), OWASP baseline
        };
        return argon2.GetBytes(KeySize);
    }

    /// <summary>Encrypts plaintext with AES-256-GCM. Returns nonce || ciphertext || tag.</summary>
    public static byte[] Encrypt(byte[] plaintext, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);
        return result;
    }

    /// <summary>Decrypts a nonce||ciphertext||tag blob. Throws CryptographicException if the key is wrong or data was tampered with.</summary>
    public static byte[] Decrypt(byte[] blob, byte[] key)
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
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
