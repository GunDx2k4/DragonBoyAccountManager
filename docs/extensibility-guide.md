# Cẩm Nang Mở Rộng Tính Năng (Extensibility Guide)

Nhờ kiến trúc được tổng quát hóa theo nguyên tắc **Open/Closed Principle (OCP)**, việc bổ sung tính năng mới vào **DBAM** được thực hiện bằng cách tạo lớp mới và đăng ký vào pipeline mà không cần sửa đổi logic điều phối cốt lõi.

---

## 1. Thêm Bộ Thu Thập Dữ Liệu Mới (`ISnapshotScraper<TSnapshot>`)

Khi cần đọc thêm dữ liệu từ tiến trình game (ví dụ: thông tin Bang hội, Đệ tử, Hành trang mở rộng):

1. **Khai báo định danh IL2CPP / Schema (nếu có trường mới):**
   - Bổ sung tên lớp/trường vào tệp `GameSchema` tương ứng trong [`Interop/Schema/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Interop/Schema) (ví dụ: [`GameSchema.Entities.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Interop/Schema/GameSchema.Entities.cs)) và cập nhật RVA trong `Offsets.json`.
2. **Tạo kiểu bản ghi bất biến (`record`):**
   - Tạo `public sealed record ClanSnapshot(...)` trong thư mục [`Telemetry/Snapshots/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Telemetry/Snapshots).
3. **Hiện thực hóa `ISnapshotScraper<TSnapshot>`:**
   - Tạo lớp `ClanScraper : ISnapshotScraper<ClanSnapshot>` trong thư mục [`Telemetry/Scrapers/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Telemetry/Scrapers), triển khai phương thức `ClanSnapshot Scrape()`.
4. **Đăng ký vào phiên làm việc:**
   - Đăng ký mặc định trong [`DefaultSessionBootstrap.Configure`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/DefaultSessionBootstrap.cs), đăng ký toàn cục qua [`ISessionManager.SessionInitializer`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Abstractions/ISessionManager.cs), hoặc đăng ký theo từng phiên qua [`AccountLaunchOptions.ConfigureSession`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Models/AccountLaunchOptions.cs):
     ```csharp
     session.RegisterScraper(new ClanScraper(session.Runtime));
     ```
5. **Truy xuất dữ liệu:**
   - Trong `IBehavior`: gọi `context.GetSnapshot<ClanSnapshot>()`.
   - Từ `GameSessionSnapshot` bất kỳ: gọi [`snapshot.Get<ClanSnapshot>()`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Telemetry/Snapshots/GameSessionSnapshot.cs).
   - Từ `IGameEngine` hoặc `IGameActionService`: gọi `engine.GetSnapshot<ClanSnapshot>(accountId)`.

---

## 2. Thêm Lệnh Điều Khiển Game Mới (`IGameCommand` & `IGameCommandHandler<TCommand>`)

Khi cần thêm một thao tác điều khiển trong game (ví dụ: nâng cấp trang bị, giao dịch, mở rương):

1. **Định nghĩa Command:**
   - Thêm `public sealed record UpgradeItemCommand(int ItemIndex) : IGameCommand { public string Name => "Nâng cấp vật phẩm"; }` vào tệp miền tương ứng trong [`Execution/Commands/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/Commands) (ví dụ: [`InventoryCommands.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/Commands/InventoryCommands.cs)).
2. **Định nghĩa Handler:**
   - Tạo lớp xử lý cài đặt [`IGameCommandHandler<UpgradeItemCommand>`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/ICommandDispatcher.cs) trong [`Execution/Handlers/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/Handlers).
   - Triển khai `HandleAsync(UpgradeItemCommand command, CancellationToken cancellationToken = default)`.
3. **Đăng ký Handler:**
   - Đăng ký vào [`ICommandDispatcher`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/ICommandDispatcher.cs) trong [`DefaultSessionBootstrap.Configure`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/DefaultSessionBootstrap.cs), qua `ISessionManager.SessionInitializer`, hoặc qua `AccountLaunchOptions.ConfigureSession`:
     ```csharp
     session.Dispatcher.RegisterHandler<UpgradeItemCommand>(handler);
     ```
4. **Gửi lệnh từ GUI hoặc Behavior:**
   - Gọi `await engine.ExecuteCommandAsync(accountId, new UpgradeItemCommand(0), ct)` hoặc `await context.Dispatcher.DispatchAsync(new UpgradeItemCommand(0), ct)`.
   - Nhờ cơ chế phân phối đa hình theo kiểu thực tế (`command.GetType()`), lệnh có thể được truyền dưới dạng biến `IGameCommand` tổng quát mà không cần ép kiểu.

---

## 3. Thêm Hành Vi Tự Động Hóa Mới (`IBehavior<TOptions>`)

Khi cần phát triển một tính năng tự động hóa chạy theo chu kỳ Tick (ví dụ: Tự động làm nhiệm vụ, Tự động săn Boss):

1. **Định nghĩa cấu hình `TOptions`:**
   - Tạo `public sealed record AutoBossOptions(...)` trong thư mục miền tương ứng của [`Automation/Behaviors/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Automation/Behaviors).
2. **Kế thừa lớp cơ sở `AutomationBehavior<TOptions>`:**
   - Tạo lớp `AutoBossBehavior : AutomationBehavior<AutoBossOptions>` trong [`Automation/Behaviors/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Automation/Behaviors).
   - Ghi đè `OnTickAsync(ISessionContext context, IProgress<string>? progress, CancellationToken cancellationToken)`:
     - Đọc trạng thái game qua `context.CurrentPlayer`, `context.CurrentWorld` hoặc `context.GetSnapshot<TSnapshot>()`.
     - Điều hướng bản đồ qua `context.Navigator.NavigateAsync(...)`.
     - Phát lệnh qua `context.Dispatcher.DispatchAsync(...)`.
   - Ghi đè `OnOptionsChanged(AutoBossOptions options)` hoặc `OnStopped()` nếu cần đồng bộ trạng thái in-game thông qua `CurrentContext`.
3. **Đăng ký và cấu hình:**
   - Đăng ký trong [`DefaultSessionBootstrap.Configure`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/DefaultSessionBootstrap.cs), qua `ISessionManager.SessionInitializer`, hoặc qua `AccountLaunchOptions.ConfigureSession`:
     ```csharp
     session.RegisterBehavior(new AutoBossBehavior());
     ```
   - Lưu và đọc cấu hình tùy chỉnh trên tài khoản thông qua từ điển tổng quát trong [`AccountAutoSettings`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Accounts/AccountAutoSettings.cs):
     ```csharp
     var options = account.AutoSettings.GetBehaviorOptionsOrDefault<AutoBossOptions>(AutoBossBehavior.BehaviorId);
     account.AutoSettings.SetBehaviorOptions(AutoBossBehavior.BehaviorId, options);
     ```
   - Hoặc bật/tắt trực tiếp ở thời gian thực qua [`IGameAutomationService`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Abstractions/IGameAutomationService.cs):
     ```csharp
     engine.StartBehavior<AutoBossBehavior, AutoBossOptions>(accountId, options);
     ```

---

## 4. Mở Rộng Giao Diện Người Dùng Trên GUI (`AccountAwareTabViewModel` & `AutoFeatureViewModelBase`)

### 4.1. Thêm Tính Năng Tự Động Hóa Con Trong Tab Auto (`AutoFeatureViewModelBase`)
1. Tạo lớp `AutoBossViewModel` kế thừa [`AutoFeatureViewModelBase`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/IAutoFeatureViewModel.cs) trong [`Presentation/Features/Auto/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto).
2. Cài đặt `BehaviorId`, `LoadFromAccount(AccountItemViewModel?)` và `StopOnEngine(Guid)`.
3. Đăng ký vào [`AutoTabViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTabViewModel.cs) và thêm thẻ cấu hình tương ứng vào [`AutoTabView.xaml`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTabView.xaml).

### 4.2. Thêm Tab Chức Năng Mới Trên Cửa Sổ Chính (`AccountAwareTabViewModel`)
1. Tạo thư mục mới trong `src/DragonBoyAccountManager.GUI/Presentation/Features/<FeatureName>/`.
2. Tạo `<FeatureName>TabViewModel` kế thừa [`AccountAwareTabViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Shell/AccountAwareTabViewModel.cs) và `<FeatureName>TabView.xaml` tuân thủ Retro Pixel Design System.
3. Đăng ký ViewModel trong [`App.xaml.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/App.xaml.cs) và thêm vào từ điển `_tabs` trong [`MainViewModel.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Shell/MainViewModel.cs).

---

## 5. Thêm Tùy Chọn Cài Đặt Hệ Thống Mới (`SettingItemViewModel.Bind...`)

Khi cần thêm một mục cấu hình mới vào Tab Cài đặt (`Settings`):

1. **Thêm thuộc tính vào `AppSettings`:**
   - Bổ sung thuộc tính cấu hình mới kèm giá trị mặc định trong [`AppSettings.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Domain/Settings/AppSettings.cs).
2. **Khai báo mục cài đặt trong `SettingsTabViewModel`:**
   - Trong [`SettingsTabViewModel.InitializeDefaultGroups`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Settings/SettingsTabViewModel.cs), thêm mục mới bằng một trong ba phương thức fluent khai báo hai chiều:
     - `SettingItemViewModel.CreateBoolean(key, label, cur.MyFlag, description).BindBool(s => s.MyFlag, (s, v) => s.MyFlag = v)`
     - `SettingItemViewModel.CreateNumber(key, label, cur.MyNumber, min, max, unit, description).BindNumber(s => s.MyNumber, (s, v) => s.MyNumber = v)`
     - `SettingItemViewModel.CreateSelect(key, label, defaultLabel, labels, description).BindSelect(choices, s => s.MyMode, (s, v) => s.MyMode = v)`
   - Không cần viết thêm bất kỳ mã đồng bộ hay `switch-case` nào khác; thao tác nạp và lưu tự động sẽ hoạt động ngay lập tức.
