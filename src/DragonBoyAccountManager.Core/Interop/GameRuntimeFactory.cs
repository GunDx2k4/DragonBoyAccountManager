using System.Diagnostics;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Il2Cpp;

namespace DragonBoyAccountManager.Core.Interop;

public static class GameRuntimeFactory
{
    public static async Task<IGameRuntime?> AttachAsync(
        Process? process,
        string? targetVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (process == null || process.HasExited)
            return null;

        return await Il2CppRuntime.AttachAsync(process, targetVersion, cancellationToken);
    }
}
