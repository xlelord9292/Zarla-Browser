using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Zarla.Core.Security;

/// <summary>
/// Secure password manager with AES-256 encryption
/// </summary>
public class PasswordManager
{
    private readonly string _dataPath;
    private readonly string _keyPath;
    private byte[]? _masterKey;
    private List<PasswordEntry> _entries = new();
    private bool _isUnlocked;

    public bool IsUnlocked => _isUnlocked;
    public bool HasMasterPassword => File.Exists(_keyPath);
    public IReadOnlyList<PasswordEntry> Entries => _entries.AsReadOnly();

    public event EventHandler? Locked;
    public event EventHandler? Unlocked;

    public PasswordManager(string dataFolder)
    {
        _dataPath = Path.Combine(dataFolder, "passwords.enc");
        _keyPath = Path.Combine(dataFolder, "passwords.key");
    }

    /// <summary>
    /// Creates a new master password for the password manager
    /// </summary>
    public bool SetMasterPassword(string masterPassword)
    {
        if (string.IsNullOrEmpty(masterPassword) || masterPassword.Length < 8)
            return false;

        try
        {
            // Generate a random salt
            var salt = RandomNumberGenerator.GetBytes(32);

            // Derive key from password using PBKDF2
            using var pbkdf2 = new Rfc2898DeriveBytes(
                masterPassword,
                salt,
                iterations: 100000,
                HashAlgorithmName.SHA256);

            _masterKey = pbkdf2.GetBytes(32); // 256-bit key

            // Store salt and verification hash
            var verificationHash = SHA256.HashData(_masterKey);
            var keyData = new MasterKeyData
            {
                Salt = Convert.ToBase64String(salt),
                VerificationHash = Convert.ToBase64String(verificationHash)
            };

            var keyJson = JsonSerializer.Serialize(keyData);
            File.WriteAllText(_keyPath, keyJson);

            _isUnlocked = true;
            _entries = new List<PasswordEntry>();

            // Save empty password file
            SavePasswords();

            Unlocked?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Unlocks the password manager with the master password
    /// </summary>
    public bool Unlock(string masterPassword)
    {
        if (!HasMasterPassword)
            return false;

        try
        {
            // Load key data
            var keyJson = File.ReadAllText(_keyPath);
            var keyData = JsonSerializer.Deserialize<MasterKeyData>(keyJson);
            if (keyData == null)
                return false;

            var salt = Convert.FromBase64String(keyData.Salt);

            // Derive key from password
            using var pbkdf2 = new Rfc2898DeriveBytes(
                masterPassword,
                salt,
                iterations: 100000,
                HashAlgorithmName.SHA256);

            var derivedKey = pbkdf2.GetBytes(32);

            // Verify the key
            var verificationHash = SHA256.HashData(derivedKey);
            var expectedHash = Convert.FromBase64String(keyData.VerificationHash);

            if (!verificationHash.SequenceEqual(expectedHash))
                return false;

            _masterKey = derivedKey;
            _isUnlocked = true;

            // Load passwords
            LoadPasswords();

            Unlocked?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Locks the password manager
    /// </summary>
    public void Lock()
    {
        _masterKey = null;
        _entries.Clear();
        _isUnlocked = false;
        Locked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Adds a new password entry
    /// </summary>
    public bool AddPassword(string website, string username, string password, string? notes = null)
    {
        if (!_isUnlocked || _masterKey == null)
            return false;

        var entry = new PasswordEntry
        {
            Id = Guid.NewGuid().ToString(),
            Website = website,
            Username = username,
            Password = password,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        _entries.Add(entry);
        SavePasswords();
        return true;
    }

    /// <summary>
    /// Updates an existing password entry
    /// </summary>
    public bool UpdatePassword(string id, string? website = null, string? username = null, string? password = null, string? notes = null)
    {
        if (!_isUnlocked || _masterKey == null)
            return false;

        var entry = _entries.FirstOrDefault(e => e.Id == id);
        if (entry == null)
            return false;

        if (website != null) entry.Website = website;
        if (username != null) entry.Username = username;
        if (password != null) entry.Password = password;
        if (notes != null) entry.Notes = notes;
        entry.ModifiedAt = DateTime.UtcNow;

        SavePasswords();
        return true;
    }

    /// <summary>
    /// Deletes a password entry
    /// </summary>
    public bool DeletePassword(string id)
    {
        if (!_isUnlocked)
            return false;

        var entry = _entries.FirstOrDefault(e => e.Id == id);
        if (entry == null)
            return false;

        _entries.Remove(entry);
        SavePasswords();
        return true;
    }

    /// <summary>
    /// Searches for passwords matching the query
    /// </summary>
    public List<PasswordEntry> Search(string query)
    {
        if (!_isUnlocked)
            return new List<PasswordEntry>();

        var lowerQuery = query.ToLower();
        return _entries
            .Where(e => e.Website.ToLower().Contains(lowerQuery) ||
                        e.Username.ToLower().Contains(lowerQuery))
            .ToList();
    }

    /// <summary>
    /// Gets password for a specific website
    /// </summary>
    public PasswordEntry? GetPasswordForSite(string url)
    {
        if (!_isUnlocked)
            return null;

        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLower();

            // Remove www prefix
            if (host.StartsWith("www."))
                host = host.Substring(4);

            return _entries.FirstOrDefault(e =>
            {
                var entryHost = e.Website.ToLower();
                if (entryHost.StartsWith("http"))
                {
                    try
                    {
                        var entryUri = new Uri(entryHost);
                        entryHost = entryUri.Host;
                    }
                    catch { }
                }
                if (entryHost.StartsWith("www."))
                    entryHost = entryHost.Substring(4);

                return entryHost == host || host.EndsWith("." + entryHost);
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Generates a secure random password
    /// </summary>
    public static string GeneratePassword(int length = 16, bool includeSymbols = true)
    {
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        const string symbols = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        var chars = lowercase + uppercase + digits;
        if (includeSymbols) chars += symbols;

        var password = new StringBuilder();
        var bytes = RandomNumberGenerator.GetBytes(length);

        for (int i = 0; i < length; i++)
        {
            password.Append(chars[bytes[i] % chars.Length]);
        }

        return password.ToString();
    }

    /// <summary>
    /// Changes the master password
    /// </summary>
    public bool ChangeMasterPassword(string currentPassword, string newPassword)
    {
        if (!_isUnlocked || _masterKey == null)
            return false;

        // Verify current password
        if (!Unlock(currentPassword))
            return false;

        // Keep entries
        var entries = _entries.ToList();

        // Set new password
        if (!SetMasterPassword(newPassword))
            return false;

        // Restore entries
        _entries = entries;
        SavePasswords();

        return true;
    }

    /// <summary>
    /// Exports passwords (encrypted) for backup
    /// </summary>
    public string? ExportPasswords()
    {
        if (!_isUnlocked || _masterKey == null)
            return null;

        try
        {
            var json = JsonSerializer.Serialize(_entries);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Imports passwords from backup
    /// </summary>
    public bool ImportPasswords(string data, bool merge = true)
    {
        if (!_isUnlocked || _masterKey == null)
            return false;

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(data));
            var imported = JsonSerializer.Deserialize<List<PasswordEntry>>(json);
            if (imported == null)
                return false;

            if (merge)
            {
                foreach (var entry in imported)
                {
                    // Skip duplicates
                    if (!_entries.Any(e => e.Website == entry.Website && e.Username == entry.Username))
                    {
                        entry.Id = Guid.NewGuid().ToString();
                        _entries.Add(entry);
                    }
                }
            }
            else
            {
                _entries = imported;
            }

            SavePasswords();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SavePasswords()
    {
        if (_masterKey == null)
            return;

        try
        {
            var json = JsonSerializer.Serialize(_entries);
            var plaintext = Encoding.UTF8.GetBytes(json);

            // Encrypt with AES-256-GCM
            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var ciphertext = new byte[plaintext.Length];

            using var aes = new AesGcm(_masterKey, 16);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            // Combine: nonce + tag + ciphertext
            var encrypted = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, encrypted, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, encrypted, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, encrypted, nonce.Length + tag.Length, ciphertext.Length);

            File.WriteAllBytes(_dataPath, encrypted);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save passwords: {ex.Message}");
        }
    }

    private void LoadPasswords()
    {
        if (_masterKey == null || !File.Exists(_dataPath))
        {
            _entries = new List<PasswordEntry>();
            return;
        }

        try
        {
            var encrypted = File.ReadAllBytes(_dataPath);

            if (encrypted.Length < 28) // minimum: 12 nonce + 16 tag
            {
                _entries = new List<PasswordEntry>();
                return;
            }

            // Extract nonce, tag, ciphertext
            var nonce = new byte[12];
            var tag = new byte[16];
            var ciphertext = new byte[encrypted.Length - 28];

            Buffer.BlockCopy(encrypted, 0, nonce, 0, 12);
            Buffer.BlockCopy(encrypted, 12, tag, 0, 16);
            Buffer.BlockCopy(encrypted, 28, ciphertext, 0, ciphertext.Length);

            // Decrypt
            var plaintext = new byte[ciphertext.Length];
            using var aes = new AesGcm(_masterKey, 16);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            var json = Encoding.UTF8.GetString(plaintext);
            _entries = JsonSerializer.Deserialize<List<PasswordEntry>>(json) ?? new List<PasswordEntry>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load passwords: {ex.Message}");
            _entries = new List<PasswordEntry>();
        }
    }
}

/// <summary>
/// Represents a stored password entry
/// </summary>
public class PasswordEntry
{
    public string Id { get; set; } = "";
    public string Website { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

/// <summary>
/// Master key data stored on disk
/// </summary>
internal class MasterKeyData
{
    public string Salt { get; set; } = "";
    public string VerificationHash { get; set; } = "";
}
