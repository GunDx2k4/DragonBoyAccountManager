namespace DragonBoyAccountManager.Core.Game.Models;

public sealed record InventoryItem(int Index, short TemplateId, string Name, int Quantity);
