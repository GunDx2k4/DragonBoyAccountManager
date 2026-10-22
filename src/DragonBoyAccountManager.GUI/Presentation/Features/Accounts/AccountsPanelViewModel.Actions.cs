using System.IO;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.Core.Sessions.Validation;
using DragonBoyAccountManager.GUI.Domain.Messaging;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts.Items;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Accounts;

public sealed partial class AccountsPanelViewModel
{
    public void CheckAssemblyStatus()
    {
        ClientStatus = _envService.Status;
        IsAssemblyMissing = _envService.IsAssemblyMissing;

        switch (ClientStatus)
        {
            case ClientStatus.GameNotFound:
                AssemblyGuidanceTitle = "CHƯA THIẾT LẬP FILE GAME GỐC 2.5.0 (.EXE)";
                AssemblyGuidanceMessage = "DBAM yêu cầu phiên bản game PC gốc 2.5.0 tải tại trang chủ (ngocrongonline.com) — tuyệt đối không cài bản mod. Vui lòng bấm \"Chọn file game (.exe)\" để bắt đầu.";
                break;

            case ClientStatus.AssemblyNotFound:
                AssemblyGuidanceTitle = "KHÔNG TÌM THẤY ASSEMBLY-CSHARP.DLL";
                AssemblyGuidanceMessage = "Đã nhận file .exe nhưng thiếu file Assembly-CSharp.dll trong Data\\Managed. Vui lòng tải bản game PC 2.5.0 gốc tại trang chủ (không dùng bản mod) và kiểm tra lại thư mục game.";
                break;

            case ClientStatus.ModifiedOrMod:
                AssemblyGuidanceTitle = "PHÁT HIỆN FILE GAME BỊ SỬA ĐỔI HOẶC BẢN MOD";
                AssemblyGuidanceMessage = "DBAM chỉ hỗ trợ phiên bản gốc 2.5.0 chính thức từ trang chủ. Các bản mod (Mod, Auto, Hack, DLL đã can thiệp) có thể gây lỗi bộ nhớ hoặc không tương thích!";
                break;

            case ClientStatus.OutdatedVersion:
                AssemblyGuidanceTitle = "PHIÊN BẢN GAME ĐÃ CŨ";
                AssemblyGuidanceMessage = "Phiên bản game hiện tại không phải 2.5.0. Vui lòng tải bản mới nhất tại ngocrongonline.com để sử dụng.";
                break;

            default:
                AssemblyGuidanceTitle = string.Empty;
                AssemblyGuidanceMessage = string.Empty;
                IsGuidanceDismissed = false;
                break;
        }
    }

    [RelayCommand]
    public void BrowseGamePath() => _envService.BrowseAndSelectGamePath(_dialogService);

    [RelayCommand]
    public void OpenGuide() => WeakReferenceMessenger.Default.Send(new RequestNavigateToTabMessage(AppTab.Guide));

    [RelayCommand]
    public void DismissGuidance() => IsGuidanceDismissed = true;

    [RelayCommand]
    public void ShowGuidance() => IsGuidanceDismissed = false;

    [RelayCommand]
    public async Task OpenOrFocusAccountAsync(AccountItemViewModel? item)
    {
        item ??= SelectedAccount;
        if (item == null) return;

        if (item.IsRunning)
        {
            FocusGameWindow(item);
        }
        else
        {
            await LoginSelectedAccountsCommand.ExecuteAsync(item);
        }
    }

    private bool EnsureGameClientReady(out string gamePath)
    {
        gamePath = _envService.GamePath;
        var metadata = _envService.CurrentMetadata;
        if (metadata != null && metadata.IsFound)
        {
            return true;
        }

        if (metadata?.Status == ClientStatus.AssemblyNotFound)
        {
            _dialogService.ShowWarning(
                "Không tìm thấy Assembly-CSharp",
                $"Đã nhận diện file thực thi game: {Path.GetFileName(gamePath)}\n" +
                "Nhưng không tìm thấy file Assembly-CSharp.dll trong thư mục dữ liệu Data\\Managed.\n\n" +
                "Vui lòng tải bản game PC gốc 2.5.0 tại trang chủ (ngocrongonline.com) — không dùng bản mod, và đảm bảo cấu trúc:\n" +
                "- [TênGame].exe\n" +
                "- [TênGame]_Data\\Managed\\Assembly-CSharp.dll");
        }
        else
        {
            bool chooseNow = _dialogService.ShowConfirmation(
                "Chưa cài đặt đường dẫn game",
                "Chưa chọn tệp thực thi game (.exe) hoặc đường dẫn không tồn tại.\n\n" +
                "Vui lòng tải bản game PC gốc 2.5.0 tại trang chủ (ngocrongonline.com) — tuyệt đối không cài bản mod.\n\n" +
                "Bạn có muốn chọn file game (.exe) ngay bây giờ không?",
                "Chọn file ngay",
                "Để sau");
            if (chooseNow)
            {
                BrowseGamePath();
            }
        }

        return false;
    }

    [RelayCommand]
    public async Task LaunchSelectedAccountsAsync(AccountItemViewModel? target = null)
    {
        var targets = GetTargetAccounts(target);
        if (targets.Count == 0)
        {
            _dialogService.ShowWarning("Thông báo", "Vui lòng chọn ít nhất một tài khoản để mở game!");
            return;
        }

        if (!EnsureGameClientReady(out string gamePath))
        {
            return;
        }

        var toLaunch = targets.Where(a => !a.IsRunning).ToList();
        if (toLaunch.Count == 0)
        {
            NotifyStatusMessage("Tất cả tài khoản được chọn đều đang mở game!");
            return;
        }

        int launched = 0;
        for (int i = 0; i < toLaunch.Count; i++)
        {
            var item = toLaunch[i];
            NotifyStatusMessage($"Đang mở game ({i + 1}/{toLaunch.Count}): {item.DisplayUsername}...");
            bool ok = await _gameEngine.StartAsync(item.Model, gamePath);
            if (ok) launched++;

            if (i < toLaunch.Count - 1)
            {
                await Task.Delay(Math.Max(1, _settingsService.Current.ReconnectDelaySeconds) * 1000);
            }
        }

        NotifyStatusMessage($"Đã mở thành công {launched}/{toLaunch.Count} tài khoản game.");
        NotifyCountsChanged();
    }

    [RelayCommand]
    public async Task LoginSelectedAccountsAsync(AccountItemViewModel? target = null)
    {
        var targets = GetTargetAccounts(target);
        if (targets.Count == 0)
        {
            _dialogService.ShowWarning("Thông báo", "Vui lòng chọn ít nhất một tài khoản để đăng nhập!");
            return;
        }

        if (!EnsureGameClientReady(out string gamePath))
        {
            return;
        }

        int successCount = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            var item = targets[i];
            if (string.IsNullOrWhiteSpace(item.Username) || string.IsNullOrWhiteSpace(item.Password))
            {
                NotifyStatusMessage($"Bỏ qua '{item.DisplayUsername}': Thiếu tên tài khoản hoặc mật khẩu.");
                continue;
            }

            NotifyStatusMessage($"Đang xử lý đăng nhập ({i + 1}/{targets.Count}): {item.DisplayUsername}...");

            bool ok;
            if (!item.IsRunning)
            {
                item.Model.AutoLogin = true;
                ok = await _gameEngine.StartAsync(item.Model, gamePath);
            }
            else
            {
                ok = await _gameEngine.LoginAsync(item.Id);
            }

            if (ok)
            {
                successCount++;
                NotifyStatusMessage($"[{item.DisplayUsername}]: Đăng nhập thành công!");
            }
            else
            {
                NotifyStatusMessage($"[{item.DisplayUsername}]: Đăng nhập không thành công.");
            }

            if (i < targets.Count - 1)
            {
                await Task.Delay(Math.Max(1, _settingsService.Current.ReconnectDelaySeconds) * 1000);
            }
        }

        NotifyStatusMessage($"Hoàn tất: {successCount}/{targets.Count} tài khoản đăng nhập thành công.");
        NotifyCountsChanged();
    }

    [RelayCommand]
    public async Task StopSelectedAccountsAsync(AccountItemViewModel? target = null)
    {
        var targets = GetTargetAccounts(target);
        if (targets.Count == 0)
        {
            _dialogService.ShowWarning("Thông báo", "Vui lòng chọn ít nhất một tài khoản để đóng game!");
            return;
        }

        var running = targets.Where(a => a.IsRunning || _gameEngine.IsRunning(a.Id)).ToList();
        if (running.Count == 0)
        {
            NotifyStatusMessage("Không có tài khoản nào đang chạy trong danh sách đã chọn.");
            return;
        }

        int stopped = 0;
        foreach (var item in running)
        {
            await _gameEngine.StopAsync(item.Id);
            stopped++;
        }

        NotifyStatusMessage($"Đã đóng {stopped} tài khoản game.");
        NotifyCountsChanged();
    }

    [RelayCommand]
    public async Task DeleteAccountAsync(AccountItemViewModel? target = null)
    {
        var item = target ?? SelectedAccount;
        if (item == null) return;

        if (item.IsRunning || _gameEngine.IsRunning(item.Id))
        {
            _dialogService.ShowWarning("Không thể xóa", $"Tài khoản '{item.DisplayUsername}' đang chạy game. Vui lòng đóng game trước khi xóa!");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation("Xác nhận xóa", $"Bạn có chắc chắn muốn xóa tài khoản '{item.DisplayUsername}'?");
        if (!confirm) return;

        await _storageService.DeleteAsync(item.Id);
        RemoveItem(item.Id);
        NotifyStatusMessage($"Đã xóa tài khoản {item.DisplayUsername}.");
    }

    [RelayCommand]
    public async Task DeleteSelectedAccountsAsync()
    {
        var targets = GetTargetAccounts();
        if (targets.Count == 0) return;

        var running = targets.Where(a => a.IsRunning || _gameEngine.IsRunning(a.Id)).ToList();
        if (running.Count > 0)
        {
            var names = string.Join(", ", running.Select(a => $"'{a.DisplayUsername}'"));
            _dialogService.ShowWarning("Không thể xóa", $"Có {running.Count} tài khoản đang chạy game ({names}). Vui lòng đóng game trước khi xóa!");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation("Xác nhận xóa", $"Bạn có chắc chắn muốn xóa {targets.Count} tài khoản đã chọn?");
        if (!confirm) return;

        foreach (var item in targets)
        {
            await _storageService.DeleteAsync(item.Id);
            RemoveItem(item.Id);
        }

        NotifyStatusMessage($"Đã xóa {targets.Count} tài khoản.");
    }

    [RelayCommand]
    public void FocusGameWindow(AccountItemViewModel? target = null)
    {
        var item = target ?? SelectedAccount;
        if (item != null && item.IsRunning)
        {
            _gameEngine.FocusWindow(item.Id);
        }
    }

    [RelayCommand]
    public void ArrangeWindows()
    {
        var running = Accounts.Where(a => a.IsRunning).ToList();
        if (running.Count == 0)
        {
            NotifyStatusMessage("Không có tài khoản nào đang mở game để sắp xếp!");
            _dialogService.ShowInfo("Sắp xếp cửa sổ", "Hiện tại không có tài khoản nào đang mở game.");
            return;
        }

        var selectedRunning = running.Where(a => a.IsSelected).ToList();
        var targets = selectedRunning.Count > 1 ? selectedRunning : running;

        _gameEngine.ArrangeWindows(targets.Select(a => a.Id));
        NotifyStatusMessage($"Đã sắp xếp {targets.Count} cửa sổ game.");
    }
}
