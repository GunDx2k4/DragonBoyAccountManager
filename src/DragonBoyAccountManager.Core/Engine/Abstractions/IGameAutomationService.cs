using DragonBoyAccountManager.Core.Automation.Behaviors;

namespace DragonBoyAccountManager.Core.Engine.Abstractions;

public interface IGameAutomationService
{
    bool StartBehavior<TBehavior, TOptions>(Guid accountId, TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class;
    bool UpdateBehaviorOptions<TBehavior, TOptions>(Guid accountId, TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class;
    bool StopBehavior<TBehavior>(Guid accountId) where TBehavior : class, IBehavior;
    bool IsBehaviorRunning<TBehavior>(Guid accountId) where TBehavior : class, IBehavior;
}
