using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DragonBoyAccountManager.GUI.ViewModels;

namespace DragonBoyAccountManager.GUI.Views;

public partial class AccountsPanelView : UserControl
{
    private bool _isSyncingSelection;

    public AccountsPanelView()
    {
        InitializeComponent();
        DataContextChanged += AccountsPanelView_DataContextChanged;
    }

    private AccountsPanelViewModel? ViewModel => DataContext as AccountsPanelViewModel;

    private void AccountsPanelView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AccountsPanelViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }
        if (e.NewValue is AccountsPanelViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountsPanelViewModel.IsAllSelected))
        {
            if (_isSyncingSelection || ViewModel == null) return;
            _isSyncingSelection = true;
            try
            {
                if (ViewModel.IsAllSelected)
                {
                    AccountsDataGrid.SelectAll();
                }
                else
                {
                    AccountsDataGrid.UnselectAll();
                }
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }
    }

    private void AccountsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection || ViewModel == null || sender is not DataGrid grid) return;

        _isSyncingSelection = true;
        try
        {
            var selectedList = grid.SelectedItems.OfType<AccountItemViewModel>().ToList();
            ViewModel.UpdateSelectedRows(selectedList);

            // Bôi đen = Tích chọn: Đồng bộ IsSelected của các item theo dòng đang được bôi đen trong DataGrid
            var selectedSet = new HashSet<AccountItemViewModel>(selectedList);
            foreach (var item in ViewModel.Accounts)
            {
                item.IsSelected = selectedSet.Contains(item);
            }
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_isSyncingSelection || ViewModel == null) return;

        if (sender is CheckBox cb && cb.DataContext is AccountItemViewModel item)
        {
            _isSyncingSelection = true;
            try
            {
                bool isChecked = cb.IsChecked == true;
                item.IsSelected = isChecked;

                if (isChecked)
                {
                    if (!AccountsDataGrid.SelectedItems.Contains(item))
                    {
                        AccountsDataGrid.SelectedItems.Add(item);
                    }
                }
                else
                {
                    AccountsDataGrid.SelectedItems.Remove(item);
                }

                var selectedList = AccountsDataGrid.SelectedItems.OfType<AccountItemViewModel>().ToList();
                ViewModel.UpdateSelectedRows(selectedList);
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }
    }

    private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_isSyncingSelection || ViewModel == null) return;

        if (sender is CheckBox cb)
        {
            _isSyncingSelection = true;
            try
            {
                bool isAll = cb.IsChecked == true;
                if (isAll)
                {
                    AccountsDataGrid.SelectAll();
                    foreach (var item in ViewModel.AccountsView.Cast<AccountItemViewModel>())
                    {
                        item.IsSelected = true;
                    }
                }
                else
                {
                    AccountsDataGrid.UnselectAll();
                    foreach (var item in ViewModel.Accounts)
                    {
                        item.IsSelected = false;
                    }
                }

                var selectedList = AccountsDataGrid.SelectedItems.OfType<AccountItemViewModel>().ToList();
                ViewModel.UpdateSelectedRows(selectedList);
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }
    }

    private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel != null && sender is DataGridRow row && row.Item is AccountItemViewModel item)
        {
            ViewModel.OpenOrFocusAccountCommand.Execute(item);
            e.Handled = true;
        }
    }

    private void DataGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row && row.Item is AccountItemViewModel item)
        {
            // Nếu click chuột phải vào hàng chưa nằm trong nhóm được chọn,
            // thì chuyển sang chọn riêng hàng này.
            // Nếu hàng này ĐÃ được chọn (trong nhóm 2+ hàng), giữ nguyên cả nhóm để menu áp dụng cho tất cả.
            if (!row.IsSelected)
            {
                AccountsDataGrid.SelectedItems.Clear();
                row.IsSelected = true;
                if (ViewModel != null)
                {
                    ViewModel.SelectedAccount = item;
                }
            }
            row.Focus();
        }
    }
}
