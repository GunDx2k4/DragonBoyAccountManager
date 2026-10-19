using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Entities;
using DragonBoyAccountManager.Core.Game.Schema;

namespace DragonBoyAccountManager.Core.Game.Actions;

public sealed class AutoTrainOptions
{
    public IReadOnlyList<int> SelectedSkillSlots { get; init; } = [0];
    public IReadOnlyList<int> TargetMobTemplateIds { get; init; } = [];
    public IReadOnlyList<int> TargetMobIds { get; init; } = [];
    public bool IgnoreBosses { get; init; } = true;
    public bool AvoidSuperMobs { get; init; } = true;
    public bool AutoPickItems { get; init; } = true;
    public bool AutoUsePean { get; init; } = true;
    public int MinHpPercent { get; init; } = 20;
    public int MinMpPercent { get; init; } = 20;
    public bool UseTdlt { get; init; } = true;
}

public class AutoTrainAction(AutoTrainOptions options) : GameAction("Auto Train")
{
    private readonly AutoTrainOptions _options = options;
    private readonly Dictionary<int, DateTime> _mobDeathTimes = new();
    private int _skillRotationIndex = 0;
    private DateTime _lastPeanTime = DateTime.MinValue;
    private DateTime _lastTdltCheckTime = DateTime.MinValue;
    private int _currentSelectedSkillSlot = -1;

    public AutoTrainOptions Options => _options;

    private bool MatchesFilter(MobEntity m)
    {
        if (_options.AvoidSuperMobs && m.IsSuperMob) return false;
        if (_options.IgnoreBosses && m.IsBoss) return false;
        if (_options.TargetMobTemplateIds.Count > 0 && !_options.TargetMobTemplateIds.Contains(m.TemplateId)) return false;
        if (_options.TargetMobIds.Count > 0 && !_options.TargetMobIds.Contains(m.Id)) return false;
        return true;
    }

    private static void SuppressGameAutoLoop(IGameContext context)
    {
        try
        {
            context.Reflector.WriteStatic(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticIsAutoPlay, false);
            IntPtr gameScrPtr = context.Reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticInstance);
            if (gameScrPtr != IntPtr.Zero)
            {
                context.Reflector.WriteField(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldAuto, gameScrPtr, 0);
            }
        }
        catch { }
    }

    protected override async Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!context.IsRunning || !context.Player.IsInGame)
        {
            progress?.Report("Game chưa sẵn sàng hoặc chưa vào game.");
            return ActionResult.Failure("Game chưa sẵn sàng hoặc chưa vào game.");
        }

        progress?.Report("[Auto Train] Đang khởi động tự động luyện tập...");

        SuppressGameAutoLoop(context);

        while (!cancellationToken.IsCancellationRequested && context.IsRunning)
        {
            try
            {
                SuppressGameAutoLoop(context);

                if (!context.Player.IsInGame || !context.Player.IsAlive || context.Player.IsChangingMap)
                {
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                IntPtr myCharPtr = context.Player.Pointer;
                if (myCharPtr == IntPtr.Zero)
                {
                    await Task.Delay(300, cancellationToken);
                    continue;
                }

                if (_options.UseTdlt && DateTime.UtcNow - _lastTdltCheckTime > TimeSpan.FromSeconds(5))
                {
                    _lastTdltCheckTime = DateTime.UtcNow;
                    if (!context.Player.IsTdltActive)
                    {
                        var tdltResult = context.Player.UseTdlt();
                        if (tdltResult.Success)
                        {
                            progress?.Report("[Auto Train] Đã kích hoạt Tự Động Luyện Tập (TDLT).");
                        }
                    }
                }

                if (_options.AutoUsePean && DateTime.UtcNow - _lastPeanTime > TimeSpan.FromSeconds(2))
                {
                    long curHp = context.Player.Hp;
                    long maxHp = context.Player.MaxHp;
                    long curMp = context.Player.Mp;
                    long maxMp = context.Player.MaxMp;

                    if ((maxHp > 0 && curHp * 100 / maxHp <= _options.MinHpPercent) ||
                        (maxMp > 0 && curMp * 100 / maxMp <= _options.MinMpPercent))
                    {
                        var peanResult = context.Player.UsePean();
                        if (peanResult.Success)
                        {
                            _lastPeanTime = DateTime.UtcNow;
                            progress?.Report("[Auto Train] Đã tự động sử dụng đậu thần.");
                        }
                    }
                }

                int playerX = context.Player.X;
                int playerY = context.Player.Y;

                if (_options.AutoPickItems)
                {
                    var groundItems = context.World.GetGroundItems();
                    if (groundItems.Count > 0)
                    {
                        int myCharId = context.Player.Id;
                        var myItems = groundItems.Where(i => i.PlayerId == myCharId || i.PlayerId == -1).ToList();
                        if (myItems.Count > 0)
                        {
                            var itemToPick = myItems
                                .OrderBy(i => Math.Abs(i.X - playerX) + Math.Abs(i.Y - playerY))
                                .First();

                            int itemDist = (int)Math.Sqrt(Math.Pow(itemToPick.X - playerX, 2) + Math.Pow(itemToPick.Y - playerY, 2));
                            if (itemDist > 35)
                            {
                                if (_options.UseTdlt)
                                {
                                    context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr, itemToPick.X);
                                    context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr, itemToPick.Y);
                                    context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvx, myCharPtr, 0);
                                    context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, myCharPtr, 0);
                                    context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, myCharPtr, 1);
                                    context.Network.CharMove();
                                }
                                else
                                {
                                    var moveAction = new AutoMoveAction(itemToPick.X, itemToPick.Y, isFastTeleport: false);
                                    await moveAction.ExecuteAsync(context, progress, cancellationToken);
                                }
                            }

                            context.Network.PickItem(itemToPick.Id);
                            progress?.Report($"[Auto Train] Đã gửi message nhặt {itemToPick.Name} #{itemToPick.Id}");
                            SuppressGameAutoLoop(context);
                            await Task.Delay(200, cancellationToken);
                            continue;
                        }
                    }
                }

                var monsters = context.World.GetMonsters();

                var currentIds = new HashSet<int>(monsters.Select(m => m.Id));
                var staleKeys = _mobDeathTimes.Keys.Where(id => !currentIds.Contains(id)).ToList();
                foreach (var stale in staleKeys)
                {
                    _mobDeathTimes.Remove(stale);
                }

                foreach (var m in monsters)
                {
                    if (m.IsAlive)
                    {
                        _mobDeathTimes.Remove(m.Id);
                    }
                    else if (!_mobDeathTimes.ContainsKey(m.Id))
                    {
                        _mobDeathTimes[m.Id] = DateTime.UtcNow;
                    }
                }

                if (_options.AvoidSuperMobs)
                {
                    var nearbySuperMob = monsters
                        .Where(m => m.IsAlive && m.Hp > 0 && m.IsSuperMob)
                        .FirstOrDefault(m => m.DistanceTo(playerX, playerY) < 130);

                    if (nearbySuperMob != null)
                    {
                        progress?.Report($"[Auto Train] Phát hiện siêu quái {nearbySuperMob.Name} gần bạn ({nearbySuperMob.DistanceTo(playerX, playerY):F0}px)! Đang né tránh...");

                        int evadeDir = playerX >= nearbySuperMob.X ? 1 : -1;
                        int safeX = playerX + (evadeDir * 180);
                        int safeY = playerY;

                        var safeMob = monsters
                            .Where(m => m.IsAlive && m.Hp > 0 && !m.IsSuperMob && m.DistanceTo(nearbySuperMob.X, nearbySuperMob.Y) > 200)
                            .OrderBy(m => m.DistanceTo(playerX, playerY))
                            .FirstOrDefault();

                        if (safeMob != null)
                        {
                            safeX = safeMob.X;
                            safeY = safeMob.Y;
                        }

                        if (_options.UseTdlt)
                        {
                            context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr, safeX);
                            context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr, safeY);
                            context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvx, myCharPtr, 0);
                            context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, myCharPtr, 0);
                            context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, myCharPtr, 1);
                            context.Network.CharMove();
                        }
                        else
                        {
                            var moveAction = new AutoMoveAction(safeX, safeY, isFastTeleport: false);
                            await moveAction.ExecuteAsync(context, progress, cancellationToken);
                        }

                        SuppressGameAutoLoop(context);
                        await Task.Delay(300, cancellationToken);
                        continue;
                    }
                }

                var candidateMobs = monsters.Where(m => m.IsAlive && MatchesFilter(m)).ToList();
                playerX = context.Player.X;
                playerY = context.Player.Y;

                MobEntity? targetMob = null;
                IntPtr currentFocusPtr = context.Player.MobFocusPtr;
                if (currentFocusPtr != IntPtr.Zero)
                {
                    var focused = candidateMobs.FirstOrDefault(m => m.Pointer == currentFocusPtr);
                    if (focused != null)
                    {
                        long fHp = context.Reflector.ReadField<long>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldHp, focused.Pointer);
                        int fStatus = context.Reflector.ReadField<int>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldStatus, focused.Pointer);
                        if (fHp > 0 && fStatus != 0 && fStatus != 1)
                        {
                            targetMob = focused;
                        }
                        else
                        {
                            _mobDeathTimes[focused.Id] = DateTime.UtcNow;
                        }
                    }
                }

                if (targetMob == null)
                {
                    targetMob = candidateMobs
                        .OrderBy(m => m.DistanceTo(playerX, playerY))
                        .FirstOrDefault();

                    if (targetMob != null)
                    {
                        context.World.Focus(targetMob);
                    }
                }

                if (targetMob == null)
                {
                    var deadCandidateMobs = monsters
                        .Where(m => !m.IsAlive && MatchesFilter(m))
                        .ToList();

                    if (deadCandidateMobs.Count > 0)
                    {
                        var earliestMob = deadCandidateMobs
                            .OrderBy(m => _mobDeathTimes.TryGetValue(m.Id, out var dt) ? dt : DateTime.MinValue)
                            .First();

                        int spawnX = earliestMob.XFirst != 0 ? earliestMob.XFirst : earliestMob.X;
                        int spawnY = earliestMob.YFirst != 0 ? earliestMob.YFirst : earliestMob.Y;
                        int distToSpawn = (int)Math.Sqrt(Math.Pow(spawnX - playerX, 2) + Math.Pow(spawnY - playerY, 2));

                        if (distToSpawn > 35)
                        {
                            if (_options.UseTdlt)
                            {
                                context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr, spawnX);
                                context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr, spawnY);
                                context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvx, myCharPtr, 0);
                                context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, myCharPtr, 0);
                                context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, myCharPtr, 1);
                                context.Network.CharMove();
                            }
                            else
                            {
                                var moveAction = new AutoMoveAction(spawnX, spawnY, isFastTeleport: false);
                                await moveAction.ExecuteAsync(context, progress, cancellationToken);
                            }
                            progress?.Report($"[Auto Train] Quái #{earliestMob.Id} ({earliestMob.Name}) dự kiến hồi sinh sớm nhất, đang đón đầu tại ({spawnX}, {spawnY})...");
                        }
                        else
                        {
                            progress?.Report($"[Auto Train] Đang chờ quái #{earliestMob.Id} ({earliestMob.Name}) hồi sinh tại ({spawnX}, {spawnY})...");
                        }
                    }
                    else
                    {
                        progress?.Report("[Auto Train] Không tìm thấy quái phù hợp trong map. Đang đợi quái hồi sinh...");
                    }

                    SuppressGameAutoLoop(context);
                    await Task.Delay(350, cancellationToken);
                    continue;
                }

                int mobX = targetMob.X;
                int mobY = targetMob.Y;
                int cdir = mobX >= playerX ? 1 : -1;
                int attackStandX = mobX - (cdir * 24);
                int attackStandY = mobY;
                int distToMob = (int)targetMob.DistanceTo(playerX, playerY);

                if (distToMob > 45)
                {
                    if (_options.UseTdlt)
                    {
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr, attackStandX);
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr, attackStandY);
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvx, myCharPtr, 0);
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, myCharPtr, 0);
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, myCharPtr, 1);
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCdir, myCharPtr, cdir);
                        context.Network.CharMove();
                    }
                    else
                    {
                        var moveAction = new AutoMoveAction(attackStandX, attackStandY, isFastTeleport: false);
                        await moveAction.ExecuteAsync(context, progress, cancellationToken);
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCdir, myCharPtr, cdir);
                    }
                }
                else
                {
                    context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCdir, myCharPtr, cdir);
                }

                long curMobHp = context.Reflector.ReadField<long>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldHp, targetMob.Pointer);
                int curMobStatus = context.Reflector.ReadField<int>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldStatus, targetMob.Pointer);
                if (curMobHp <= 0 || curMobStatus == 0 || curMobStatus == 1)
                {
                    _mobDeathTimes[targetMob.Id] = DateTime.UtcNow;
                    progress?.Report($"[Auto Train] Quái #{targetMob.Id} ({targetMob.Name}) đã chết, chuyển mục tiêu...");
                    continue;
                }

                int chosenSlot = SelectNextSkillSlot(context);

                if (_currentSelectedSkillSlot != chosenSlot)
                {
                    context.Player.SelectSkill(chosenSlot);
                    _currentSelectedSkillSlot = chosenSlot;
                }

                if (targetMob.Pointer != IntPtr.Zero)
                {
                    bool attacked = context.Network.SendPlayerAttack(targetMob.Pointer);
                    if (!attacked)
                    {
                        progress?.Report($"[Auto Train] Gửi đòn đánh thất bại tới {targetMob.Name} #{targetMob.Id}");
                    }
                    else
                    {
                        IntPtr mySkillPtr = context.Reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill, myCharPtr);
                        if (mySkillPtr != IntPtr.Zero)
                        {
                            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            context.Reflector.WriteField(GameSchema.Skill.ClassName, GameSchema.Skill.FieldLastTimeUseThisSkill, mySkillPtr, nowMs);
                        }

                        long hpAfter = context.Reflector.ReadField<long>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldHp, targetMob.Pointer);
                        int statusAfter = context.Reflector.ReadField<int>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldStatus, targetMob.Pointer);
                        if (hpAfter <= 0 || statusAfter == 0 || statusAfter == 1)
                        {
                            _mobDeathTimes[targetMob.Id] = DateTime.UtcNow;
                        }
                    }
                }

                progress?.Report($"[Auto Train] Đang đánh {targetMob.Name} #{targetMob.Id} [HP: {targetMob.Hp}/{targetMob.MaxHp}] bằng skill {chosenSlot + 1} (gửi message)");

                SuppressGameAutoLoop(context);
                await Task.Delay(350, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                progress?.Report($"[Auto Train] Ngoại lệ bước luyện tập: {ex.Message}");
                SuppressGameAutoLoop(context);
                await Task.Delay(500, cancellationToken);
            }
        }

        SuppressGameAutoLoop(context);

        progress?.Report("[Auto Train] Đã dừng tự động luyện tập.");
        return ActionResult.Success("Đã dừng tự động luyện tập.");
    }

    private int SelectNextSkillSlot(IGameContext context)
    {
        var slots = _options.SelectedSkillSlots;
        if (slots == null || slots.Count == 0)
        {
            return 0;
        }

        if (slots.Count == 1)
        {
            return slots[0];
        }

        var skills = context.Player.GetSkills();
        var usableSlots = new List<int>();

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var slot in slots)
        {
            var skill = skills.FirstOrDefault(s => s.Slot == slot);
            if (skill != null && (skill.CoolDown <= 0 || now - skill.LastTimeUse >= skill.CoolDown))
            {
                usableSlots.Add(slot);
            }
        }

        if (usableSlots.Count > 0)
        {
            int slot = usableSlots[_skillRotationIndex % usableSlots.Count];
            _skillRotationIndex++;
            return slot;
        }

        int nextSlot = slots[_skillRotationIndex % slots.Count];
        _skillRotationIndex++;
        return nextSlot;
    }
}
