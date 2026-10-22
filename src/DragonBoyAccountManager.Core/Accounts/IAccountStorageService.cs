namespace DragonBoyAccountManager.Core.Accounts;

public interface IAccountStorageService
{
    Task<List<Account>> LoadAllAsync();
    Task SaveAllAsync(IEnumerable<Account> accounts);
    Task AddOrUpdateAsync(Account account);
    Task DeleteAsync(Guid id);
}
