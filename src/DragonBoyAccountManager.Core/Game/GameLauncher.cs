using System.Diagnostics;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Game.Schema;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameLauncher
{
    public static ResolvedGamePaths Validate(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            throw new ArgumentException("Game path is empty.");

        if (!File.Exists(gamePath))
            throw new ArgumentException($"File not found: \"{gamePath}\".");

        if (!Path.GetExtension(gamePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Not an executable (.exe).");

        using var validator = new GameClientValidator();
        var paths = validator.ResolveGamePaths(gamePath);

        if (paths == null)
        {
            string exeName = Path.GetFileNameWithoutExtension(gamePath);
            throw new ArgumentException($"Missing Unity data folder or \"{Constants.Client.AssemblyName}\" under \"{exeName}_Data\".");
        }

        return paths;
    }

    public async Task<GameInstance> LaunchAsync(LaunchOptions options, CancellationToken cancellationToken = default)
    {
        Validate(options.GamePath);

        string workingDir = options.WorkingDirectory ?? Path.GetDirectoryName(options.GamePath)!;
        var argsList = new List<string>();

        if (options.ScreenWidth > 0 && options.ScreenHeight > 0)
        {
            argsList.Add($"-screen-width {options.ScreenWidth}");
            argsList.Add($"-screen-height {options.ScreenHeight}");
            argsList.Add("-screen-fullscreen 0");
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

        try
        {
            var monoContext = await MonoContext.CreateAsync(process, cancellationToken)
                              ?? throw new InvalidOperationException("Failed to attach Mono runtime.");

            var instance = new GameInstance(process, monoContext, options.SessionId ?? Guid.NewGuid());

            await instance.Reflector.WaitForAssemblyAsync("Assembly-CSharp", 15000, cancellationToken);
            await Task.Delay(500, cancellationToken);

            try
            {
                instance.Reflector.CallUnityMethod("UnityEngine", "Application", "set_runInBackground", IntPtr.Zero, true);
                instance.Reflector.WriteStatic(GameSchema.Main.ClassName, GameSchema.Main.StaticIsQuitApp, false);
            }
            catch { }

            return instance;
        }
        catch
        {
            if (!process.HasExited)
            {
                try { process.Kill(); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }

            process.Dispose();
            throw;
        }
    }
}
