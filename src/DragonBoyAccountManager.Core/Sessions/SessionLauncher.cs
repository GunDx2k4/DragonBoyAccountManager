using System.Diagnostics;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Interop;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Interop.Native;
using DragonBoyAccountManager.Core.Sessions.Validation;

namespace DragonBoyAccountManager.Core.Sessions;

public class SessionLauncher : ISessionLauncher
{
    public static ResolvedGamePaths Validate(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            throw new ArgumentException("Game path is empty.");

        if (!File.Exists(gamePath))
            throw new ArgumentException($"File not found: \"{gamePath}\".");

        if (!Path.GetExtension(gamePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Not an executable (.exe).");

        var paths = GameClientValidator.ResolveGamePathsStatic(gamePath);

        if (paths == null)
        {
            throw new ArgumentException($"Tệp thực thi không hợp lệ: \"{gamePath}\".");
        }

        return paths;
    }

    public async Task<GameSession> LaunchAsync(LaunchOptions options, CancellationToken cancellationToken = default)
    {
        Validate(options.GamePath);

        string workingDir = options.WorkingDirectory ?? Path.GetDirectoryName(options.GamePath)!;
        var argsList = new List<string>
        {
            "-screen-fullscreen 0"
        };

        if (options.ScreenWidth > 0 && options.ScreenHeight > 0)
        {
            argsList.Add($"-screen-width {options.ScreenWidth}");
            argsList.Add($"-screen-height {options.ScreenHeight}");
        }

        if (!string.IsNullOrWhiteSpace(options.AdditionalArguments))
        {
            argsList.Add(options.AdditionalArguments.Trim());
        }

        if (options.EnableLogging && !string.IsNullOrEmpty(options.LogPrefix))
        {
            var logsDir = Path.Combine(workingDir, "Logs");
            Directory.CreateDirectory(logsDir);

            string safeName = string.Join("_", options.LogPrefix.Split(Path.GetInvalidFileNameChars()));
            string logFile = Path.Combine(logsDir, $"log_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            argsList.Add($"-logfile \"{logFile}\"");
        }

        string arguments = string.Join(" ", argsList);

        var startInfo = new ProcessStartInfo
        {
            FileName = options.GamePath,
            WorkingDirectory = workingDir,
            Arguments = arguments,
            UseShellExecute = true
        };

        var process = Process.Start(startInfo)
                      ?? throw new InvalidOperationException("Failed to start game process.");

        WindowsJobManager.AddProcess(process);

        IGameRuntime? gameRuntime = null;
        try
        {
            gameRuntime = await GameRuntimeFactory.AttachAsync(process, cancellationToken: cancellationToken)
                          ?? throw new InvalidOperationException("Failed to attach game runtime.");

            if (options.ScreenWidth > 0 && options.ScreenHeight > 0)
            {
                ApplyCustomResolutionPatch(gameRuntime, options.ScreenWidth, options.ScreenHeight);
                ApplyResolutionInRam(gameRuntime, options.ScreenWidth, options.ScreenHeight, options.ZoomLevel);
            }

            if (options.ServerId.HasValue && gameRuntime.Layout.ServerListScreen.IpSelect >= 0)
            {
                try
                {
                    gameRuntime.WriteStatic(GameSchema.ServerListScreen.ClassName, gameRuntime.Layout.ServerListScreen.IpSelect, options.ServerId.Value);
                }
                catch { }
            }

            return new GameSession(process, gameRuntime, options.SessionId ?? Guid.NewGuid(), options.ConfigureSession);
        }
        catch
        {
            gameRuntime?.Dispose();
            if (!process.HasExited)
            {
                try { process.Kill(); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
            throw;
        }
    }

    private static void ApplyCustomResolutionPatch(IGameRuntime runtime, int width, int height)
    {
        if (runtime.ModuleBase == IntPtr.Zero || width <= 0 || height <= 0) return;

        try
        {
            IntPtr baseAddr = runtime.ModuleBase;
            IntPtr branch0 = IntPtr.Add(baseAddr, GameSchema.Main.RvaResolutionBranchDefault);
            IntPtr branch1 = IntPtr.Add(baseAddr, GameSchema.Main.RvaResolutionBranchSmall);

            byte b0_edx = runtime.Memory.ReadByte(branch0);
            byte b0_ecx = runtime.Memory.ReadByte(IntPtr.Add(branch0, 5));
            if (b0_edx == 0xBA && b0_ecx == 0xB9)
            {
                if (NativeMethods.VirtualProtectEx(runtime.ProcessHandle, branch0, 10, MemoryProtection.ExecuteReadWrite, out var oldProtect0))
                {
                    try
                    {
                        runtime.Memory.Write(IntPtr.Add(branch0, 1), height);
                        runtime.Memory.Write(IntPtr.Add(branch0, 6), width);
                    }
                    finally
                    {
                        NativeMethods.VirtualProtectEx(runtime.ProcessHandle, branch0, 10, oldProtect0, out _);
                    }
                }
            }

            byte b1_edx = runtime.Memory.ReadByte(branch1);
            byte b1_ecx = runtime.Memory.ReadByte(IntPtr.Add(branch1, 5));
            if (b1_edx == 0xBA && b1_ecx == 0xB9)
            {
                if (NativeMethods.VirtualProtectEx(runtime.ProcessHandle, branch1, 10, MemoryProtection.ExecuteReadWrite, out var oldProtect1))
                {
                    try
                    {
                        runtime.Memory.Write(IntPtr.Add(branch1, 1), height);
                        runtime.Memory.Write(IntPtr.Add(branch1, 6), width);
                    }
                    finally
                    {
                        NativeMethods.VirtualProtectEx(runtime.ProcessHandle, branch1, 10, oldProtect1, out _);
                    }
                }
            }
        }
        catch
        {
        }
    }

    private static void ApplyResolutionInRam(IGameRuntime runtime, int width, int height, int zoomLevel)
    {
        if (runtime.ModuleBase == IntPtr.Zero || width <= 0 || height <= 0) return;

        try
        {
            if (zoomLevel <= 0)
            {
                zoomLevel = (width * height < 480000) ? 1 : 2;
            }

            IntPtr setResPtr = IntPtr.Add(runtime.ModuleBase, GameSchema.UnityScreen.RvaSetResolution);
            runtime.InvokeMethod(setResPtr, IntPtr.Zero, (IntPtr)width, (IntPtr)height, IntPtr.Zero);

            IntPtr scaleGuiStatic = runtime.GetStaticFieldsPtr(GameSchema.ScaleGUI.ClassName);
            if (scaleGuiStatic != IntPtr.Zero)
            {
                runtime.WriteField(scaleGuiStatic, 0x4, (float)width);
                runtime.WriteField(scaleGuiStatic, 0x8, (float)height);
            }

            IntPtr graphicsStatic = runtime.GetStaticFieldsPtr(GameSchema.mGraphics.ClassName);
            if (graphicsStatic != IntPtr.Zero)
            {
                runtime.WriteField(graphicsStatic, 0x18, zoomLevel);
            }

            IntPtr motherCanvasStatic = runtime.GetStaticFieldsPtr(GameSchema.MotherCanvas.ClassName);
            if (motherCanvasStatic != IntPtr.Zero)
            {
                IntPtr motherInstance = runtime.ReadFieldPtr(motherCanvasStatic, 0x0);
                if (motherInstance != IntPtr.Zero)
                {
                    runtime.WriteField(motherInstance, 0x18, zoomLevel);
                }
            }

            int canvasW = (width / zoomLevel) + (width % zoomLevel);
            int canvasH = (height / zoomLevel) + (height % zoomLevel);

            IntPtr canvasStatic = runtime.GetStaticFieldsPtr(GameSchema.GameCanvas.ClassName);
            if (canvasStatic != IntPtr.Zero)
            {
                runtime.WriteField(canvasStatic, 144, canvasW);
                runtime.WriteField(canvasStatic, 148, canvasH);
                runtime.WriteField(canvasStatic, 152, canvasW / 2);
                runtime.WriteField(canvasStatic, 156, canvasH / 2);
                runtime.WriteField(canvasStatic, 160, canvasW / 3);
                runtime.WriteField(canvasStatic, 164, canvasH / 3);
                runtime.WriteField(canvasStatic, 168, (canvasW * 2) / 3);
                runtime.WriteField(canvasStatic, 172, (canvasH * 2) / 3);
                runtime.WriteField(canvasStatic, 176, (canvasW * 3) / 4);
                runtime.WriteField(canvasStatic, 180, (canvasH * 3) / 4);
                runtime.WriteField(canvasStatic, 184, canvasW / 6);
                runtime.WriteField(canvasStatic, 188, canvasH / 6);

                IntPtr canvasInstance = runtime.ReadFieldPtr(canvasStatic, 24);
                if (canvasInstance != IntPtr.Zero)
                {
                    IntPtr initCanvasPtr = IntPtr.Add(runtime.ModuleBase, GameSchema.GameCanvas.RvaInitGameCanvas);
                    runtime.InvokeMethod(initCanvasPtr, canvasInstance);
                }

                IntPtr serverScreen = runtime.ReadFieldPtr(canvasStatic, 952);
                if (serverScreen != IntPtr.Zero)
                {
                    IntPtr initCmdPtr = IntPtr.Add(runtime.ModuleBase, GameSchema.ServerListScreen.RvaInitCommand);
                    runtime.InvokeMethod(initCmdPtr, serverScreen);
                }
            }
        }
        catch
        {
        }
    }
}
