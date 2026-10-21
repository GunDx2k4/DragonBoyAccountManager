using DragonBoyAccountManager.Core.Accounts;

namespace DragonBoyAccountManager.Core.Storage;

public interface IAccountStorageService
{
    Task<List<Account>> LoadAllAsync();
    Task SaveAllAsync(IEnumerable<Account> accounts);
    Task AddOrUpdateAsync(Account account);
    Task DeleteAsync(Guid id);
}
