using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Scrapers;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class WorldInteractionHandler(IGameRuntime runtime) :
    IGameCommandHandler<FocusEntityCommand>,
    IGameCommandHandler<WakeUpCommand>,
    IGameCommandHandler<LoginCommand>,
    IGameCommandHandler<OpenNpcMenuCommand>,
    IGameCommandHandler<ConfirmMenuCommand>
{
    private readonly IGameRuntime _runtime = runtime;

    public Task<Result> HandleAsync(OpenNpcMenuCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodOpenMenu, service, (int)command.NpcTemplateId);
        return Task.FromResult(Result.Success($"Đã mở menu NPC #{command.NpcTemplateId}"));
    }

    public Task<Result> HandleAsync(ConfirmMenuCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodConfirmMenu, service, command.NpcTemplateId, command.SelectIndex);
        return Task.FromResult(Result.Success($"Đã chọn menu #{command.SelectIndex} của NPC #{command.NpcTemplateId}"));
    }

    public Task<Result> HandleAsync(WakeUpCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodWakeUpFromDead, service);
        return Task.FromResult(Result.Success("Đã gửi yêu cầu hồi sinh về nhà."));
    }

    public Task<Result> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodLogin, service,
            command.Username, command.Password, command.Version, (sbyte)0);
        return Task.FromResult(Result.Success($"Đã gửi yêu cầu đăng nhập {command.Username}"));
    }

    public Task<Result> HandleAsync(FocusEntityCommand command, CancellationToken cancellationToken)
    {
        IntPtr charPtr = _runtime.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Nhân vật chưa vào game."));

        var charLayout = _runtime.Layout.Char;
        _runtime.WriteField(charPtr, charLayout.MobFocus, IntPtr.Zero);
        _runtime.WriteField(charPtr, charLayout.NpcFocus, IntPtr.Zero);
        _runtime.WriteField(charPtr, charLayout.CharFocus, IntPtr.Zero);
        _runtime.WriteField(charPtr, charLayout.ItemFocus, IntPtr.Zero);

        string type = command.EntityType.Trim().ToLowerInvariant();
        IntPtr targetPtr = IntPtr.Zero;
        int focusOffset = -1;

        switch (type)
        {
            case "mob":
                targetPtr = FindEntityInVector(_runtime.Layout.GameScr.VMob, _runtime.Layout.Mob.MobId, command.EntityId);
                focusOffset = charLayout.MobFocus;
                break;

            case "npc":
                targetPtr = FindEntityInVector(_runtime.Layout.GameScr.VNpc, _runtime.Layout.Npc.NpcId, command.EntityId);
                focusOffset = charLayout.NpcFocus;
                break;

            case "boss":
            case "player":
                targetPtr = FindEntityInVector(_runtime.Layout.GameScr.VCharInMap, charLayout.CharId, command.EntityId);
                focusOffset = charLayout.CharFocus;
                break;

            case "item":
                targetPtr = FindItemInVector(command.EntityId);
                focusOffset = charLayout.ItemFocus;
                break;
        }

        if (targetPtr == IntPtr.Zero || focusOffset < 0)
        {
            return Task.FromResult(Result.Failure($"Không tìm thấy đối tượng {command.Name}."));
        }

        _runtime.WriteField(charPtr, focusOffset, targetPtr);
        _runtime.WriteField(charPtr, charLayout.IsManualFocus, true);

        if (type == "npc")
        {
            IntPtr gameScr = _runtime.GetGameScrPtr();
            if (gameScr != IntPtr.Zero)
            {
                _runtime.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodDoDoubleClickToObj, gameScr, targetPtr);
            }
        }

        return Task.FromResult(Result.Success($"Đã chọn {command.Name}"));
    }

    private IntPtr FindEntityInVector(int staticVectorOffset, int idFieldOffset, int targetId)
    {
        IntPtr vectorPtr = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, staticVectorOffset);
        if (vectorPtr == IntPtr.Zero) return IntPtr.Zero;

        foreach (var ptr in CollectionHelper.ReadMyVector(_runtime, vectorPtr))
        {
            if (ptr != IntPtr.Zero && _runtime.ReadField<int>(ptr, idFieldOffset) == targetId)
            {
                return ptr;
            }
        }
        return IntPtr.Zero;
    }

    private IntPtr FindItemInVector(int targetId)
    {
        IntPtr vectorPtr = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.VItemMap);
        if (vectorPtr == IntPtr.Zero) return IntPtr.Zero;

        var itemMapLayout = _runtime.Layout.ItemMap;
        foreach (var ptr in CollectionHelper.ReadMyVector(_runtime, vectorPtr))
        {
            if (ptr != IntPtr.Zero && _runtime.ReadField<short>(ptr, itemMapLayout.ItemMapId) == targetId)
            {
                return ptr;
            }
        }
        return IntPtr.Zero;
    }
}
