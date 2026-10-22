using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Engine.Abstractions;

public static class GameEngineExtensions
{
    public static PlayerSnapshot? GetPlayerSnapshot(this IGameActionService engine, Guid accountId)
        => engine.GetSnapshot<PlayerSnapshot>(accountId);

    public static WorldSnapshot? GetWorldSnapshot(this IGameActionService engine, Guid accountId)
        => engine.GetSnapshot<WorldSnapshot>(accountId);

    public static MapSnapshot? GetMapSnapshot(this IGameActionService engine, Guid accountId)
        => engine.GetSnapshot<MapSnapshot>(accountId);

    public static bool ChangeZone(this IGameActionService engine, Guid accountId, int zoneId)
    {
        if (!engine.IsRunning(accountId)) return false;
        _ = engine.ExecuteCommandAsync(accountId, new ChangeZoneCommand(zoneId));
        return true;
    }

    public static void RequestZoneList(this IGameActionService engine, Guid accountId)
    {
        if (engine.IsRunning(accountId))
        {
            _ = engine.ExecuteCommandAsync(accountId, new OpenZonePanelCommand());
        }
    }

    public static void CloseZonePanel(this IGameActionService engine, Guid accountId)
    {
        if (engine.IsRunning(accountId))
        {
            _ = engine.ExecuteCommandAsync(accountId, new CloseZonePanelCommand());
        }
    }

    public static bool MoveToWaypoint(this IGameActionService engine, Guid accountId, int targetX, int targetY)
    {
        if (!engine.IsRunning(accountId)) return false;
        _ = engine.ExecuteCommandAsync(accountId, new MoveCommand(targetX, targetY, false));
        return true;
    }

    public static async Task<Result> FocusEntityAsync(
        this IGameActionService engine,
        Guid accountId,
        IWorldEntity entity,
        bool moveToEntity = true,
        bool isFastTeleport = false,
        CancellationToken cancellationToken = default)
    {
        if (moveToEntity && entity.X > 0 && entity.Y > 0)
        {
            await engine.AutoMoveAsync(accountId, entity.X, entity.Y, isFastTeleport, cancellationToken);
        }

        return await engine.ExecuteCommandAsync(
            accountId,
            new FocusEntityCommand(entity.EntityType, entity.Id, entity.Name),
            cancellationToken);
    }

    public static bool SendChat(this IGameActionService engine, Guid accountId, string text, ChatType type = ChatType.Map)
    {
        if (!engine.IsRunning(accountId)) return false;
        _ = engine.ExecuteCommandAsync(accountId, new SendChatCommand(text, type == ChatType.Global));
        return true;
    }

    public static async Task<bool> SendChatAsync(this IGameActionService engine, Guid accountId, string text, ChatType type = ChatType.Map, CancellationToken cancellationToken = default)
    {
        var res = await engine.ExecuteCommandAsync(accountId, new SendChatCommand(text, type == ChatType.Global), cancellationToken);
        return res.IsSuccess;
    }

    public static Task<Result> UseItemAsync(this IGameActionService engine, Guid accountId, short templateId, string itemName, sbyte bagIndex = -1, CancellationToken cancellationToken = default)
        => engine.ExecuteCommandAsync(accountId, new UseItemCommand(bagIndex, templateId, itemName), cancellationToken);

    public static Task<Result> UseCapsuleAsync(this IGameActionService engine, Guid accountId, CancellationToken cancellationToken = default)
        => engine.UseItemAsync(accountId, GameDefinitions.DefaultCapsuleTemplateId, "Capsule", -1, cancellationToken);

    public static Task<Result> UseTdltAsync(this IGameActionService engine, Guid accountId, CancellationToken cancellationToken = default)
        => engine.UseItemAsync(accountId, GameDefinitions.DefaultTdltTemplateId, "TDLT", -1, cancellationToken);

    public static Task<Result> UsePeanAsync(this IGameActionService engine, Guid accountId, CancellationToken cancellationToken = default)
        => engine.UseItemAsync(accountId, GameDefinitions.DefaultPeanTemplateId, "Đậu thần", -1, cancellationToken);

    public static Task<Result> SelectSkillAsync(this IGameActionService engine, Guid accountId, int slotIndex, CancellationToken cancellationToken = default)
        => engine.ExecuteCommandAsync(accountId, new SelectSkillCommand(slotIndex), cancellationToken);

    public static Task<Result> AutoAssignSkillsAsync(this IGameActionService engine, Guid accountId, CancellationToken cancellationToken = default)
        => engine.ExecuteCommandAsync(accountId, new AssignSkillsCommand(), cancellationToken);

    public static bool StartAutoTrain(this IGameAutomationService engine, Guid accountId, AutoTrainOptions options)
        => engine.StartBehavior<AutoTrainBehavior, AutoTrainOptions>(accountId, options);

    public static bool UpdateAutoTrainOptions(this IGameAutomationService engine, Guid accountId, AutoTrainOptions options)
        => engine.UpdateBehaviorOptions<AutoTrainBehavior, AutoTrainOptions>(accountId, options);

    public static bool StopAutoTrain(this IGameAutomationService engine, Guid accountId)
        => engine.StopBehavior<AutoTrainBehavior>(accountId);

    public static bool IsAutoTrainRunning(this IGameAutomationService engine, Guid accountId)
        => engine.IsBehaviorRunning<AutoTrainBehavior>(accountId);

    public static bool StartAutoChat(this IGameAutomationService engine, Guid accountId, AutoChatOptions options)
        => engine.StartBehavior<AutoChatBehavior, AutoChatOptions>(accountId, options);

    public static bool StartAutoChat(this IGameAutomationService engine, Guid accountId, IReadOnlyList<string> messages, TimeSpan interval, ChatType type = ChatType.Map, bool isRandom = false)
        => engine.StartBehavior<AutoChatBehavior, AutoChatOptions>(accountId, new AutoChatOptions
        {
            Messages = messages,
            Delay = interval,
            IsGlobal = type == ChatType.Global,
            IsRandom = isRandom
        });

    public static bool UpdateAutoChatOptions(this IGameAutomationService engine, Guid accountId, AutoChatOptions options)
        => engine.UpdateBehaviorOptions<AutoChatBehavior, AutoChatOptions>(accountId, options);

    public static bool UpdateAutoChatConfig(this IGameAutomationService engine, Guid accountId, IReadOnlyList<string> messages, TimeSpan interval, ChatType type = ChatType.Map, bool isRandom = false)
        => engine.UpdateBehaviorOptions<AutoChatBehavior, AutoChatOptions>(accountId, new AutoChatOptions
        {
            Messages = messages,
            Delay = interval,
            IsGlobal = type == ChatType.Global,
            IsRandom = isRandom
        });

    public static bool StopAutoChat(this IGameAutomationService engine, Guid accountId)
        => engine.StopBehavior<AutoChatBehavior>(accountId);

    public static bool IsAutoChatRunning(this IGameAutomationService engine, Guid accountId)
        => engine.IsBehaviorRunning<AutoChatBehavior>(accountId);
}
