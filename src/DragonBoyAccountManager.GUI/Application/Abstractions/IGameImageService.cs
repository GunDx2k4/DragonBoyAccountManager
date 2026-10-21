using System.Windows.Media;

namespace DragonBoyAccountManager.GUI.Application.Abstractions;

public interface IGameImageService
{
    ImageSource? GetSmallImage(int iconId);
}
