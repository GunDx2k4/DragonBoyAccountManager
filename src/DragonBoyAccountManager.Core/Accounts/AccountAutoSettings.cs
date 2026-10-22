using System.Text.Json;
using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Common;

namespace DragonBoyAccountManager.Core.Accounts;

public sealed class AccountAutoSettings
{
    public bool IgnoreBosses { get; set; } = true;
    public bool AvoidSuperMobs { get; set; } = true;
    public bool AutoPickItems { get; set; } = true;
    public bool AutoUsePean { get; set; } = true;
    public bool UseTdlt { get; set; } = true;
    public int MinHpPercent { get; set; } = 20;
    public int MinMpPercent { get; set; } = 20;
    public List<int> SelectedSkillSlots { get; set; } = [0];
    public List<int> TargetMobTemplateIds { get; set; } = [];
    public List<int> TargetMobIds { get; set; } = [];
    public MobTargetMode TargetMode { get; set; } = MobTargetMode.ByTemplate;

    public string ChatText { get; set; } = "Chào mọi người!";
    public int ChatDelaySeconds { get; set; } = 5;
    public ChatType ChatScope { get; set; } = ChatType.Map;
    public bool IsRandomChat { get; set; } = false;

    public Dictionary<string, JsonElement> CustomBehaviors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public TOptions? GetBehaviorOptions<TOptions>(string behaviorId) where TOptions : class
    {
        if (CustomBehaviors.TryGetValue(behaviorId, out var element))
        {
            return element.Deserialize<TOptions>();
        }
        return null;
    }

    public void SetBehaviorOptions<TOptions>(string behaviorId, TOptions options) where TOptions : class
    {
        CustomBehaviors[behaviorId] = JsonSerializer.SerializeToElement(options);
    }

    public AutoTrainOptions ToAutoTrainOptions() => new()
    {
        TargetMode = TargetMode,
        SelectedSkillSlots = SelectedSkillSlots is { Count: > 0 } ? SelectedSkillSlots : [0],
        TargetMobTemplateIds = TargetMobTemplateIds ?? [],
        TargetMobIds = TargetMobIds ?? [],
        IgnoreBosses = IgnoreBosses,
        AvoidSuperMobs = AvoidSuperMobs,
        AutoPickItems = AutoPickItems,
        AutoUsePean = AutoUsePean,
        UseTdlt = UseTdlt,
        MinHpPercent = MinHpPercent,
        MinMpPercent = MinMpPercent
    };

    public AutoChatOptions ToAutoChatOptions(string? fallbackChatText = null)
    {
        var rawText = !string.IsNullOrWhiteSpace(ChatText) ? ChatText : (fallbackChatText ?? string.Empty);
        var lines = rawText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        return new AutoChatOptions
        {
            Messages = lines,
            Delay = TimeSpan.FromSeconds(Math.Max(1, ChatDelaySeconds <= 0 ? 5 : ChatDelaySeconds)),
            IsGlobal = ChatScope == ChatType.Global,
            IsRandom = IsRandomChat
        };
    }
}
