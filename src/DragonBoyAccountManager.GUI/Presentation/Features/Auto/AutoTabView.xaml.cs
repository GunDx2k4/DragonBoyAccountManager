using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public partial class AutoTabView : UserControl
{
    public AutoTabView()
    {
        InitializeComponent();
    }

    private void SkillSlot_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement elem && elem.DataContext is SkillButtonItem item)
        {
            e.Handled = true;
            if (DataContext is AutoTabViewModel vm)
            {
                vm.UseSkillSlotCommand.Execute(item);
            }
        }
    }
}
