using DragonBoyAccountManager.GUI.Models;

namespace DragonBoyAccountManager.GUI.Services.Storage;

public interface IAccountStorageService
{
    Task<List<Account>> LoadAllAsync();
    Task SaveAllAsync(IEnumerable<Account> accounts);
    Task AddOrUpdateAsync(Account account);
    Task DeleteAsync(Guid id);
}
