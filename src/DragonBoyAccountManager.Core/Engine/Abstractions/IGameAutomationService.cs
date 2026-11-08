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
    TBehavior? GetBehavior<TBehavior>(Guid accountId) where TBehavior : class, IBehavior;

    bool StartBehavior(Guid accountId, string behaviorId);
    bool StopBehavior(Guid accountId, string behaviorId);
    bool IsBehaviorRunning(Guid accountId, string behaviorId);
    IReadOnlyCollection<IBehavior> GetBehaviors(Guid accountId);
}
