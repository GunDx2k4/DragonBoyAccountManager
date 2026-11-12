using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Automation.Behaviors.Social;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Execution.Handlers;
using DragonBoyAccountManager.Core.Telemetry.Scrapers;

namespace DragonBoyAccountManager.Core.Sessions;

public static class DefaultSessionBootstrap
{
    public static void Configure(GameSession session)
    {
        var runtime = session.Runtime;
        var dispatcher = session.Dispatcher;

        session.RegisterScraper(new PlayerScraper(runtime, () => session.MaskingController.OriginalCharacterName));
        session.RegisterScraper(new WorldScraper(runtime));
        session.RegisterScraper(new MapScraper(runtime));

        var combatHandler = new CombatHandler(runtime);
        var movementHandler = new MovementHandler(runtime, session.Navigator, session);
        var inventoryHandler = new InventoryHandler(runtime);
        var zoneHandler = new ZoneHandler(runtime);
        var chatHandler = new ChatHandler(runtime);
        var worldInteractionHandler = new WorldInteractionHandler(runtime);
        var skillAssignmentHandler = new SkillAssignmentHandler(runtime);
        var gameConfigHandler = new GameConfigHandler(session.ConfigController);

        dispatcher.RegisterHandler<AttackMobCommand>(combatHandler);
        dispatcher.RegisterHandler<SelectSkillCommand>(combatHandler);
        dispatcher.RegisterHandler<SetInGameAutoTrainCommand>(combatHandler);
        dispatcher.RegisterHandler<MoveCommand>(movementHandler);
        dispatcher.RegisterHandler<NavigateCommand>(movementHandler);
        dispatcher.RegisterHandler<EnterWaypointCommand>(movementHandler);
        dispatcher.RegisterHandler<XmapMoveCommand>(movementHandler);
        dispatcher.RegisterHandler<UseItemCommand>(inventoryHandler);
        dispatcher.RegisterHandler<PickItemCommand>(inventoryHandler);
        dispatcher.RegisterHandler<SendChatCommand>(chatHandler);
        dispatcher.RegisterHandler<WakeUpCommand>(worldInteractionHandler);
        dispatcher.RegisterHandler<ChangeZoneCommand>(zoneHandler);
        dispatcher.RegisterHandler<OpenZonePanelCommand>(zoneHandler);
        dispatcher.RegisterHandler<CloseZonePanelCommand>(zoneHandler);
        dispatcher.RegisterHandler<LoginCommand>(worldInteractionHandler);
        dispatcher.RegisterHandler<FocusEntityCommand>(worldInteractionHandler);
        dispatcher.RegisterHandler<OpenNpcMenuCommand>(worldInteractionHandler);
        dispatcher.RegisterHandler<ConfirmMenuCommand>(worldInteractionHandler);
        dispatcher.RegisterHandler<AssignSkillsCommand>(skillAssignmentHandler);
        dispatcher.RegisterHandler<SetGameConfigCommand>(gameConfigHandler);

        session.RegisterBehavior(new AutoTrainBehavior(new AutoTrainOptions()));
        session.RegisterBehavior(new AutoChatBehavior(new AutoChatOptions()));
    }
}
