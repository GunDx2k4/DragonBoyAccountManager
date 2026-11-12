using System.Windows;
using System.Windows.Input;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Dialogs;

public partial class XmapNodePickerDialog : Window
{
    private readonly XmapNodePickerViewModel _viewModel;

    public XmapGraphNodeViewModel? SelectedNode => _viewModel.SelectedNode;

    public XmapNodePickerDialog(XmapNodePickerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestClose = () =>
        {
            DialogResult = _viewModel.Confirmed;
            Close();
        };
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Cancel();
    }

    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: XmapGraphNodeViewModel node })
        {
            _viewModel.SelectNode(node);
            if (e.ClickCount >= 2)
            {
                _viewModel.Confirm();
            }
        }
    }
}
