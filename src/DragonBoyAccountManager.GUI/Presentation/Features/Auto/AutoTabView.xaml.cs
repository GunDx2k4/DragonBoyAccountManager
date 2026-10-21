using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DragonBoyAccountManager.GUI.ViewModels;

namespace DragonBoyAccountManager.GUI.Views;

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
