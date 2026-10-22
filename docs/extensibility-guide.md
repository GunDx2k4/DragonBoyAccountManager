# Cẩm Nang Mở Rộng Tính Năng (Extensibility Guide)

Nhờ kiến trúc được tổng quát hóa theo nguyên tắc **Open/Closed Principle (OCP)**, việc bổ sung tính năng mới vào **DBAM** được thực hiện bằng cách tạo lớp mới và đăng ký vào pipeline mà không cần sửa đổi logic điều phối cốt lõi.

---

## 1. Thêm Bộ Thu Thập Dữ Liệu Mới (`ISnapshotScraper<TSnapshot>`)

Khi cần đọc thêm dữ liệu từ tiến trình game (ví dụ: thông tin Bang hội, Đệ tử, Hành trang mở rộng):

1. **Khai báo định danh Mono (nếu có trường mới):**
   - Bổ sung tên lớp/trường vào tệp `GameSchema` tương ứng trong [`Interop/Mono/Schema/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Interop/Mono/Schema) (ví dụ: [`GameSchema.Entities.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Interop/Mono/Schema/GameSchema.Entities.cs)).
2. **Tạo kiểu bản ghi bất biến (`record`):**
   - Tạo `public sealed record ClanSnapshot(...)` trong thư mục [`Telemetry/Snapshots/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Telemetry/Snapshots).
3. **Hiện thực hóa `ISnapshotScraper<TSnapshot>`:**
   - Tạo lớp `ClanScraper : ISnapshotScraper<ClanSnapshot>` trong thư mục [`Telemetry/Scrapers/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Telemetry/Scrapers), triển khai phương thức `ClanSnapshot Scrape()`.
4. **Đăng ký vào phiên làm việc:**
   - Đăng ký mặc định trong hàm tạo [`GameSession`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/GameSession.cs#L98-L145), đăng ký toàn cục qua [`ISessionManager.SessionInitializer`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Abstractions/ISessionManager.cs#L11), hoặc đăng ký theo từng phiên qua [`AccountLaunchOptions.ConfigureSession`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Models/AccountLaunchOptions.cs#L18):
     ```csharp
     session.RegisterScraper(new ClanScraper(session.Mono));
     ```
5. **Truy xuất dữ liệu:**
   - Trong `IBehavior`: gọi `context.GetSnapshot<ClanSnapshot>()`.
   - Từ `GameSessionSnapshot` bất kỳ: gọi [`snapshot.Get<ClanSnapshot>()`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Telemetry/Snapshots/GameSessionSnapshot.cs#L15-L24).
   - Từ `IGameEngine` hoặc `IGameActionService`: gọi `engine.GetSnapshot<ClanSnapshot>(accountId)`.

---

## 2. Thêm Lệnh Điều Khiển Game Mới (`IGameCommand` & `IGameCommandHandler<TCommand>`)

Khi cần thêm một thao tác điều khiển trong game (ví dụ: nâng cấp trang bị, giao dịch, mở rương):

1. **Định nghĩa Command:**
   - Thêm `public sealed record UpgradeItemCommand(int ItemIndex) : IGameCommand { public string Name => "Nâng cấp vật phẩm"; }` vào [`GameCommands.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/Commands/GameCommands.cs) (hoặc tạo tệp riêng trong [`Execution/Commands/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/Commands)).
2. **Định nghĩa Handler:**
   - Tạo lớp xử lý cài đặt [`IGameCommandHandler<UpgradeItemCommand>`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/ICommandDispatcher.cs#L8-L11) trong [`Execution/Handlers/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/Handlers).
   - Triển khai `ExecuteAsync(UpgradeItemCommand command, CancellationToken cancellationToken = default)`.
3. **Đăng ký Handler:**
   - Đăng ký vào [`ICommandDispatcher`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/ICommandDispatcher.cs#L9) trong [`GameSession`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/GameSession.cs#L126-L138), qua `ISessionManager.SessionInitializer`, hoặc qua `AccountLaunchOptions.ConfigureSession`:
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
   - Tạo `public sealed record AutoBossOptions(...)`.
2. **Kế thừa lớp cơ sở `AutomationBehavior<TOptions>`:**
   - Tạo lớp `AutoBossBehavior : AutomationBehavior<AutoBossOptions>` trong [`Automation/Behaviors/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Automation/Behaviors).
   - Ghi đè `OnTickAsync(ISessionContext? context, CancellationToken ct)`:
     - Đọc trạng thái game qua `context.CurrentPlayer`, `context.CurrentWorld` hoặc `context.GetSnapshot<TSnapshot>()`.
     - Điều hướng bản đồ qua `context.Navigator.NavigateAsync(...)`.
     - Phát lệnh qua `context.Dispatcher.DispatchAsync(...)`.
3. **Đăng ký và cấu hình:**
   - Đăng ký trong [`GameSession`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/GameSession.cs#L141-L144), qua `ISessionManager.SessionInitializer`, hoặc qua `AccountLaunchOptions.ConfigureSession`:
     ```csharp
     session.RegisterBehavior(new AutoBossBehavior(new AutoBossOptions()));
     ```
   - Lưu cấu hình tùy chỉnh trên tài khoản mà không cần sửa cấu trúc lớp [`AccountAutoSettings`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Accounts/AccountAutoSettings.cs#L24-L42):
     ```csharp
     account.AutoSettings.SetBehaviorOptions(new AutoBossOptions(...));
     ```
   - Hoặc bật/tắt trực tiếp ở thời gian thực qua [`IGameEngine`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Abstractions/IGameEngine.cs#L31-L39) hoặc [`IGameAutomationService`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Services/Engine/IGameEngineService.cs#L50-L66):
     ```csharp
     engine.StartBehavior<AutoBossBehavior, AutoBossOptions>(accountId, options);
     ```

---

## 4. Thêm Tab Tính Năng Mới Trên GUI (`AccountAwareTabViewModel`)

Khi cần bổ sung một Tab chức năng mới trên cửa sổ chính của GUI:

1. **Tạo thư mục tính năng theo chiều dọc (Vertical Slice):**
   - Tạo thư mục mới trong `src/DragonBoyAccountManager.GUI/Presentation/Features/<FeatureName>/`.
2. **Tạo ViewModel kế thừa `AccountAwareTabViewModel`:**
   - Kế thừa [`AccountAwareTabViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Shell/AccountAwareTabViewModel.cs) để tự động nhận thông báo khi tài khoản đang chọn thay đổi (`OnSelectedAccountChanged`), khi Snapshot phiên của tài khoản đang chọn cập nhật (`OnActiveAccountSnapshotUpdated`), và khi người dùng chuyển vào/rời khỏi Tab (`OnTabActivated` / `OnTabDeactivated`).
3. **Tạo UserControl XAML tuân thủ Retro Pixel Design System:**
   - Sử dụng các tài nguyên màu sắc từ [`Colors.xaml`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/DesignSystem/Themes/Colors.xaml) và các control từ [`Presentation/DesignSystem/Controls/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/DesignSystem/Controls).
4. **Đăng ký vào DI Container và `MainViewModel`:**
   - Đăng ký ViewModel trong [`App.xaml.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/App.xaml.cs#L42-L52) và thêm vào từ điển `_tabs` trong [`MainViewModel.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Shell/MainViewModel.cs#L70-L75).

---

## 5. Thêm Tùy Chọn Cài Đặt Hệ Thống Mới (`SettingItemViewModel.Bind...`)

Khi cần thêm một mục cấu hình mới vào Tab Cài đặt (`Settings`):

1. **Thêm thuộc tính vào `AppSettings`:**
   - Bổ sung thuộc tính cấu hình mới kèm giá trị mặc định trong [`AppSettings.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Domain/Settings/AppSettings.cs).
2. **Khai báo mục cài đặt trong `SettingsTabViewModel`:**
   - Trong [`SettingsTabViewModel.InitializeDefaultGroups`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Settings/SettingsTabViewModel.cs#L53-L134), thêm mục mới bằng một trong ba phương thức fluent khai báo hai chiều:
     - `SettingItemViewModel.CreateBoolean(key, label, cur.MyFlag, description).BindBool(s => s.MyFlag, (s, v) => s.MyFlag = v)`
     - `SettingItemViewModel.CreateNumber(key, label, cur.MyNumber, min, max, unit, description).BindNumber(s => s.MyNumber, (s, v) => s.MyNumber = v)`
     - `SettingItemViewModel.CreateSelect(key, label, defaultLabel, labels, description).BindSelect(choices, s => s.MyMode, (s, v) => s.MyMode = v)`
   - Không cần viết thêm bất kỳ mã đồng bộ hay `switch-case` nào khác; thao tác nạp và lưu tự động sẽ hoạt động ngay lập tức.
