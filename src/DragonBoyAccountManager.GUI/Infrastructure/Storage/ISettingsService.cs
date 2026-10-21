
namespace DragonBoyAccountManager.GUI.Infrastructure.Storage;

public interface ISettingsService
{
    AppSettings Current { get; }
    Task SaveAsync();
    Task LoadAsync();
}
