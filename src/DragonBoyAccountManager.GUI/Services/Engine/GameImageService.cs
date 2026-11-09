using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DragonBoyAccountManager.GUI.Services.Engine;

public sealed class GameImageService : IGameImageService
{
    private readonly string _rmsDir;
    private readonly ConcurrentDictionary<int, ImageSource> _cache = new();
    private readonly ConcurrentDictionary<byte, BitmapSource> _bigBitmaps = new();
    private readonly ConcurrentDictionary<int, AtlasRegion> _atlas = new();

    private readonly record struct AtlasRegion(byte BigId, short X, short Y, short W, short H);

    public GameImageService()
    {
        _rmsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Team", "Dragonboy252");
        LoadAtlas();
    }

    private void LoadAtlas()
    {
        if (!Directory.Exists(_rmsDir)) return;

        string nrImagePath = Path.Combine(_rmsDir, "NR_image");
        if (!File.Exists(nrImagePath)) return;

        try
        {
            byte[] data = File.ReadAllBytes(nrImagePath);
            if (data.Length < 2) return;

            int offset = 0;
            short count = (short)((data[offset++] << 8) | data[offset++]);
            for (int i = 0; i < count && offset + 9 <= data.Length; i++)
            {
                byte bigId = data[offset++];
                short x = (short)((data[offset++] << 8) | data[offset++]);
                short y = (short)((data[offset++] << 8) | data[offset++]);
                short w = (short)((data[offset++] << 8) | data[offset++]);
                short h = (short)((data[offset++] << 8) | data[offset++]);
                if (x < 256 && y < 256 && w < 256 && h < 256)
                {
                    _atlas[i] = new AtlasRegion(bigId, x, y, w, h);
                }
            }
        }
        catch
        {
        }
    }

    public ImageSource? GetSmallImage(int iconId)
    {
        if (iconId < 0) return null;

        if (_cache.TryGetValue(iconId, out var cached))
        {
            return cached;
        }

        if (_atlas.Count == 0)
        {
            LoadAtlas();
        }

        if (_atlas.TryGetValue(iconId, out var region))
        {
            var bigBmp = GetBigBitmap(region.BigId);
            if (bigBmp != null)
            {
                int x = region.X * 2;
                int y = region.Y * 2;
                int w = region.W * 2;
                int h = region.H * 2;

                if (x >= 0 && y >= 0 && w > 0 && h > 0 &&
                    x + w <= bigBmp.PixelWidth &&
                    y + h <= bigBmp.PixelHeight)
                {
                    try
                    {
                        var rect = new Int32Rect(x, y, w, h);
                        var cropped = new CroppedBitmap(bigBmp, rect);
                        cropped.Freeze();
                        _cache[iconId] = cropped;
                        return cropped;
                    }
                    catch
                    {
                    }
                }
            }
        }

        if (Directory.Exists(_rmsDir))
        {
            string singlePath = Path.Combine(_rmsDir, $"2Small{iconId}");
            if (File.Exists(singlePath))
            {
                try
                {
                    using var stream = new MemoryStream(File.ReadAllBytes(singlePath));
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = stream;
                    bmp.EndInit();
                    bmp.Freeze();
                    _cache[iconId] = bmp;
                    return bmp;
                }
                catch
                {
                }
            }
        }

        return null;
    }

    private BitmapSource? GetBigBitmap(byte bigId)
    {
        if (_bigBitmaps.TryGetValue(bigId, out var cached))
        {
            return cached;
        }

        string bigPath = Path.Combine(_rmsDir, $"x2Big{bigId}");
        if (!File.Exists(bigPath)) return null;

        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(bigPath));
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = stream;
            bmp.EndInit();
            bmp.Freeze();
            _bigBitmaps[bigId] = bmp;
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
