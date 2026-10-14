using System.IO;
using System.Text.Json;

using DragonBoyAccountManager.GUI.Models;

namespace DragonBoyAccountManager.GUI.Services.Storage;

public sealed class AccountStorageService : IAccountStorageService
{
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

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, accounts, JsonOptions);
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
