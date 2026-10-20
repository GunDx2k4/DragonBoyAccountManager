namespace DragonBoyAccountManager.Core.Domain.Models;

public sealed record InventoryItem(int Index, short TemplateId, string Name, int Quantity);
