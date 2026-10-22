using System.Windows.Media;

namespace DragonBoyAccountManager.GUI.Services.Engine;

public interface IGameImageService
{
    ImageSource? GetSmallImage(int iconId);
}
