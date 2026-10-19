using System.Windows.Media;

namespace DragonBoyAccountManager.GUI.Services.Images;

public interface IGameImageService
{
    ImageSource? GetSmallImage(int iconId);
}
