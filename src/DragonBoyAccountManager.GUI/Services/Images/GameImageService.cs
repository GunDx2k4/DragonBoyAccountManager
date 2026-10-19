using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DragonBoyAccountManager.GUI.Services.Images;

public sealed class GameImageService : IGameImageService
{
    private readonly string _rmsDir;
    private readonly ConcurrentDictionary<int, ImageSource> _cache = new();
    private readonly ConcurrentDictionary<byte, BitmapSource> _bigBitmaps = new();
    private readonly Dictionary<int, AtlasRegion> _atlas = [];

    private readonly record struct AtlasRegion(byte BigId, short X, short Y, short W, short H);

    public GameImageService()
    {
        _rmsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Team", "DragonBoy250");
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
                _atlas[i] = new AtlasRegion(bigId, x, y, w, h);
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

        if (_atlas.TryGetValue(iconId, out var region))
        {
            var bigBmp = GetBigBitmap(region.BigId);
            if (bigBmp != null &&
                region.X >= 0 && region.Y >= 0 &&
                region.W > 0 && region.H > 0 &&
                region.X + region.W <= bigBmp.PixelWidth &&
                region.Y + region.H <= bigBmp.PixelHeight)
            {
                try
                {
                    var rect = new Int32Rect(region.X, region.Y, region.W, region.H);
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
