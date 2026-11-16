using DragonBoyAccountManager.Core.Automation.Navigation;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.Core.Tests.Mocks;

namespace DragonBoyAccountManager.Core.Tests.Navigation;

public class AutoNavigatorTests
{
    [Fact]
    public void FindPath_WhenValidOpenSpace_ReturnsPath()
    {
        int width = 10;
        int height = 10;
        int[] tileTypes = new int[width * height];
        for (int x = 0; x < width; x++)
        {
            tileTypes[5 * width + x] = 2;
        }

        var map = new MapSnapshot(1, 0, "Làng Aru", width, height, tileTypes);

        var path = AutoNavigator.FindPath(map, 48, 96, 144, 96);

        Assert.NotNull(path);
        Assert.NotEmpty(path);
    }

    [Fact]
    public async Task NavigateAsync_WhenAlreadyAtTarget_ReturnsSuccessWithoutMoving()
    {
        var dispatcher = new MockCommandDispatcher();
        var navigator = new AutoNavigator(dispatcher);

        var player = PlayerSnapshot.Empty with
        {
            Id = 1,
            X = 100,
            Y = 100,
            IsInGame = true,
            IsAlive = true
        };

        var map = MapSnapshot.Empty;

        var result = await navigator.NavigateAsync(player, map, 105, 105);

        Assert.True(result.IsSuccess);
        Assert.Empty(dispatcher.DispatchedCommands);
    }
}
