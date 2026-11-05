using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class InventoryHandler(IGameRuntime runtime) :
    IGameCommandHandler<UseItemCommand>,
    IGameCommandHandler<PickItemCommand>
{
    private readonly IGameRuntime _runtime = runtime;

    public Task<Result> HandleAsync(UseItemCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        sbyte targetBagIndex = command.BagIndex;
        short targetTemplateId = command.TemplateId;
        string resolvedName = command.ItemName;

        if (targetBagIndex < 0)
        {
            IntPtr charPtr = _runtime.GetMyCharPtr();
            IntPtr arrBagPtr = charPtr != IntPtr.Zero
                ? _runtime.ReadFieldPtr(charPtr, _runtime.Layout.Char.ArrItemBag)
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

        _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodUseItem, service, (sbyte)0, (sbyte)1, targetBagIndex, targetTemplateId);
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

        var itemLayout = _runtime.Layout.Item;
        var itemTemplateLayout = _runtime.Layout.ItemTemplate;

        IntPtr[] itemPtrs = _runtime.ReadPointerArray(arrBagPtr);
        for (int i = 0; i < itemPtrs.Length; i++)
        {
            IntPtr itemPtr = itemPtrs[i];
            if (itemPtr == IntPtr.Zero) continue;

            int quantity = _runtime.ReadField<int>(itemPtr, itemLayout.Quantity);
            if (quantity < 0) continue;

            IntPtr templatePtr = _runtime.ReadFieldPtr(itemPtr, itemLayout.Template);
            if (templatePtr == IntPtr.Zero) continue;

            short curTemplateId = _runtime.ReadField<short>(templatePtr, itemTemplateLayout.Id);
            sbyte curType = _runtime.ReadField<sbyte>(templatePtr, itemTemplateLayout.Type);

            bool isMatch = curTemplateId == requestedTemplateId
                || (requestedTemplateId == GameDefinitions.DefaultPeanTemplateId && GameDefinitions.IsPean(curTemplateId, curType))
                || (requestedTemplateId == GameDefinitions.DefaultCapsuleTemplateId && GameDefinitions.IsCapsule(curTemplateId))
                || (requestedTemplateId == GameDefinitions.DefaultTdltTemplateId && GameDefinitions.IsTdlt(curTemplateId));

            if (isMatch)
            {
                bagIndex = (sbyte)i;
                templateId = curTemplateId;
                itemType = curType;
                itemName = _runtime.ReadStringField(templatePtr, itemTemplateLayout.Name);
                return true;
            }
        }

        return false;
    }

    public Task<Result> HandleAsync(PickItemCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodPickItem, service, command.ItemMapId);
        return Task.FromResult(Result.Success($"Đã nhặt {command.ItemName} #{command.ItemMapId}"));
    }
}
