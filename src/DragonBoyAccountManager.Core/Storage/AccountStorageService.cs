using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DragonBoyAccountManager.Core.Accounts;

namespace DragonBoyAccountManager.Core.Storage;

public sealed class AccountStorageService : IAccountStorageService
{
    private const string EncryptedPrefix = "enc:v1:";
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AccountStorageService(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            _filePath = customPath;
        }
        else
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            _filePath = Path.Combine(folder, "accounts.json");
        }
    }

    public async Task<List<Account>> LoadAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return await LoadAllInternalAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<Account>> LoadAllInternalAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            await using var stream = File.OpenRead(_filePath);
            var list = await JsonSerializer.DeserializeAsync<List<Account>>(stream, JsonOptions);
            if (list != null)
            {
                foreach (var account in list)
                {
                    account.Password = DecryptPassword(account.Password);
                }
            }
            return list ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountStorageService] Lỗi khi đọc file accounts: {ex.Message}");
            return [];
        }
    }

    public async Task SaveAllAsync(IEnumerable<Account> accounts)
    {
        await _lock.WaitAsync();
        try
        {
            await SaveAllInternalAsync(accounts);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveAllInternalAsync(IEnumerable<Account> accounts)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var toSave = accounts.Select(CloneWithEncryptedPassword).ToList();
        var tempPath = _filePath + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, toSave, JsonOptions);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static Account CloneWithEncryptedPassword(Account a)
    {
        return new Account
        {
            Id = a.Id,
            Username = a.Username,
            Password = EncryptPassword(a.Password),
            ServerId = a.ServerId,
            ServerName = a.ServerName,
            CreatedAt = a.CreatedAt,
            LastLogin = a.LastLogin,
            GameSpeed = a.GameSpeed,
            AutoLogin = a.AutoLogin,
            AutoSettings = a.AutoSettings
        };
    }

    private static string EncryptPassword(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword)) return string.Empty;
        if (plainPassword.StartsWith(EncryptedPrefix, StringComparison.Ordinal)) return plainPassword;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainPassword);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                return EncryptedPrefix + Convert.ToBase64String(encryptedBytes);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountStorageService] Lỗi mã hóa mật khẩu: {ex.Message}");
        }

        return plainPassword;
    }

    private static string DecryptPassword(string cipherOrPlainPassword)
    {
        if (string.IsNullOrEmpty(cipherOrPlainPassword)) return string.Empty;
        if (!cipherOrPlainPassword.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            return cipherOrPlainPassword;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                string base64 = cipherOrPlainPassword[EncryptedPrefix.Length..];
                byte[] cipherBytes = Convert.FromBase64String(base64);
                byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountStorageService] Lỗi giải mã mật khẩu: {ex.Message}");
        }

        return cipherOrPlainPassword;
    }

    public async Task AddOrUpdateAsync(Account account)
    {
        await _lock.WaitAsync();
        try
        {
            var list = await LoadAllInternalAsync();
            var index = list.FindIndex(a => a.Id == account.Id);
            if (index >= 0)
            {
                list[index] = account;
            }
            else
            {
                list.Add(account);
            }
            await SaveAllInternalAsync(list);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var list = await LoadAllInternalAsync();
            var item = list.FirstOrDefault(a => a.Id == id);
            if (item != null)
            {
                list.Remove(item);
                await SaveAllInternalAsync(list);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}
