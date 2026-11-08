using DragonBoyAccountManager.Core.Engine.Abstractions;

namespace DragonBoyAccountManager.GUI.Services.Engine;

public interface IGameEngineService :
    ISessionManagementService,
    IGameWindowControlService,
    IGameActionService,
    IGameAutomationService,
    IGameEngineEvents
{
}
