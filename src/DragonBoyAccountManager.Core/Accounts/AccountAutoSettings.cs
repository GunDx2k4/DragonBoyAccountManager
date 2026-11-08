using System.Text.Json;

namespace DragonBoyAccountManager.Core.Accounts;

public sealed class AccountAutoSettings
{
    private Dictionary<string, JsonElement> _behaviors = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, JsonElement> Behaviors
    {
        get => _behaviors;
        set => _behaviors = value != null
            ? new Dictionary<string, JsonElement>(value, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
    }

    public TOptions? GetBehaviorOptions<TOptions>(string behaviorId) where TOptions : class
    {
        if (_behaviors.TryGetValue(behaviorId, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            try
            {
                return element.Deserialize<TOptions>();
            }
            catch (JsonException)
            {
                return null;
            }
        }
        return null;
    }

    public TOptions GetBehaviorOptionsOrDefault<TOptions>(string behaviorId) where TOptions : class, new()
        => GetBehaviorOptions<TOptions>(behaviorId) ?? new TOptions();

    public void SetBehaviorOptions<TOptions>(string behaviorId, TOptions options) where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(options);
        _behaviors[behaviorId] = JsonSerializer.SerializeToElement(options);
    }
}
