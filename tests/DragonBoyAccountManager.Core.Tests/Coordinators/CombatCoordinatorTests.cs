using DragonBoyAccountManager.Core.Automation.Coordinators;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.Core.Tests.Mocks;

namespace DragonBoyAccountManager.Core.Tests.Coordinators;

public class CombatCoordinatorTests
{
    [Fact]
    public async Task TryExecuteCombatAsync_WhenNearMob_DispatchesAttackCommand()
    {
        var coordinator = new CombatCoordinator();
        var context = new MockSessionContext();

        var player = PlayerSnapshot.Empty with
        {
            Id = 1,
            X = 100,
            Y = 200,
            Mp = 100,
            MaxMp = 100,
            Skills =
            [
                new GameSkillInfo(
                    Slot: 0,
                    SkillId: 1,
                    Name: "Đấm Dragon",
                    Point: 7,
                    CoolDown: 0,
                    ManaUse: 0,
                    Dx: 50,
                    Dy: 50,
                    SkillType: 1)
            ]
        };

        var world = WorldSnapshot.Empty with
        {
            Mobs =
            [
                new WorldMobInfo(
                    Id: 10,
                    TemplateId: 1,
                    Name: "Mộc nhân",
                    Hp: 1000,
                    MaxHp: 1000,
                    X: 120,
                    Y: 200,
                    Status: 5,
                    IsAlive: true,
                    IsBoss: false)
            ]
        };

        var options = new CombatOptions(
            AvoidSuperMobs: true,
            CheckAllMobs: true,
            TargetMobNames: [],
            CheckAttackByHp: false,
            MinMobHp: 0,
            MaxMobHp: 0,
            SelectedSkillSlots: [0],
            AutoUsePean: false,
            MinHpPercent: 20,
            MinMpPercent: 20);

        bool handled = await coordinator.TryExecuteCombatAsync(
            context,
            player,
            world,
            options,
            "[Test]",
            null,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Single(context.MockDispatcher.DispatchedCommands);
        var cmd = Assert.IsType<AttackMobCommand>(context.MockDispatcher.DispatchedCommands[0]);
        Assert.Equal(10, cmd.MobId);
        Assert.Equal("Mộc nhân", cmd.MobName);
        Assert.Equal(0, cmd.SkillSlot);
    }

    [Fact]
    public async Task TryExecuteCombatAsync_WhenMobFar_DispatchesNavigateCommand()
    {
        var coordinator = new CombatCoordinator();
        var context = new MockSessionContext();

        var player = PlayerSnapshot.Empty with
        {
            Id = 1,
            X = 100,
            Y = 200,
            Mp = 100,
            MaxMp = 100,
            Skills =
            [
                new GameSkillInfo(
                    Slot: 0,
                    SkillId: 1,
                    Name: "Đấm Dragon",
                    Point: 7,
                    CoolDown: 0,
                    ManaUse: 0,
                    Dx: 50,
                    Dy: 50,
                    SkillType: 1)
            ]
        };

        var world = WorldSnapshot.Empty with
        {
            Mobs =
            [
                new WorldMobInfo(10, 1, "Mộc nhân", 1000, 1000, 500, 200, 5, IsAlive: true, IsBoss: false)
            ]
        };

        var options = new CombatOptions(
            AvoidSuperMobs: true,
            CheckAllMobs: true,
            TargetMobNames: [],
            CheckAttackByHp: false,
            MinMobHp: 0,
            MaxMobHp: 0,
            SelectedSkillSlots: [0],
            AutoUsePean: false,
            MinHpPercent: 20,
            MinMpPercent: 20);

        bool handled = await coordinator.TryExecuteCombatAsync(
            context,
            player,
            world,
            options,
            "[Test]",
            null,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Single(context.MockDispatcher.DispatchedCommands);
        var cmd = Assert.IsType<NavigateCommand>(context.MockDispatcher.DispatchedCommands[0]);
        Assert.Equal(500, cmd.TargetX);
        Assert.Equal(200, cmd.TargetY);
    }

    [Fact]
    public async Task TryExecuteCombatAsync_WhenAvoidSuperMobs_IgnoresBossMob()
    {
        var coordinator = new CombatCoordinator();
        var context = new MockSessionContext();

        var player = PlayerSnapshot.Empty with { Id = 1, X = 100, Y = 200, Mp = 100, MaxMp = 100 };
        var world = WorldSnapshot.Empty with
        {
            Mobs =
            [
                new WorldMobInfo(10, 1, "Boss Siêu Cấp", 10000, 10000, 120, 200, 5, IsAlive: true, IsBoss: true)
            ]
        };

        var options = new CombatOptions(
            AvoidSuperMobs: true,
            CheckAllMobs: true,
            TargetMobNames: [],
            CheckAttackByHp: false,
            MinMobHp: 0,
            MaxMobHp: 0,
            SelectedSkillSlots: [],
            AutoUsePean: false,
            MinHpPercent: 20,
            MinMpPercent: 20);

        bool handled = await coordinator.TryExecuteCombatAsync(
            context,
            player,
            world,
            options,
            "[Test]",
            null,
            CancellationToken.None);

        Assert.False(handled);
        Assert.Empty(context.MockDispatcher.DispatchedCommands);
    }
}
