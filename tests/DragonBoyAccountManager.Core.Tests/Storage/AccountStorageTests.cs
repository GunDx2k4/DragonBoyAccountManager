using DragonBoyAccountManager.Core.Accounts;
using DragonBoyAccountManager.Core.Storage;

namespace DragonBoyAccountManager.Core.Tests.Storage;

public class AccountStorageTests : IDisposable
{
    private readonly string _tempFile;

    public AccountStorageTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"dbam_test_acc_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public async Task SaveAllAsync_And_LoadAllAsync_PreservesAccountData()
    {
        var service = new AccountStorageService(_tempFile);

        var account = new Account
        {
            Username = "superSaiyan99",
            Password = "secretPassword123",
            ServerId = 2,
            ServerName = "Vũ trụ 2",
            AutoLogin = true,
            GameSpeed = 1.5f,
            AutoSettings = new AccountAutoSettings
            {
                UseTdlt = true,
                AutoPickItems = false,
                MinHpPercent = 35
            }
        };

        await service.SaveAllAsync([account]);
        var loaded = await service.LoadAllAsync();

        Assert.Single(loaded);
        var loadedAcc = loaded[0];
        Assert.Equal(account.Id, loadedAcc.Id);
        Assert.Equal(account.Username, loadedAcc.Username);
        Assert.Equal(account.Password, loadedAcc.Password);
        Assert.Equal(account.ServerId, loadedAcc.ServerId);
        Assert.Equal(account.ServerName, loadedAcc.ServerName);
        Assert.Equal(account.AutoLogin, loadedAcc.AutoLogin);
        Assert.Equal(account.GameSpeed, loadedAcc.GameSpeed);
        Assert.True(loadedAcc.AutoSettings.UseTdlt);
        Assert.False(loadedAcc.AutoSettings.AutoPickItems);
        Assert.Equal(35, loadedAcc.AutoSettings.MinHpPercent);
    }

    [Fact]
    public async Task LoadAllAsync_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        var service = new AccountStorageService(_tempFile);

        var loaded = await service.LoadAllAsync();

        Assert.NotNull(loaded);
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task AddOrUpdateAsync_AddsNewAccountAndUpdatesExisting()
    {
        var service = new AccountStorageService(_tempFile);

        var acc = new Account
        {
            Username = "user1",
            Password = "pwd"
        };

        await service.AddOrUpdateAsync(acc);
        var loaded = await service.LoadAllAsync();
        Assert.Single(loaded);

        acc.Username = "user1_updated";
        await service.AddOrUpdateAsync(acc);
        var reloaded = await service.LoadAllAsync();
        Assert.Single(reloaded);
        Assert.Equal("user1_updated", reloaded[0].Username);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAccountSuccessfully()
    {
        var service = new AccountStorageService(_tempFile);

        var acc1 = new Account { Username = "user1", Password = "p1" };
        var acc2 = new Account { Username = "user2", Password = "p2" };

        await service.SaveAllAsync([acc1, acc2]);
        await service.DeleteAsync(acc1.Id);

        var remaining = await service.LoadAllAsync();
        Assert.Single(remaining);
        Assert.Equal(acc2.Id, remaining[0].Id);
    }
}
