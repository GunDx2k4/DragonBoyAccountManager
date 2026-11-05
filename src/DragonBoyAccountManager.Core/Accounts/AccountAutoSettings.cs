using System.Text.Json;
using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

namespace DragonBoyAccountManager.Core.Accounts;

public sealed class AccountAutoSettings
{
    public AutoTrainOptions Train { get; set; } = new();
    public AutoChatOptions Chat { get; set; } = new();
    public Dictionary<string, JsonElement> CustomBehaviors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public TOptions? GetBehaviorOptions<TOptions>(string behaviorId) where TOptions : class
    {
        if (string.Equals(behaviorId, AutoTrainBehavior.BehaviorId, StringComparison.OrdinalIgnoreCase) && Train is TOptions trainOpts)
            return trainOpts;

        if (string.Equals(behaviorId, AutoChatBehavior.BehaviorId, StringComparison.OrdinalIgnoreCase) && Chat is TOptions chatOpts)
            return chatOpts;

        if (CustomBehaviors.TryGetValue(behaviorId, out var element))
        {
            return element.Deserialize<TOptions>();
        }
        return null;
    }

    public void SetBehaviorOptions<TOptions>(string behaviorId, TOptions options) where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.Equals(behaviorId, AutoTrainBehavior.BehaviorId, StringComparison.OrdinalIgnoreCase) && options is AutoTrainOptions trainOpts)
        {
            Train = trainOpts;
            return;
        }

        if (string.Equals(behaviorId, AutoChatBehavior.BehaviorId, StringComparison.OrdinalIgnoreCase) && options is AutoChatOptions chatOpts)
        {
            Chat = chatOpts;
            return;
        }

        CustomBehaviors[behaviorId] = JsonSerializer.SerializeToElement(options);
    }
}
