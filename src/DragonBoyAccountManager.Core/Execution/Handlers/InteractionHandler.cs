using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;
using DragonBoyAccountManager.Core.Telemetry.Scrapers;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class InteractionHandler(IMonoRuntime mono) :
    IGameCommandHandler<UseItemCommand>,
    IGameCommandHandler<PickItemCommand>,
    IGameCommandHandler<SendChatCommand>,
    IGameCommandHandler<WakeUpCommand>,
    IGameCommandHandler<ChangeZoneCommand>,
    IGameCommandHandler<OpenZonePanelCommand>,
    IGameCommandHandler<CloseZonePanelCommand>,
    IGameCommandHandler<LoginCommand>,
    IGameCommandHandler<FocusEntityCommand>
{
    private readonly IMonoRuntime _mono = mono;

    public Task<Result> HandleAsync(UseItemCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _mono.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        sbyte targetBagIndex = command.BagIndex;
        short targetTemplateId = command.TemplateId;
        string resolvedName = command.ItemName;

        if (targetBagIndex < 0)
        {
            IntPtr charPtr = _mono.GetMyCharPtr();
            IntPtr arrBagPtr = charPtr != IntPtr.Zero
                ? _mono.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldArrItemBag, charPtr)
                : IntPtr.Zero;

            if (arrBagPtr != IntPtr.Zero)
            {
                if (!TryResolveBagItem(arrBagPtr, command.TemplateId, out sbyte foundIndex, out short foundTemplateId, out sbyte foundType, out string foundName))
                {
                    return Task.FromResult(Result.Failure($"Không tìm thấy {command.ItemName} trong hành trang."));
                }

                if (!string.IsNullOrWhiteSpace(foundName))
                {
                    resolvedName = foundName;
                }

                if (GameDefinitions.IsPean(foundTemplateId, foundType))
                {
                    targetBagIndex = -1;
                    targetTemplateId = foundTemplateId;
                }
                else
                {
                    targetBagIndex = foundIndex;
                    targetTemplateId = -1;
                }
            }
        }

        _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodUseItem, service, (sbyte)0, (sbyte)1, targetBagIndex, targetTemplateId);
        return Task.FromResult(Result.Success($"Đã dùng {resolvedName}"));
    }

    private bool TryResolveBagItem(
        IntPtr arrBagPtr,
        short requestedTemplateId,
        out sbyte bagIndex,
        out short templateId,
        out sbyte itemType,
        out string itemName)
    {
        bagIndex = -1;
        templateId = requestedTemplateId;
        itemType = -1;
        itemName = string.Empty;

        IntPtr[] itemPtrs = _mono.ReadMonoPointerArray(arrBagPtr);
        for (int i = 0; i < itemPtrs.Length; i++)
        {
            IntPtr itemPtr = itemPtrs[i];
            if (itemPtr == IntPtr.Zero) continue;

            int quantity = _mono.ReadField<int>(GameSchema.Item.ClassName, GameSchema.Item.FieldQuantity, itemPtr);
            if (quantity < 0) continue;

            IntPtr templatePtr = _mono.ReadFieldPtr(GameSchema.Item.ClassName, GameSchema.Item.FieldTemplate, itemPtr);
            if (templatePtr == IntPtr.Zero) continue;

            short curTemplateId = _mono.ReadField<short>(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldId, templatePtr);
            sbyte curType = _mono.ReadField<sbyte>(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldType, templatePtr);

            bool isMatch = curTemplateId == requestedTemplateId
                || (requestedTemplateId == GameDefinitions.DefaultPeanTemplateId && GameDefinitions.IsPean(curTemplateId, curType))
                || (requestedTemplateId == GameDefinitions.DefaultCapsuleTemplateId && GameDefinitions.IsCapsule(curTemplateId))
                || (requestedTemplateId == GameDefinitions.DefaultTdltTemplateId && GameDefinitions.IsTdlt(curTemplateId));

            if (isMatch)
            {
                bagIndex = (sbyte)i;
                templateId = curTemplateId;
                itemType = curType;
                itemName = _mono.ReadStringField(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldName, templatePtr);
                return true;
            }
        }

        return false;
    }

    public Task<Result> HandleAsync(PickItemCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _mono.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodPickItem, service, command.ItemMapId);
        return Task.FromResult(Result.Success($"Đã nhặt {command.ItemName} #{command.ItemMapId}"));
    }

    public Task<Result> HandleAsync(SendChatCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Text))
            return Task.FromResult(Result.Failure("Nội dung chat trống."));

        IntPtr service = _mono.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        string method = command.IsGlobal ? GameSchema.Service.MethodChatGlobal : GameSchema.Service.MethodChat;
        _mono.CallMethod(GameSchema.Service.ClassName, method, service, command.Text);

        return Task.FromResult(Result.Success($"Đã chat: {command.Text}"));
    }

    public Task<Result> HandleAsync(WakeUpCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _mono.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodWakeUpFromDead, service);
        return Task.FromResult(Result.Success("Đã gửi yêu cầu hồi sinh về nhà."));
    }

    public Task<Result> HandleAsync(ChangeZoneCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _mono.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodRequestChangeZone, service, command.ZoneId, -1);
        return Task.FromResult(Result.Success($"Đã gửi yêu cầu đổi sang khu {command.ZoneId}"));
    }

    public Task<Result> HandleAsync(OpenZonePanelCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _mono.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodOpenUIZone, service);
        return Task.FromResult(Result.Success("Đã gửi yêu cầu mở bảng chọn khu vực."));
    }

    public Task<Result> HandleAsync(CloseZonePanelCommand command, CancellationToken cancellationToken)
    {
        IntPtr canvas = _mono.GetGameCanvasPtr();
        if (canvas == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy GameCanvas."));

        _mono.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodEndDlg, canvas);
        return Task.FromResult(Result.Success("Đã đóng bảng chọn khu vực."));
    }

    public Task<Result> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _mono.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodLogin, service,
            command.Username, command.Password, command.Version, (sbyte)0);
        return Task.FromResult(Result.Success($"Đã gửi yêu cầu đăng nhập {command.Username}"));
    }

    public Task<Result> HandleAsync(FocusEntityCommand command, CancellationToken cancellationToken)
    {
        IntPtr charPtr = _mono.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Nhân vật chưa vào game."));

        _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldMobFocus, charPtr, IntPtr.Zero);
        _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldNpcFocus, charPtr, IntPtr.Zero);
        _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCharFocus, charPtr, IntPtr.Zero);
        _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldItemFocus, charPtr, IntPtr.Zero);

        string type = command.EntityType.Trim().ToLowerInvariant();
        IntPtr targetPtr = IntPtr.Zero;
        string focusField = string.Empty;

        switch (type)
        {
            case "mob":
                targetPtr = FindEntityInVector(GameSchema.GameScr.StaticVMob, GameSchema.Mob.ClassName, GameSchema.Mob.FieldMobId, command.EntityId);
                focusField = GameSchema.Char.FieldMobFocus;
                break;

            case "npc":
                targetPtr = FindEntityInVector(GameSchema.GameScr.StaticVNpc, GameSchema.Npc.ClassName, GameSchema.Npc.FieldNpcId, command.EntityId);
                focusField = GameSchema.Char.FieldNpcFocus;
                break;

            case "boss":
            case "player":
                targetPtr = FindEntityInVector(GameSchema.GameScr.StaticVCharInMap, GameSchema.Char.ClassName, GameSchema.Char.FieldCharId, command.EntityId);
                focusField = GameSchema.Char.FieldCharFocus;
                break;

            case "item":
                targetPtr = FindItemInVector(command.EntityId);
                focusField = GameSchema.Char.FieldItemFocus;
                break;
        }

        if (targetPtr == IntPtr.Zero || string.IsNullOrEmpty(focusField))
        {
            return Task.FromResult(Result.Failure($"Không tìm thấy đối tượng {command.Name}."));
        }

        _mono.WriteField(GameSchema.Char.ClassName, focusField, charPtr, targetPtr);
        _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldIsManualFocus, charPtr, true);

        if (type == "npc")
        {
            IntPtr gameScr = _mono.GetGameScrPtr();
            if (gameScr != IntPtr.Zero)
            {
                _mono.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodDoDoubleClickToObj, gameScr, targetPtr);
            }
        }

        return Task.FromResult(Result.Success($"Đã chọn {command.Name}"));
    }

    private IntPtr FindEntityInVector(string staticVectorField, string itemClassName, string idFieldName, int targetId)
    {
        IntPtr vectorPtr = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, staticVectorField);
        if (vectorPtr == IntPtr.Zero) return IntPtr.Zero;

        foreach (var ptr in CollectionHelper.ReadMyVector(_mono, vectorPtr))
        {
            if (ptr != IntPtr.Zero && _mono.ReadField<int>(itemClassName, idFieldName, ptr) == targetId)
            {
                return ptr;
            }
        }
        return IntPtr.Zero;
    }

    private IntPtr FindItemInVector(int targetId)
    {
        IntPtr vectorPtr = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVItemMap);
        if (vectorPtr == IntPtr.Zero) return IntPtr.Zero;

        foreach (var ptr in CollectionHelper.ReadMyVector(_mono, vectorPtr))
        {
            if (ptr != IntPtr.Zero && _mono.ReadField<short>(GameSchema.ItemMap.ClassName, GameSchema.ItemMap.FieldItemMapId, ptr) == targetId)
            {
                return ptr;
            }
        }
        return IntPtr.Zero;
    }
}
