using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Objects;

public record InventoryItem(int Index, short TemplateId, string Name, int Quantity);

public class GameInventory(MonoReflector reflector) : GameObject(reflector)
{
    public override string ClassName => "Char";

    public override IntPtr Pointer
    {
        get
        {
            IntPtr ptr = ReadStatic<IntPtr>("myChar");
            if (ptr == IntPtr.Zero)
            {
                ptr = Reflector.CallMethod("Char", "myCharz", IntPtr.Zero);
            }
            return ptr;
        }
    }

    public List<InventoryItem> GetBagItems()
    {
        var items = new List<InventoryItem>();
        if (!IsAlive) return items;

        IntPtr bagArrayPtr = ReadFieldPtr("arrItemBag");
        if (bagArrayPtr == IntPtr.Zero) return items;

        IntPtr[] itemPointers = Reflector.ReadMonoPointerArray(bagArrayPtr);
        for (int i = 0; i < itemPointers.Length; i++)
        {
            IntPtr itemPtr = itemPointers[i];
            if (itemPtr == IntPtr.Zero) continue;

            IntPtr templatePtr = Reflector.ReadFieldPtr("Item", "template", itemPtr);
            if (templatePtr == IntPtr.Zero) continue;

            short templateId = Reflector.ReadField<short>("ItemTemplate", "id", templatePtr);
            string name = Reflector.ReadStringField("ItemTemplate", "name", templatePtr);
            int quantity = Reflector.ReadField<int>("Item", "quantity", itemPtr);

            items.Add(new InventoryItem(i, templateId, name, quantity));
        }

        return items;
    }

    public InventoryItem? FindItemInBag(params short[] templateIds)
    {
        var bag = GetBagItems();
        foreach (short id in templateIds)
        {
            var match = bag.FirstOrDefault(item => item.TemplateId == id);
            if (match != null)
            {
                return match;
            }
        }
        return null;
    }

    public bool UseItem(sbyte index, short templateId = -1)
    {
        try
        {
            IntPtr service = Reflector.CallMethod("Service", "gI", IntPtr.Zero);
            if (service == IntPtr.Zero)
            {
                service = Reflector.ReadStatic<IntPtr>("Service", "instance");
            }

            if (service == IntPtr.Zero) return false;

            // Service.gI().useItem((sbyte)type, (sbyte)where, (sbyte)index, (short)template)
            // type = 0 (dùng item), where = 1 (túi hành trang / arrItemBag)
            Reflector.CallMethod("Service", "useItem", service, (sbyte)0, (sbyte)1, index, templateId);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameInventory] Lỗi dùng vật phẩm: {ex.Message}");
            return false;
        }
    }

    public (bool Success, string Message) UseCapsule()
    {
        if (!IsAlive)
        {
            return (false, "Nhân vật chưa vào game!");
        }

        // Ưu tiên 194 (Viên Capsule đặc biệt VIP không giới hạn), sau đó 193 (Gói 10 viên Capsule)
        var item = FindItemInBag(194, 193);
        if (item == null)
        {
            return (false, "Không tìm thấy Capsule (194/193) trong hành trang!");
        }

        // Service.gI().useItem(0, 1, -1, this.arrItemBag[i].template.id);
        bool ok = UseItem(-1, item.TemplateId);
        if (ok)
        {
            return (true, $"Đã dùng {item.Name} (ID: {item.TemplateId})");
        }

        return (false, "Không thể gửi lệnh sử dụng Capsule!");
    }

    public (bool Success, string Message) UseTdlt()
    {
        if (!IsAlive)
        {
            return (false, "Nhân vật chưa vào game!");
        }

        // Ưu tiên 521 (Tự động luyện tập), hỗ trợ cả 1523, 1524 nếu có
        var item = FindItemInBag(521, 1523, 1524);
        if (item == null)
        {
            return (false, "Không tìm thấy Tự Động Luyện Tập (521) trong hành trang!");
        }

        // Service.gI().useItem(0, 1, -1, this.arrItemBag[i].template.id);
        bool ok = UseItem(-1, item.TemplateId);
        if (ok)
        {
            return (true, $"Đã dùng {item.Name} (ID: {item.TemplateId})");
        }

        return (false, "Không thể gửi lệnh sử dụng TDLT!");
    }

    public void HidePanelIfOpen()
    {
        try
        {
            IntPtr panelPtr = Reflector.ReadStatic<IntPtr>("GameCanvas", "panel");
            if (panelPtr != IntPtr.Zero)
            {
                Reflector.CallMethod("Panel", "hide", panelPtr);
            }
        }
        catch { }
    }
}
