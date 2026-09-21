namespace RhodiumVault.Services;

/// <summary>Rough, dependency-free strength estimate for user-chosen master passwords. A hint, not a guarantee.</summary>
public static class PasswordStrength
{
    private static readonly string[] CommonPasswords =
    {
        "password", "passwort", "123456", "12345678", "qwerty", "abc123", "letmein", "iloveyou", "admin", "welcome", "monkey", "dragon"
    };

    public static double EstimateBits(string password)
    {
        if (string.IsNullOrEmpty(password)) return 0;

        int pool = 0;
        if (password.Any(char.IsLower)) pool += 26;
        if (password.Any(char.IsUpper)) pool += 26;
        if (password.Any(char.IsDigit)) pool += 10;
        if (password.Any(c => !char.IsLetterOrDigit(c))) pool += 32;
        if (pool == 0) return 0;

        double bits = password.Length * Math.Log2(pool);

        // Penalise obvious patterns: repeated characters and common passwords.
        int distinct = password.Distinct().Count();
        if (distinct < password.Length / 2) bits *= 0.6;
        var lower = password.ToLowerInvariant();
        if (CommonPasswords.Any(lower.Contains)) bits *= 0.4;

        return bits;
    }

    /// <summary>Returns a label and the name of a theme brush resource to colour it with.</summary>
    public static (string Label, string BrushKey) Describe(string password)
    {
        var bits = EstimateBits(password);
        if (bits < 40) return ("Weak - easy to guess. Try a longer passphrase.", "DangerBrush");
        if (bits < 60) return ("Fair - longer would be better.", "WarningBrush");
        if (bits < 80) return ("Good", "SuccessBrush");
        return ("Strong", "SuccessBrush");
    }
}
