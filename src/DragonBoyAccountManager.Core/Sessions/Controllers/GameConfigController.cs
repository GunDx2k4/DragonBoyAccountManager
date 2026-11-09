using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Sessions.Controllers;

public sealed class GameConfigController(IGameRuntime runtime)
{
    private readonly IGameRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    private GameConfigOptions _currentConfig = new();

    public GameConfigOptions CurrentConfig => _currentConfig;

    public void ApplyConfig(GameConfigOptions config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _currentConfig = config;

        try
        {
            ApplySoundAndGraphic(config);
            ApplyEffectsAndChat(config);
            ApplyAutoPickTab(config.EnablePickTab);
        }
        catch
        {
        }
    }

    public void OnTick()
    {
        if (_currentConfig.EnablePickTab)
        {
            try
            {
                ApplyAutoPickTab(true);
            }
            catch
            {
            }
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
                _runtime.WriteField(canvasStatic, canvasLayout.IsPlaySound, config.IsPlaySound);
            }
            if (canvasLayout.LowGraphic >= 0)
            {
                _runtime.WriteField(canvasStatic, canvasLayout.LowGraphic, config.LowGraphic);
            }
        }

        var graphicsLayout = _runtime.Layout.MGraphics;
        if (graphicsLayout.ZoomLevel >= 0)
        {
            IntPtr graphicsStatic = _runtime.GetStaticFieldsPtr(GameSchema.mGraphics.ClassName);
            if (graphicsStatic != IntPtr.Zero)
            {
                _runtime.WriteField(graphicsStatic, graphicsLayout.ZoomLevel, config.ZoomLevel);
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
                _runtime.WriteField(charStatic, charLayout.IsPaintAura, config.IsPaintAura);
            }
            if (charLayout.IsPaintAura2 >= 0)
            {
                _runtime.WriteField(charStatic, charLayout.IsPaintAura2, config.IsPaintAura2);
            }
        }

        var gameScrLayout = _runtime.Layout.GameScr;
        if (gameScrLayout.IsPaintChatVip >= 0)
        {
            IntPtr gameScrStatic = _runtime.GetStaticFieldsPtr(GameSchema.GameScr.ClassName);
            if (gameScrStatic != IntPtr.Zero)
            {
                _runtime.WriteField(gameScrStatic, gameScrLayout.IsPaintChatVip, config.IsPaintChatVip);
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
