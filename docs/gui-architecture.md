# Kiến Trúc Phân Hệ GUI (`DragonBoyAccountManager.GUI`)

Phân hệ [`DragonBoyAccountManager.GUI`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI) là ứng dụng giao diện người dùng xây dựng trên nền tảng **WPF (.NET 10 x64)** kết hợp mô hình **MVVM (`CommunityToolkit.Mvvm`)**, **Dependency Injection (`Microsoft.Extensions.DependencyInjection`)** và hệ thống thiết kế thuần XAML **NRO Retro Pixel Design System**.

---

## 1. Cấu Trúc Thư Mục & Phân Tầng 3 Lớp Tinh Gọn

Sau khi loại bỏ các lớp trung gian dư thừa (YAGNI) và chuẩn hóa hậu tố đồng bộ `View` $\leftrightarrow$ `ViewModel`, GUI được tổ chức thành 3 tầng rõ ràng và tuân thủ tuyệt đối quy tắc **1:1 Folder-to-Namespace Alignment**:

```text
src/DragonBoyAccountManager.GUI/
├── App.xaml / App.xaml.cs                      # Composition Root & DI Container Registration
├── GlobalUsings.cs                             # Khai báo namespace dùng chung toàn ứng dụng GUI
├── Domain/                                     # Mô hình dữ liệu, thông điệp & cấu hình cốt lõi của GUI
│   ├── Feedback/                               # FeedbackModels, FeedbackTypeOption
│   ├── Messaging/                              # Messages (SessionSnapshotUpdatedMessage, RequestNavigateToTabMessage...)
│   ├── Settings/                               # AppSettings, SettingViewType, WindowArrangeMode, WindowArrangeOptions
│   └── Updates/                                # AppUpdateInfo
├── Services/                                   # Tầng dịch vụ ứng dụng, Adapter & Hạ tầng hệ điều hành
│   ├── Dialogs/                                # IDialogService, DialogService, DialogSeverity
│   ├── Dispatching/                            # IDispatcherService, WpfDispatcherService
│   ├── Engine/                                 # Hợp đồng Desktop & Adapter kết nối với DragonBoyAccountManager.Core
│   │   ├── ISessionManagementService.cs        # Quản lý vòng đời mở/tắt phiên từ Desktop Host
│   │   ├── IGameWindowControlService.cs        # Quản lý cửa sổ Win32 (Focus, Arrange, Resolution, Masking)
│   │   ├── IGameEngineService.cs               # Hợp nhất IGameActionService, IGameAutomationService, IGameEngineEvents, ISessionManagementService, IGameWindowControlService
│   │   ├── CoreGameEngineAdapter.cs            # Adapter kết nối IGameEngine của Core & quản lý cửa sổ Win32
│   │   ├── IGameEnvironmentService.cs          # Quản lý môi trường & đường dẫn GameClient
│   │   ├── GameEnvironmentService.cs
│   │   ├── IGameImageService.cs                # Trích xuất & lưu đệm biểu tượng từ dữ liệu client game
│   │   └── GameImageService.cs
│   ├── Feedback/                               # IFeedbackService, DiscordFeedbackService
│   ├── Input/                                  # IGameInputService, GameInputService, IHotkeyRoutingService, HotkeyRoutingService
│   ├── Selection/                              # Quản lý trạng thái lựa chọn tài khoản tập trung
│   │   ├── IActiveSelectionService.cs          # Single Source of Truth cho SelectedAccount & CheckedAccounts
│   │   └── ActiveSelectionStore.cs
│   ├── Settings/                               # ISettingsService, SettingsService
│   ├── Updater/                                # IAppUpdaterService, AppUpdaterService
│   └── Windowing/                              # IWindowManagerService, WindowManagerService, User32
└── Presentation/                               # Tầng hiển thị MVVM & Retro Pixel XAML
    ├── DesignSystem/                           # Hệ thống thiết kế Retro Pixel & Custom Controls
    │   ├── Assists/                            # MarkdownAssist, ObservableCollectionExtensions
    │   ├── Controls/                           # GameButton, GameCard, GameBadge, GameStatusOrb, GameTextBox, GamePasswordBox
    │   ├── Converters/                         # BooleanToVisibilityConverter, StatusToBrushConverter, ClientStatusToBrushConverter
    │   └── Themes/                             # Colors.xaml, Buttons.xaml, Inputs.xaml, DataGrids.xaml, Dialogs.xaml, Theme.xaml
    ├── Shell/                                  # Khung cửa sổ chính & Hợp đồng vòng đời Tab tổng quát
    │   ├── MainWindow.xaml / .cs               # Cửa sổ khung chứa chính
    │   ├── MainViewModel.cs                    # Điều phối các Tab thông qua IActivatableTab
    │   ├── IActivatableTab.cs                  # Hợp đồng vòng đời kích hoạt / hủy kích hoạt Tab
    │   ├── AccountAwareTabViewModel.cs         # Lớp cơ sở tự động đồng bộ tài khoản đang chọn & vòng đời Tab
    │   ├── Dialogs/                            # GameDialogWindow, GameDialogViewModel, HotkeyCheatsheetWindow
    │   ├── StatusBar/                          # AppStatusBarView, AppStatusBarViewModel
    │   └── TitleBar/                           # AppTitleBarView, AppTitleBarViewModel
    └── Features/                               # Các phân hệ tính năng độc lập theo chiều dọc (Vertical Slices)
        ├── Accounts/                           # AccountsPanelView / AccountsPanelViewModel, AccountConfigTabView / AccountConfigTabViewModel (.Controls.cs), Items/AccountItemViewModel
        ├── Auto/                               # AutoTabView / AutoTabViewModel, IAutoFeatureViewModel (AutoFeatureViewModelBase), AutoTrainViewModel, AutoChatViewModel, Items/
        ├── Feedback/                           # FeedbackTabView / FeedbackTabViewModel
        ├── Guide/                              # GuideTabView / GuideTabViewModel
        ├── MapUtilities/                       # MapUtilitiesTabView / MapUtilitiesTabViewModel (.Actions.cs), Items/WaypointButtonItem
        └── Settings/                           # SettingsTabView / SettingsTabViewModel, Items/ (SettingGroupViewModel, SettingItemViewModel)
```

---

## 2. Năm Trụ Cột Thiết Kế Trong `DragonBoyAccountManager.GUI`

### 2.1. Kế Thừa Trực Tiếp Giao Diện ISP Từ Core (`Services/Engine/`)
Thay vì khai báo trùng lặp các giao diện và phương thức mở rộng của Core, [`IGameEngineService.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Services/Engine/IGameEngineService.cs) kế thừa trực tiếp 3 giao diện vai trò từ Core và 2 giao diện quản lý cấp Desktop:
- [`IGameActionService`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Abstractions/IGameActionService.cs) *(từ Core)*: Thực thi lệnh `ExecuteCommandAsync`, truy xuất `GetSnapshot<TSnapshot>`, lấy danh sách kỹ năng `GetSkills`, điều chỉnh tốc độ game, gửi phím, tìm đường `AutoMoveAsync` và đi qua cổng `EnterWaypointAsync`.
- [`IGameAutomationService`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Abstractions/IGameAutomationService.cs) *(từ Core)*: Bật, tắt và cập nhật cấu hình động cho mọi `IBehavior<TOptions>` (`StartBehavior`, `UpdateBehaviorOptions`, `StopBehavior`, `IsBehaviorRunning`).
- [`IGameEngineEvents`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Abstractions/IGameEngineEvents.cs) *(từ Core)*: Phát sự kiện `OnSessionEvent`.
- [`ISessionManagementService`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Services/Engine/ISessionManagementService.cs) *(Desktop Host)*: Khởi chạy (`StartAsync`), dừng tiến trình (`StopAsync`, `StopAllAsync`), kiểm tra trạng thái (`IsRunning`) và đăng nhập (`LoginAsync`).
- [`IGameWindowControlService`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Services/Engine/IGameWindowControlService.cs) *(Desktop Host)*: Đưa cửa sổ lên trước (`FocusWindow`), sắp xếp lưới cửa sổ (`ArrangeWindows`), đổi độ phân giải (`SetResolution`) và bật/tắt chế độ ẩn danh (`SetMasked`).
- Toàn bộ phương thức mở rộng tiện ích trong [`GameEngineExtensions.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Abstractions/GameEngineExtensions.cs) của Core tự động khả dụng trên cả `IGameEngine` lẫn `IGameEngineService` mà không cần bất kỳ lớp wrapper trùng lặp nào ở GUI.

### 2.2. Nguồn Sự Thật Duy Nhất Cho Trạng Thái Chọn Tài Khoản (`IActiveSelectionService`)
- [`IActiveSelectionService`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Services/Selection/IActiveSelectionService.cs) và [`ActiveSelectionStore`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Services/Selection/ActiveSelectionStore.cs) đóng vai trò là **Single Source of Truth** cho danh sách `Accounts`, tài khoản đang được chọn (`SelectedAccount`) và tập hợp các tài khoản mục tiêu (`GetTargetAccounts`).
- Trạng thái vận hành của từng Behavior trên mỗi tài khoản được quản lý tổng quát qua [`AccountItemViewModel.GetBehaviorState(behaviorId)`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Accounts/Items/AccountItemViewModel.cs).

### 2.3. Tổng Quát Hóa Vòng Đời Tab (`IActivatableTab`, `AccountAwareTabViewModel` & `IAutoFeatureViewModel`)
- **Hợp đồng kích hoạt Tab ([`IActivatableTab`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Shell/IActivatableTab.cs)):**
  - Định nghĩa `OnTabActivated()` và `OnTabDeactivated()`.
  - [`MainViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Shell/MainViewModel.cs) điều phối các Tab hoàn toàn đa hình thông qua từ điển `IReadOnlyDictionary<AppTab, IActivatableTab>` mà không cần biết chi tiết bên trong từng Tab.
- **Lớp cơ sở nhận biết tài khoản ([`AccountAwareTabViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Shell/AccountAwareTabViewModel.cs)):**
  - Tự động đăng ký sự kiện `SelectedAccountChanged` từ [`IActiveSelectionService`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Services/Selection/IActiveSelectionService.cs) và nhận thông điệp [`SessionSnapshotUpdatedMessage`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Domain/Messaging/Messages.cs) qua `WeakReferenceMessenger`.
  - Các Tab [`AccountConfigTabViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Accounts/AccountConfigTabViewModel.cs), [`MapUtilitiesTabViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/MapUtilities/MapUtilitiesTabViewModel.cs) và [`AutoTabViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTabViewModel.cs) kế thừa trực tiếp từ lớp này, chỉ làm mới dữ liệu khi Tab đang hiển thị (`IsTabActive == true`).
- **Tách biệt các tính năng tự động hóa con ([`IAutoFeatureViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/IAutoFeatureViewModel.cs)):**
  - [`AutoTabViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTabViewModel.cs) đóng vai trò điều phối tập hợp `IReadOnlyList<IAutoFeatureViewModel>` ([`AutoTrainViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTrainViewModel.cs), [`AutoChatViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoChatViewModel.cs)) kế thừa từ [`AutoFeatureViewModelBase`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/IAutoFeatureViewModel.cs), giúp việc thêm tính năng Auto mới độc lập hoàn toàn với các tính năng hiện có.

### 2.4. Ràng Buộc Cài Đặt Hai Chiều Khai Báo (`Declarative Two-Way Settings Binding`)
- [`SettingItemViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Settings/Items/SettingItemViewModel.cs) hỗ trợ khai báo trực tiếp biểu thức đọc/ghi (`Func<AppSettings, T>` getter và `Action<AppSettings, T>` setter) thông qua chuỗi phương thức fluent:
  - `SettingItemViewModel.CreateBoolean(...).BindBool(getter, setter)`
  - `SettingItemViewModel.CreateNumber(...).BindNumber(getter, setter)`
  - `SettingItemViewModel.CreateSelect(...).BindSelect(choices, getter, setter)`
- Khi nạp hoặc lưu cài đặt trong [`SettingsTabViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Settings/SettingsTabViewModel.cs), mỗi mục tự động đồng bộ hai chiều với [`AppSettings`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Domain/Settings/AppSettings.cs) qua `LoadFromSettings` và `ApplyToSettings` mà không cần viết các khối `switch-case` thủ công theo chuỗi khóa.

### 2.5. An Toàn Luồng Giao Diện (`IDispatcherService`) & Hệ Thống Thiết Kế Retro Pixel XAML
- **An toàn luồng tuyệt đối:**
  - Mọi cập nhật trạng thái từ luồng nền lên các `ObservableCollection` hoặc thuộc tính ràng buộc dữ liệu trong ViewModel đều đi qua [`IDispatcherService`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Services/Dispatching/IDispatcherService.cs) ([`WpfDispatcherService`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Services/Dispatching/WpfDispatcherService.cs)).
  - Việc lấy danh sách kỹ năng nhân vật trong [`CoreGameEngineAdapter.GetSkills`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Services/Engine/CoreGameEngineAdapter.cs) đọc trực tiếp từ `PlayerSnapshot.SkillList` trong bộ nhớ đệm, đảm bảo không bao giờ phát sinh syscall liên tiến trình trên luồng UI.
- **NRO Retro Pixel Design System ([`Presentation/DesignSystem/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/DesignSystem)):**
  - Toàn bộ giao diện sử dụng bảng màu chuẩn hóa trong [`Colors.xaml`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/DesignSystem/Themes/Colors.xaml) và hệ thống control tùy chỉnh ([`GameButton`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/DesignSystem/Controls/GameButton.cs), [`GameCard`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/DesignSystem/Controls/GameCard.cs), [`GameBadge`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/DesignSystem/Controls/GameBadge.cs), [`GameStatusOrb`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/DesignSystem/Controls/GameStatusOrb.cs)) được định kiểu tại [`Themes/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/DesignSystem/Themes).
