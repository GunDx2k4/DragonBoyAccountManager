using DragonBoyAccountManager.Core.Automation.Coordinators;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.Core.Tests.Mocks;

namespace DragonBoyAccountManager.Core.Tests.Coordinators;

public class GroundLootCoordinatorTests
{
    [Fact]
    public async Task TryPickGroundItemsAsync_WhenItemInPickRange_DispatchesPickItemCommand()
    {
        var coordinator = new GroundLootCoordinator();
        var context = new MockSessionContext();

        var player = PlayerSnapshot.Empty with
        {
            Id = 1,
            X = 100,
            Y = 200,
            IsInGame = true,
            IsAlive = true
        };

        var world = WorldSnapshot.Empty with
        {
            GroundItems =
            [
                new GroundItemSnapshot(ItemMapId: 101, TemplateId: 190, Name: "Vàng", X: 110, Y: 200, PlayerId: 1)
            ]
        };

        var options = new LootFilterOptions(
            AutoPick: true,
            PickOthersItems: true,
            PickAllMap: false,
            IsFastTeleport: false);

        bool handled = await coordinator.TryPickGroundItemsAsync(
            context,
            player,
            world,
            options,
            "[Test]",
            null,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Single(context.MockDispatcher.DispatchedCommands);
        var cmd = Assert.IsType<PickItemCommand>(context.MockDispatcher.DispatchedCommands[0]);
        Assert.Equal(101, cmd.ItemMapId);
        Assert.Equal("Vàng", cmd.ItemName);
    }

    [Fact]
    public async Task TryPickGroundItemsAsync_WhenItemFar_DispatchesNavigateCommand()
    {
        var coordinator = new GroundLootCoordinator();
        var context = new MockSessionContext();

        var player = PlayerSnapshot.Empty with
        {
            Id = 1,
            X = 100,
            Y = 200,
            IsInGame = true,
            IsAlive = true
        };

        var world = WorldSnapshot.Empty with
        {
            GroundItems =
            [
                new GroundItemSnapshot(ItemMapId: 102, TemplateId: 190, Name: "Vàng", X: 180, Y: 200, PlayerId: 1)
            ]
        };

        var options = new LootFilterOptions(
            AutoPick: true,
            PickOthersItems: true,
            PickAllMap: false,
            IsFastTeleport: false);

        bool handled = await coordinator.TryPickGroundItemsAsync(
            context,
            player,
            world,
            options,
            "[Test]",
            null,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Single(context.MockDispatcher.DispatchedCommands);
        var cmd = Assert.IsType<NavigateCommand>(context.MockDispatcher.DispatchedCommands[0]);
        Assert.Equal(180, cmd.TargetX);
        Assert.Equal(200, cmd.TargetY);
    }

    [Fact]
    public async Task TryPickGroundItemsAsync_WhenItemExcluded_IgnoresItem()
    {
        var coordinator = new GroundLootCoordinator();
        var context = new MockSessionContext();

        var player = PlayerSnapshot.Empty with { Id = 1, X = 100, Y = 200 };
        var world = WorldSnapshot.Empty with
        {
            GroundItems =
            [
                new GroundItemSnapshot(ItemMapId: 103, TemplateId: 190, Name: "Vàng", X: 105, Y: 200, PlayerId: 1)
            ]
        };

        var options = new LootFilterOptions(
            AutoPick: true,
            PickOthersItems: true,
            PickAllMap: false,
            IsFastTeleport: false,
            ExcludeIds: new HashSet<short> { 190 });

        bool handled = await coordinator.TryPickGroundItemsAsync(
            context,
            player,
            world,
            options,
            "[Test]",
            null,
            CancellationToken.None);

        Assert.False(handled);
        Assert.Empty(context.MockDispatcher.DispatchedCommands);
    }
}
