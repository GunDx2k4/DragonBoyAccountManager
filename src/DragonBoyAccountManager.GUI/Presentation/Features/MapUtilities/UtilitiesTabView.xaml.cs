using System;
using System.Windows.Controls;
using DragonBoyAccountManager.GUI.ViewModels;

namespace DragonBoyAccountManager.GUI.Views;

public partial class UtilitiesTabView : UserControl
{
    public UtilitiesTabView()
    {
        InitializeComponent();
    }

    private void ZoneComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        if (DataContext is UtilitiesTabViewModel vm)
        {
            vm.RequestZoneList();
        }
    }

    private void EntityItem_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement fe && fe.DataContext != null)
        {
            if (DataContext is UtilitiesTabViewModel vm)
            {
                vm.ClickObjectCommand.Execute(fe.DataContext);
                e.Handled = true;
            }
        }
    }
}
