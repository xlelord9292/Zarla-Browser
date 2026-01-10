using System.Security.Cryptography;
using System.Text;

namespace Zarla.Core.Security;

/// <summary>
/// Provides secure storage for sensitive data like API keys using Windows DPAPI
/// Also provides portable encryption for embedded API keys using AES
/// </summary>
public static class SecureStorage
{
    private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("ZarlaBrowserSecureStorage2026");
    
    // Portable encryption key - MUST match build.ps1 exactly!
    // Uses fixed string so encryption in PowerShell matches decryption in C#
    private static readonly byte[] PortableKey = SHA256.HashData(
        Encoding.UTF8.GetBytes("ZarlaBrowser-Embedded-Key-v1-Zarla.Core"));
    private static readonly byte[] PortableIV = MD5.HashData(
        Encoding.UTF8.GetBytes("Zarla-IV-2026"));

    /// <summary>
    /// Encrypts a string using Windows DPAPI (Data Protection API)
    /// The data can only be decrypted by the same user on the same machine
    /// </summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(
                plainBytes,
                AdditionalEntropy,
                DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Encryption failed: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Decrypts a string that was encrypted using Windows DPAPI
    /// </summary>
    public static string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                AdditionalEntropy,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Decryption failed: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Encrypts a string using AES - portable across machines (for embedded API keys)
    /// Use this for API keys that need to work on any user's machine
    /// </summary>
    public static string EncryptPortable(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            using var aes = Aes.Create();
            aes.Key = PortableKey;
            aes.IV = PortableIV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Portable encryption failed: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Decrypts a string using AES - portable across machines (for embedded API keys)
    /// </summary>
    public static string DecryptPortable(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        try
        {
            using var aes = Aes.Create();
            aes.Key = PortableKey;
            aes.IV = PortableIV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Portable decryption failed: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Checks if a string appears to be encrypted (base64 format)
    /// </summary>
    public static bool IsEncrypted(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        try
        {
            Convert.FromBase64String(text);
            return text.Length > 20 && !text.Contains("sk-") && !text.Contains("gsk_");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets an API key, decrypting it if necessary
    /// Tries portable decryption first (for embedded keys), then DPAPI, then environment variable
    /// </summary>
    public static string GetApiKey(string? encryptedKey, string envVarName)
    {
        // First try decrypting the stored key (try both methods)
        if (!string.IsNullOrEmpty(encryptedKey))
        {
            // Try portable decryption first (for embedded API keys from config)
            var portableDecrypted = DecryptPortable(encryptedKey);
            if (!string.IsNullOrEmpty(portableDecrypted) && ValidateApiKeyFormat(portableDecrypted))
                return portableDecrypted;
            
            // Try DPAPI decryption (for user-stored keys)
            var dpApiDecrypted = Decrypt(encryptedKey);
            if (!string.IsNullOrEmpty(dpApiDecrypted) && ValidateApiKeyFormat(dpApiDecrypted))
                return dpApiDecrypted;
        }

        // Fall back to environment variable
        return Environment.GetEnvironmentVariable(envVarName) ?? string.Empty;
    }

    /// <summary>
    /// Stores an API key securely by encrypting it
    /// </summary>
    public static string StoreApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return string.Empty;

        return Encrypt(apiKey);
    }

    /// <summary>
    /// Validates that an API key looks correct (basic format check)
    /// </summary>
    public static bool ValidateApiKeyFormat(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        // Check for common API key formats
        // Groq: gsk_...
        // OpenAI: sk-...
        // Generic: at least 20 chars with alphanumeric
        return apiKey.StartsWith("gsk_") ||
               apiKey.StartsWith("sk-") ||
               (apiKey.Length >= 20 && apiKey.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
    }
}
