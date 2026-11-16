# Kiến Trúc Phân Hệ GUI (`DragonBoyAccountManager.GUI`)

Phân hệ [`DragonBoyAccountManager.GUI`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI) là ứng dụng giao diện người dùng xây dựng trên nền tảng **WPF (.NET 10 x64)** kết hợp mô hình **MVVM (`CommunityToolkit.Mvvm`)**, **Dependency Injection (`Microsoft.Extensions.DependencyInjection`)** và hệ thống thiết kế thuần XAML **NRO Retro Pixel Design System**.

---

## 1. Cấu Trúc Thư Mục & Phân Tầng

GUI được tổ chức thành 3 tầng rõ ràng và tuân thủ tuyệt đối quy tắc **1:1 Folder-to-Namespace Alignment**:

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
│   │   ├── IGameEngineService.cs               # Hợp nhất các dịch vụ engine Core và quản lý cửa sổ
│   │   ├── CoreGameEngineAdapter.cs            # Adapter kết nối IGameEngine của Core & quản lý cửa sổ Win32
│   │   ├── IGameEnvironmentService.cs          # Quản lý môi trường & đường dẫn GameClient
│   │   ├── GameEnvironmentService.cs
│   │   ├── IGameImageService.cs                # Trích xuất & lưu đệm biểu tượng từ dữ liệu client game
│   │   └── GameImageService.cs
│   ├── Feedback/                               # IFeedbackService, DiscordFeedbackService
│   ├── Input/                                  # IGameInputService, GameInputService, IHotkeyRoutingService, HotkeyRoutingService
│   ├── Selection/                              # Quản lý trạng thái lựa chọn tài khoản tập trung
│   │   ├── IActiveSelectionService.cs          # Single Source of Truth cho SelectedAccount & CheckedAccounts
│   │   └── ActiveSelectionService.cs
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
    │   ├── AccountTargetedViewModelBase.cs     # Lớp cơ sở chọn tập tài khoản mục tiêu (ActiveAccount / SyncTargets)
    │   ├── AccountAwareTabViewModel.cs         # Lớp cơ sở tự động đồng bộ tài khoản đang chọn & vòng đời Tab
    │   ├── Dialogs/                            # GameDialogWindow, GameDialogViewModel, HotkeyCheatsheetWindow
    │   ├── StatusBar/                          # AppStatusBarView, AppStatusBarViewModel
    │   └── TitleBar/                           # AppTitleBarView, AppTitleBarViewModel
    └── Features/                               # Các phân hệ tính năng độc lập theo chiều dọc (Vertical Slices)
        ├── Accounts/                           # Quản lý danh sách và cấu hình tài khoản
        ├── Auto/                               # Tự động hóa (Luyện tập, Săn Boss, Tự chat)
        │   ├── AutoTabView.xaml / .cs          # View giao diện tổng hợp các tab tự động
        │   ├── AutoTabViewModel.cs             # ViewModel điều phối chung các tính năng Auto
        │   ├── AutoTrainViewModel.cs           # Facade ViewModel điều phối luyện tập
        │   ├── AutoTrainViewModel.Skills.cs    # Partial: Quản lý 10 skill slots, cooldown timer 100ms
        │   ├── AutoTrainViewModel.Mobs.cs      # Partial: Quản lý danh sách quái lọc theo map, dọn dẹp event
        │   ├── AutoTrainViewModel.Items.cs     # Partial: Quản lý vật phẩm tự dùng và bùa
        │   ├── AutoTrainViewModel.Pick.cs      # Partial: Quản lý preset nhặt đồ và chuỗi ID bộ lọc
        │   ├── AutoBossViewModel.cs            # ViewModel cấu hình săn Boss
        │   ├── AutoChatViewModel.cs            # ViewModel cấu hình tự chat
        │   └── Items/                          # SkillButtonItem, MobFilterItem, AutoItemSlotItem
        ├── Feedback/                           # FeedbackTabView / FeedbackTabViewModel
        ├── GameConfig/                         # GameConfigTabView / GameConfigTabViewModel
        └── Guide/                              # GuideTabView / GuideTabViewModel
```

---

## 2. Các Nguyên Tắc Kiến Trúc & Thiết Kế GUI

### 2.1. Phân Tách ViewModel Theo Miền Nghiệp Vụ (Facade + Partial Classes)
- [`AutoTrainViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTrainViewModel.cs) được phân rã thành các partial classes chuyên trách:
  - [`AutoTrainViewModel.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTrainViewModel.cs): Facade trung gian quản lý vòng đời (`OnActivated`, `OnDeactivated`, `OnSnapshotUpdated`), timer debounce lưu trữ và điều phối bật/tắt hành vi.
  - [`AutoTrainViewModel.Skills.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTrainViewModel.Skills.cs): Quản lý 10 nút kỹ năng, timer cooldown 100ms, đồng bộ icon và lệnh kích hoạt skill.
  - [`AutoTrainViewModel.Mobs.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTrainViewModel.Mobs.cs): Quản lý danh sách quái lọc theo map, dọn dẹp event handler chống memory leak khi đổi khu/map.
  - [`AutoTrainViewModel.Items.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTrainViewModel.Items.cs): Quản lý vật phẩm tự dùng và bùa, đếm số lượng từ kho đồ.
  - [`AutoTrainViewModel.Pick.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTrainViewModel.Pick.cs): Quản lý preset nhặt đồ và chuỗi ID bộ lọc nhặt/vứt.
- **Bảo toàn 100% Binding XAML:** Không làm vỡ bất kỳ binding nào trong [`AutoTabView.xaml`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTabView.xaml), không sinh thêm tầng forwarder hay wrapper trung gian.

### 2.2. An Toàn Luồng UI Dispatcher & Chống Rò Rỉ Bộ Nhớ
- Toàn bộ thao tác cập nhật `ObservableCollection` chỉ được phép diễn ra trên UI Thread (`RunOnUi`).
- Mọi timer chu kỳ (`DispatcherTimer`) tự động dừng (`Stop()`) khi tab bị ẩn (`OnDeactivated()`) hoặc tài khoản offline (`OnAccountOffline()`), triệt tiêu CPU ngầm.
- Hủy đăng ký sự kiện `PropertyChanged` triệt để trước khi xóa phần tử khỏi bộ sưu tập (`ClearAvailableMobTypes`).

### 2.3. Hệ Thống Thiết Kế Thuần XAML (Retro Pixel Design System)
- Giao diện được định kiểu 100% bằng WPF XAML vector/path và styles tái sử dụng tại `Presentation/DesignSystem/Themes/`.
- Không sử dụng thư viện UI bên ngoài (không nạp MaterialDesignXAML hay MahApps.Metro).
