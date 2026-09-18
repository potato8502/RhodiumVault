using System.Security.Cryptography;
using System.Text;

namespace RhodiumVault.Services;

public static class PasswordGeneratorService
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{}?";

    public static string Generate(int length, bool useUpper, bool useLower, bool useDigits, bool useSymbols)
    {
        length = Math.Clamp(length, 4, 128);

        var pool = new StringBuilder();
        if (useUpper) pool.Append(Upper);
        if (useLower) pool.Append(Lower);
        if (useDigits) pool.Append(Digits);
        if (useSymbols) pool.Append(Symbols);

        if (pool.Length == 0)
        {
            pool.Append(Lower);
            useLower = true;
        }

        var poolStr = pool.ToString();
        var result = new StringBuilder(length);

        // Guarantee at least one character from each selected category, then fill the rest randomly.
        var required = new List<string>();
        if (useUpper) required.Add(Upper);
        if (useLower) required.Add(Lower);
        if (useDigits) required.Add(Digits);
        if (useSymbols) required.Add(Symbols);

        foreach (var set in required)
        {
            if (result.Length >= length) break;
            result.Append(set[RandomNumberGenerator.GetInt32(set.Length)]);
        }

        while (result.Length < length)
        {
            result.Append(poolStr[RandomNumberGenerator.GetInt32(poolStr.Length)]);
        }

        // Shuffle so the guaranteed-category characters aren't always at the start (Fisher-Yates).
        var chars = result.ToString().ToCharArray();
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
