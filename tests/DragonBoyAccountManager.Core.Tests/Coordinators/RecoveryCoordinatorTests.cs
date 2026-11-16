using DragonBoyAccountManager.Core.Automation.Coordinators;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.Core.Tests.Mocks;

namespace DragonBoyAccountManager.Core.Tests.Coordinators;

public class RecoveryCoordinatorTests
{
    [Fact]
    public async Task TryUsePeanAsync_WhenHpBelowThreshold_DispatchesUseItemCommand()
    {
        var coordinator = new RecoveryCoordinator();
        var context = new MockSessionContext();

        var player = PlayerSnapshot.Empty with
        {
            Id = 1,
            Hp = 20,
            MaxHp = 100,
            Mp = 50,
            MaxMp = 100,
            BagItems =
            [
                new InventoryItem(0, 500, "Đậu thần cấp 5", "", 10, 0, IsPean: true)
            ]
        };

        var options = new RecoveryOptions(
            AutoUsePean: true,
            MinHpPercent: 30,
            MinMpPercent: 20);

        bool used = await coordinator.TryUsePeanAsync(
            context,
            player,
            options,
            "[Test]",
            null,
            CancellationToken.None);

        Assert.True(used);
        Assert.Single(context.MockDispatcher.DispatchedCommands);
        var cmd = Assert.IsType<UseItemCommand>(context.MockDispatcher.DispatchedCommands[0]);
        Assert.Equal(500, cmd.TemplateId);
        Assert.Equal("Đậu thần cấp 5", cmd.ItemName);
    }

    [Fact]
    public async Task TryUsePeanAsync_WhenHpAboveThreshold_DoesNotDispatchCommand()
    {
        var coordinator = new RecoveryCoordinator();
        var context = new MockSessionContext();

        var player = PlayerSnapshot.Empty with
        {
            Id = 1,
            Hp = 80,
            MaxHp = 100,
            Mp = 80,
            MaxMp = 100,
            BagItems =
            [
                new InventoryItem(0, 500, "Đậu thần cấp 5", "", 10, 0, IsPean: true)
            ]
        };

        var options = new RecoveryOptions(
            AutoUsePean: true,
            MinHpPercent: 30,
            MinMpPercent: 20);

        bool used = await coordinator.TryUsePeanAsync(
            context,
            player,
            options,
            "[Test]",
            null,
            CancellationToken.None);

        Assert.False(used);
        Assert.Empty(context.MockDispatcher.DispatchedCommands);
    }

    [Fact]
    public async Task TryUsePeanAsync_WhenNoPeanInBag_ReturnsFalse()
    {
        var coordinator = new RecoveryCoordinator();
        var context = new MockSessionContext();

        var player = PlayerSnapshot.Empty with
        {
            Id = 1,
            Hp = 10,
            MaxHp = 100,
            Mp = 10,
            MaxMp = 100,
            BagItems = []
        };

        var options = new RecoveryOptions(
            AutoUsePean: true,
            MinHpPercent: 50,
            MinMpPercent: 50);

        bool used = await coordinator.TryUsePeanAsync(
            context,
            player,
            options,
            "[Test]",
            null,
            CancellationToken.None);

        Assert.False(used);
        Assert.Empty(context.MockDispatcher.DispatchedCommands);
    }
}
