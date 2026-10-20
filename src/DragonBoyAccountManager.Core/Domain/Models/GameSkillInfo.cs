namespace DragonBoyAccountManager.Core.Domain.Models;

public sealed record GameSkillInfo(int Slot, short SkillId, string Name, int Point, int CoolDown, int ManaUse, int IconId = -1, bool IsSelected = false, long LastTimeUse = 0);
