using System;
using System.Threading.Tasks;
using DragonBoyAccountManager.Core.Sessions.Validation;

namespace DragonBoyAccountManager.GUI.Infrastructure.Engine;

public interface IGameEnvironmentService
{
    string GamePath { get; set; }
    ClientStatus Status { get; }
    string AssemblyStatus { get; }
    string AssemblyStatusTooltip { get; }
    string AssemblyPath { get; }
    string GameVersion { get; }
    bool IsAssemblyMissing { get; }
    ClientMetadata? CurrentMetadata { get; }

    event Action? EnvironmentChanged;

    void CheckStatus();
    Task LoadChecksumsAndVerifyAsync();
}
