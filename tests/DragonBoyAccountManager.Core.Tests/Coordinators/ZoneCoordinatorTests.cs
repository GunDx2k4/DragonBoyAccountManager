using DragonBoyAccountManager.Core.Automation.Coordinators;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.Core.Tests.Mocks;

namespace DragonBoyAccountManager.Core.Tests.Coordinators;

public class ZoneCoordinatorTests
{
    [Fact]
    public async Task TryHopZoneAsync_WhenDisabled_ReturnsFalse()
    {
        var coordinator = new ZoneCoordinator();
        var context = new MockSessionContext();

        var options = new ZoneHopOptions(
            ChangeZoneWhenNoMobs: false,
            CheckAllMobs: true,
            TargetMobNames: [],
            CheckAttackByHp: false,
            MinMobHp: 0,
            MaxMobHp: 0);

        bool hopped = await coordinator.TryHopZoneAsync(
            context,
            WorldSnapshot.Empty,
            options,
            "[Test]",
            null,
            CancellationToken.None);

        Assert.False(hopped);
        Assert.Empty(context.MockDispatcher.DispatchedCommands);
    }
}
