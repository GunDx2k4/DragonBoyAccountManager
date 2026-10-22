
namespace DragonBoyAccountManager.GUI.Services.Settings;

public interface ISettingsService
{
    AppSettings Current { get; }
    Task SaveAsync();
    Task LoadAsync();
}
