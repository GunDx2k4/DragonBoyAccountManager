
namespace DragonBoyAccountManager.Core.Automation.Routines.Training;

public sealed class AutoTrainRoutine(AutoTrainOptions options)
    : AutomationRoutine<AutoTrainOptions>("Auto Train", options, RoutinePriority.Primary, "AutoTrain")
{
    private readonly Dictionary<int, DateTime> _mobDeathTimes = new();
    private readonly Dictionary<int, long> _skillLastUseTimes = new();
    private int _skillRotationIndex;
    private DateTime _lastPeanTime = DateTime.MinValue;
    private DateTime _lastTdltCheckTime = DateTime.MinValue;

    protected override void OnOptionsChanged(AutoTrainOptions previousOptions, AutoTrainOptions newOptions)
    {
        _skillRotationIndex = 0;
    }

    protected override void OnStarted(IGameContext context, IProgress<string>? progress)
    {
        RegisterInterrupter(new DialogDismissInterrupter());
        context.World.SuppressAutoPlay();
        progress?.Report("[Auto Train] Bắt đầu tự động luyện tập...");
    }

    protected override void OnStopped(IGameContext context, IProgress<string>? progress)
    {
        context.World.SuppressAutoPlay();
        progress?.Report("[Auto Train] Đã dừng tự động luyện tập.");
    }

    protected override async Task OnTickAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        context.World.SuppressAutoPlay();

        if (!context.Player.IsInGame || !context.Player.IsAlive || context.Player.IsChangingMap)
        {
            if (!context.Player.IsAlive && context.Player.IsInGame && !context.Player.IsChangingMap)
            {
                await ExecuteActionAsync(new WakeUpAction(), context, progress, cancellationToken);
                await Task.Delay(1000, cancellationToken);
                return;
            }

            await Task.Delay(500, cancellationToken);
            return;
        }

        IntPtr myCharPtr = context.Player.Pointer;
        if (myCharPtr == IntPtr.Zero)
        {
            await Task.Delay(300, cancellationToken);
            return;
        }

        if (Options.UseTdlt && DateTime.UtcNow - _lastTdltCheckTime > TimeSpan.FromSeconds(5))
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

        if (Options.AutoUsePean && DateTime.UtcNow - _lastPeanTime > TimeSpan.FromSeconds(2))
        {
            long curHp = context.Player.Hp;
            long maxHp = context.Player.MaxHp;
            long curMp = context.Player.Mp;
            long maxMp = context.Player.MaxMp;

            if ((maxHp > 0 && curHp * 100 / maxHp <= Options.MinHpPercent) ||
                (maxMp > 0 && curMp * 100 / maxMp <= Options.MinMpPercent))
            {
                var peanResult = await ExecuteActionAsync(new UsePeanAction(), context, progress, cancellationToken);
                if (peanResult.IsSuccess)
                {
                    _lastPeanTime = DateTime.UtcNow;
                }
            }
        }

        int playerX = context.Player.X;
        int playerY = context.Player.Y;

        if (Options.AutoPickItems)
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

                    var pickAction = new PickItemAction(
                        itemToPick.Id,
                        itemToPick.X,
                        itemToPick.Y,
                        autoApproach: true,
                        isFastTeleport: Options.UseTdlt);

                    await ExecuteActionAsync(pickAction, context, progress, cancellationToken);
                    context.World.SuppressAutoPlay();
                    await Task.Delay(200, cancellationToken);
                    return;
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

        if (Options.AvoidSuperMobs)
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

                var evadeAction = new AutoMoveAction(safeX, safeY, isFastTeleport: Options.UseTdlt);
                await ExecuteActionAsync(evadeAction, context, progress, cancellationToken);

                context.World.SuppressAutoPlay();
                await Task.Delay(300, cancellationToken);
                return;
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
            await HandleNoTargetAsync(context, monsters, playerX, playerY, progress, cancellationToken);
            return;
        }

        int mobX = targetMob.X;
        int mobY = targetMob.Y;
        int cdir = mobX >= playerX ? 1 : -1;
        int attackStandX = mobX - (cdir * 24);
        int attackStandY = mobY;
        int distToMob = (int)targetMob.DistanceTo(playerX, playerY);

        if (distToMob > 45)
        {
            var approachAction = new AutoMoveAction(attackStandX, attackStandY, isFastTeleport: Options.UseTdlt);
            await ExecuteActionAsync(approachAction, context, progress, cancellationToken);
        }

        long curMobHp = context.Reflector.ReadField<long>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldHp, targetMob.Pointer);
        int curMobStatus = context.Reflector.ReadField<int>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldStatus, targetMob.Pointer);
        if (curMobHp <= 0 || curMobStatus == 0 || curMobStatus == 1)
        {
            _mobDeathTimes[targetMob.Id] = DateTime.UtcNow;
            progress?.Report($"[Auto Train] Quái #{targetMob.Id} ({targetMob.Name}) đã chết, chuyển mục tiêu...");
            return;
        }

        int targetSlot = GetTargetSkillSlot();
        long remainingCooldown = GetSkillRemainingCooldown(context, targetSlot);
        if (remainingCooldown > 0)
        {
            progress?.Report($"[Auto Train] Kỹ năng ô {targetSlot + 1} đang hồi chiêu (chờ {remainingCooldown}ms)...");
            context.World.SuppressAutoPlay();
            int delay = (int)Math.Clamp(remainingCooldown, 50, 1000);
            await Task.Delay(delay, cancellationToken);
            return;
        }

        if (targetMob.Pointer != IntPtr.Zero)
        {
            var attackAction = new AttackMobAction(targetMob, targetSlot);
            var attackResult = await ExecuteActionAsync(attackAction, context, progress, cancellationToken);
            if (attackResult.IsSuccess)
            {
                _skillLastUseTimes[targetSlot] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                long hpAfter = context.Reflector.ReadField<long>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldHp, targetMob.Pointer);
                int statusAfter = context.Reflector.ReadField<int>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldStatus, targetMob.Pointer);
                if (hpAfter <= 0 || statusAfter == 0 || statusAfter == 1)
                {
                    _mobDeathTimes[targetMob.Id] = DateTime.UtcNow;
                }
            }
        }

        _skillRotationIndex++;
        context.World.SuppressAutoPlay();
        await Task.Delay(350, cancellationToken);
    }

    private async Task HandleNoTargetAsync(
        IGameContext context,
        IReadOnlyList<MobEntity> monsters,
        int playerX,
        int playerY,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
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
                var moveAction = new AutoMoveAction(spawnX, spawnY, isFastTeleport: Options.UseTdlt);
                await ExecuteActionAsync(moveAction, context, progress, cancellationToken);
                progress?.Report($"[Auto Train] Quái #{earliestMob.Id} ({earliestMob.Name}) dự kiến hồi sinh sớm nhất, đang đón đầu tại ({spawnX}, {spawnY})...");
            }
            else
            {
                progress?.Report($"[Auto Train] Đang chờ quái #{earliestMob.Id} ({earliestMob.Name}) hồi sinh tại ({spawnX}, {spawnY})...");
            }
        }
        else
        {
            progress?.Report("[Auto Train] Không tìm thấy quái phù hợp trong bản đồ. Đang chờ quái hồi sinh...");
        }

        context.World.SuppressAutoPlay();
        await Task.Delay(350, cancellationToken);
    }

    private bool MatchesFilter(MobEntity m)
    {
        if (Options.AvoidSuperMobs && m.IsSuperMob) return false;
        if (Options.IgnoreBosses && m.IsBoss) return false;
        if (Options.TargetMobTemplateIds.Count > 0 && !Options.TargetMobTemplateIds.Contains(m.TemplateId)) return false;
        if (Options.TargetMobIds.Count > 0 && !Options.TargetMobIds.Contains(m.Id)) return false;
        return true;
    }

    private int GetTargetSkillSlot()
    {
        var slots = Options.SelectedSkillSlots;
        if (slots == null || slots.Count == 0)
        {
            return 0;
        }

        return slots[_skillRotationIndex % slots.Count];
    }

    private long GetSkillRemainingCooldown(IGameContext context, int slot)
    {
        var skills = context.Player.GetSkills();
        var skill = skills.FirstOrDefault(s => s.Slot == slot);
        if (skill == null || skill.CoolDown <= 0)
        {
            return 0;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long lastUse = _skillLastUseTimes.TryGetValue(slot, out var t) ? Math.Max(t, skill.LastTimeUse) : skill.LastTimeUse;
        long elapsed = now - lastUse;
        long remaining = skill.CoolDown - elapsed;

        return remaining > 0 ? remaining : 0;
    }
}
