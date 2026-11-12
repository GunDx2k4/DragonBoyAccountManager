using System.Diagnostics;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Il2Cpp;
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
            gameRuntime = await Il2CppRuntime.AttachAsync(process, cancellationToken: cancellationToken)
                          ?? throw new InvalidOperationException("Failed to attach game runtime.");

            if (options.ScreenWidth > 0 && options.ScreenHeight > 0)
            {
                ApplyCustomResolutionPatch(gameRuntime, options.ScreenWidth, options.ScreenHeight);
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
}
