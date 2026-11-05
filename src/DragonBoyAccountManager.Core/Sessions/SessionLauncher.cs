using System.Diagnostics;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Interop;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Interop.Native;
using DragonBoyAccountManager.Core.Sessions.Validation;

namespace DragonBoyAccountManager.Core.Sessions;

public class SessionLauncher
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
}
