using DragonBoyAccountManager.Core.Automation.Behaviors.Navigation;

namespace DragonBoyAccountManager.Core.Tests.Automation;

public class TileAStarFinderTests
{
    [Fact]
    public void FindPath_EmptyMap_ReturnsValidPath()
    {
        int width = 10;
        int height = 10;
        var mapTypes = new int[width * height]; // All T_EMPTY = 0

        int startX = 24; // tile (1, 1)
        int startY = 24;
        int targetX = 96; // tile (4, 1)
        int targetY = 24;

        var path = TileAStarFinder.FindPath(startX, startY, targetX, targetY, width, height, mapTypes);

        Assert.NotEmpty(path);
        var last = path[^1];
        Assert.Equal(targetX, last.X);
        Assert.Equal(targetY, last.Y);
    }

    [Fact]
    public void FindPath_InvalidDimensions_ReturnsEmptyList()
    {
        var path = TileAStarFinder.FindPath(0, 0, 100, 100, 0, 0, []);

        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_WithWallObstacle_PathAvoidsObstacle()
    {
        int width = 10;
        int height = 10;
        var mapTypes = new int[width * height];

        // Place a solid wall at column 3, rows 0 to 4
        for (int y = 0; y <= 4; y++)
        {
            mapTypes[y * width + 3] = TileAStarFinder.T_SOLIDGROUND;
        }

        int startX = 24;  // tile (1, 2)
        int startY = 48;
        int targetX = 120; // tile (5, 2)
        int targetY = 48;

        var path = TileAStarFinder.FindPath(startX, startY, targetX, targetY, width, height, mapTypes);

        Assert.NotEmpty(path);
        // Ensure no point in path lies inside the wall at column 3, row <= 4
        foreach (var (px, py) in path)
        {
            int tx = px / TileAStarFinder.TileSize;
            int ty = py / TileAStarFinder.TileSize;
            if (tx == 3 && ty <= 4)
            {
                Assert.Fail($"Path passed through wall at ({tx}, {ty})");
            }
        }
    }
}
