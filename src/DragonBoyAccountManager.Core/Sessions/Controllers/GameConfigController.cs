using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Sessions.Controllers;

public sealed class GameConfigController(IGameRuntime runtime)
{
    private readonly IGameRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    private GameConfigOptions _currentConfig = new();
    private int _appliedWidth;
    private int _appliedHeight;
    private int _appliedZoom;

    public GameConfigOptions CurrentConfig => _currentConfig;

    public void ApplyConfig(GameConfigOptions config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _currentConfig = config;

        try
        {
            ApplyResolutionAndZoom(config);
            ApplySoundAndGraphic(config);
            ApplyEffectsAndChat(config);
            ApplyAutoPickTab(true);
        }
        catch
        {
        }
    }

    public void OnTick()
    {
        try
        {
            ApplyResolutionAndZoom(_currentConfig);
            ApplySoundAndGraphic(_currentConfig);
            ApplyEffectsAndChat(_currentConfig);
            ApplyAutoPickTab(true);
        }
        catch
        {
        }
    }

    public void ApplyResolutionAndZoom(GameConfigOptions config)
    {
        int w = config.ScreenWidth;
        int h = config.ScreenHeight;
        int zoom = config.ZoomLevel;

        if (w <= 0 || h <= 0)
        {
            if (zoom == 1)
            {
                w = 720;
                h = 320;
            }
            else
            {
                w = 1024;
                h = 600;
            }
        }

        if (zoom <= 0)
        {
            zoom = (w * h < 480000) ? 1 : 2;
        }

        if (w == _appliedWidth && h == _appliedHeight && zoom == _appliedZoom)
        {
            return;
        }

        _appliedWidth = w;
        _appliedHeight = h;
        _appliedZoom = zoom;

        try
        {
            if (_runtime.ModuleBase != IntPtr.Zero)
            {
                IntPtr setResPtr = IntPtr.Add(_runtime.ModuleBase, GameSchema.UnityScreen.RvaSetResolution);
                _runtime.InvokeMethod(setResPtr, IntPtr.Zero, (IntPtr)w, (IntPtr)h, IntPtr.Zero);
            }

            IntPtr scaleGuiStatic = _runtime.GetStaticFieldsPtr(GameSchema.ScaleGUI.ClassName);
            if (scaleGuiStatic != IntPtr.Zero)
            {
                _runtime.WriteField(scaleGuiStatic, 0x4, (float)w);
                _runtime.WriteField(scaleGuiStatic, 0x8, (float)h);
            }

            var graphicsLayout = _runtime.Layout.MGraphics;
            IntPtr graphicsStatic = _runtime.GetStaticFieldsPtr(GameSchema.mGraphics.ClassName);
            if (graphicsStatic != IntPtr.Zero && graphicsLayout.ZoomLevel >= 0)
            {
                _runtime.WriteField(graphicsStatic, graphicsLayout.ZoomLevel, zoom);
            }

            IntPtr motherCanvasStatic = _runtime.GetStaticFieldsPtr(GameSchema.MotherCanvas.ClassName);
            if (motherCanvasStatic != IntPtr.Zero)
            {
                IntPtr motherInstance = _runtime.ReadFieldPtr(motherCanvasStatic, 0x0);
                if (motherInstance != IntPtr.Zero)
                {
                    _runtime.WriteField(motherInstance, 0x18, zoom);
                }
            }

            int canvasW = (w / zoom) + (w % zoom);
            int canvasH = (h / zoom) + (h % zoom);

            IntPtr canvasStatic = _runtime.GetStaticFieldsPtr(GameSchema.GameCanvas.ClassName);
            if (canvasStatic != IntPtr.Zero)
            {
                _runtime.WriteField(canvasStatic, 144, canvasW);
                _runtime.WriteField(canvasStatic, 148, canvasH);
                _runtime.WriteField(canvasStatic, 152, canvasW / 2);
                _runtime.WriteField(canvasStatic, 156, canvasH / 2);
                _runtime.WriteField(canvasStatic, 160, canvasW / 3);
                _runtime.WriteField(canvasStatic, 164, canvasH / 3);
                _runtime.WriteField(canvasStatic, 168, (canvasW * 2) / 3);
                _runtime.WriteField(canvasStatic, 172, (canvasH * 2) / 3);
                _runtime.WriteField(canvasStatic, 176, (canvasW * 3) / 4);
                _runtime.WriteField(canvasStatic, 180, (canvasH * 3) / 4);
                _runtime.WriteField(canvasStatic, 184, canvasW / 6);
                _runtime.WriteField(canvasStatic, 188, canvasH / 6);

                IntPtr canvasInstance = _runtime.ReadFieldPtr(canvasStatic, 24);
                if (canvasInstance != IntPtr.Zero && _runtime.ModuleBase != IntPtr.Zero)
                {
                    IntPtr initCanvasPtr = IntPtr.Add(_runtime.ModuleBase, GameSchema.GameCanvas.RvaInitGameCanvas);
                    _runtime.InvokeMethod(initCanvasPtr, canvasInstance);
                }

                IntPtr serverScreen = _runtime.ReadFieldPtr(canvasStatic, 952);
                if (serverScreen != IntPtr.Zero && _runtime.ModuleBase != IntPtr.Zero)
                {
                    IntPtr initCmdPtr = IntPtr.Add(_runtime.ModuleBase, GameSchema.ServerListScreen.RvaInitCommand);
                    _runtime.InvokeMethod(initCmdPtr, serverScreen);
                }
            }

            IntPtr gameScrStatic = _runtime.GetStaticFieldsPtr(GameSchema.GameScr.ClassName);
            if (gameScrStatic != IntPtr.Zero)
            {
                _runtime.WriteField(gameScrStatic, 32, canvasW);
                _runtime.WriteField(gameScrStatic, 36, canvasH);
                _runtime.WriteField(gameScrStatic, 40, canvasW / 2);
                _runtime.WriteField(gameScrStatic, 72, canvasH / 2);
                _runtime.WriteField(gameScrStatic, 56, canvasW / 3);
                _runtime.WriteField(gameScrStatic, 60, canvasH / 3);
                _runtime.WriteField(gameScrStatic, 68, canvasH - 120);
                _runtime.WriteField(gameScrStatic, 64, (canvasH * 2) / 3);
                _runtime.WriteField(gameScrStatic, 84, (canvasW * 3) / 4);
                _runtime.WriteField(gameScrStatic, 52, (canvasH * 3) / 4);
                _runtime.WriteField(gameScrStatic, 88, canvasW / 6);
                _runtime.WriteField(gameScrStatic, 92, canvasH / 6);
                _runtime.WriteField(gameScrStatic, 1368, canvasW);
                _runtime.WriteField(gameScrStatic, 1364, canvasH - 54);
            }
        }
        catch
        {
        }
    }

    private void ApplySoundAndGraphic(GameConfigOptions config)
    {
        var canvasLayout = _runtime.Layout.GameCanvas;
        IntPtr canvasStatic = _runtime.GetStaticFieldsPtr(GameSchema.GameCanvas.ClassName);
        if (canvasStatic != IntPtr.Zero)
        {
            if (canvasLayout.IsPlaySound >= 0)
            {
                bool curSound = _runtime.ReadField<bool>(canvasStatic, canvasLayout.IsPlaySound);
                if (curSound != config.IsPlaySound)
                {
                    _runtime.WriteField(canvasStatic, canvasLayout.IsPlaySound, config.IsPlaySound);
                }
            }
            if (canvasLayout.LowGraphic >= 0)
            {
                bool curLow = _runtime.ReadField<bool>(canvasStatic, canvasLayout.LowGraphic);
                if (curLow != config.LowGraphic)
                {
                    _runtime.WriteField(canvasStatic, canvasLayout.LowGraphic, config.LowGraphic);
                }
            }
        }
    }

    private void ApplyEffectsAndChat(GameConfigOptions config)
    {
        var charLayout = _runtime.Layout.Char;
        IntPtr charStatic = _runtime.GetStaticFieldsPtr(GameSchema.Char.ClassName);
        if (charStatic != IntPtr.Zero)
        {
            if (charLayout.IsPaintAura >= 0)
            {
                bool curAura = _runtime.ReadField<bool>(charStatic, charLayout.IsPaintAura);
                if (curAura != config.IsPaintAura)
                {
                    _runtime.WriteField(charStatic, charLayout.IsPaintAura, config.IsPaintAura);
                }
            }
            if (charLayout.IsPaintAura2 >= 0)
            {
                bool curAura2 = _runtime.ReadField<bool>(charStatic, charLayout.IsPaintAura2);
                if (curAura2 != config.IsPaintAura2)
                {
                    _runtime.WriteField(charStatic, charLayout.IsPaintAura2, config.IsPaintAura2);
                }
            }
        }

        var gameScrLayout = _runtime.Layout.GameScr;
        if (gameScrLayout.IsPaintChatVip >= 0)
        {
            IntPtr gameScrStatic = _runtime.GetStaticFieldsPtr(GameSchema.GameScr.ClassName);
            if (gameScrStatic != IntPtr.Zero)
            {
                bool curChatVip = _runtime.ReadField<bool>(gameScrStatic, gameScrLayout.IsPaintChatVip);
                if (curChatVip != config.IsPaintChatVip)
                {
                    _runtime.WriteField(gameScrStatic, gameScrLayout.IsPaintChatVip, config.IsPaintChatVip);
                }
            }
        }
    }

    private void ApplyAutoPickTab(bool enable)
    {
        var autoTrainLayout = _runtime.Layout.AutoTrainMenu;
        if (autoTrainLayout.IsNhat >= 0)
        {
            IntPtr autoTrainStatic = _runtime.GetStaticFieldsPtr(GameSchema.AutoTrainMenu.ClassName);
            if (autoTrainStatic != IntPtr.Zero)
            {
                bool isNhat = _runtime.ReadField<bool>(autoTrainStatic, autoTrainLayout.IsNhat);
                if (isNhat != enable)
                {
                    _runtime.WriteField(autoTrainStatic, autoTrainLayout.IsNhat, enable);
                }
            }
        }

        if (enable)
        {
            var mresLayout = _runtime.Layout.MResources;
            if (mresLayout.AutoTrainTabNames >= 0 && mresLayout.AutoTrainTabNames2 >= 0)
            {
                IntPtr mResStatic = _runtime.GetStaticFieldsPtr(GameSchema.mResources.ClassName);
                if (mResStatic != IntPtr.Zero)
                {
                    IntPtr tab3Ptr = _runtime.ReadFieldPtr(mResStatic, mresLayout.AutoTrainTabNames);
                    if (tab3Ptr != IntPtr.Zero)
                    {
                        IntPtr curTab2Ptr = _runtime.ReadFieldPtr(mResStatic, mresLayout.AutoTrainTabNames2);
                        if (curTab2Ptr != tab3Ptr)
                        {
                            _runtime.Memory.Write<IntPtr>(mResStatic + mresLayout.AutoTrainTabNames2, tab3Ptr);
                        }
                    }
                }
            }
        }
    }
}
