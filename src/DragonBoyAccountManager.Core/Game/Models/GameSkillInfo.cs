namespace DragonBoyAccountManager.Core.Game.Models;

public sealed record GameSkillInfo(int Slot, short SkillId, string Name, int Point, int CoolDown, int ManaUse, bool IsSelected = false);
