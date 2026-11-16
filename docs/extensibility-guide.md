# Cẩm Nang Mở Rộng Tính Năng (Extensibility Guide)

Khi bổ sung tính năng mới vào **DBAM**, tạo lớp mới và đăng ký vào pipeline mà không cần sửa đổi logic điều phối cốt lõi.

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
   - Đăng ký mặc định trong [`DefaultSessionBootstrap.Configure`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/DefaultSessionBootstrap.cs), hoặc đăng ký qua `AccountLaunchOptions.ConfigureSession`:
     ```csharp
     session.RegisterScraper(new ClanScraper(session.Runtime));
     ```
5. **Truy xuất dữ liệu:**
   - Trong Behavior: gọi `context.GetSnapshot<ClanSnapshot>()`.
   - Từ `GameSessionSnapshot`: gọi [`snapshot.Get<ClanSnapshot>()`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Telemetry/Snapshots/GameSessionSnapshot.cs).
   - Từ `IGameEngine`: gọi `engine.GetSnapshot<ClanSnapshot>(accountId)`.

---

## 2. Thêm Lệnh Điều Khiển Game Mới (`IGameCommand` & `IGameCommandHandler<TCommand>`)

Khi cần thêm một thao tác điều khiển trong game (ví dụ: nâng cấp trang bị, giao dịch, mở rương):

1. **Định nghĩa Command:**
   - Thêm `public sealed record UpgradeItemCommand(int ItemIndex) : IGameCommand { public string Name => "Nâng cấp vật phẩm"; }` vào tệp miền tương ứng trong [`Execution/Commands/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/Commands) (ví dụ: [`InventoryCommands.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/Commands/InventoryCommands.cs)).
2. **Định nghĩa Handler:**
   - Tạo lớp xử lý cài đặt [`IGameCommandHandler<UpgradeItemCommand>`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/ICommandDispatcher.cs) trong [`Execution/Handlers/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/Handlers).
   - Triển khai `HandleAsync(UpgradeItemCommand command, CancellationToken cancellationToken = default)`.
3. **Đăng ký Handler:**
   - Đăng ký vào [`ICommandDispatcher`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/ICommandDispatcher.cs) trong [`DefaultSessionBootstrap.Configure`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/DefaultSessionBootstrap.cs):
     ```csharp
     session.Dispatcher.RegisterHandler<UpgradeItemCommand>(handler);
     ```
4. **Gửi lệnh từ GUI hoặc Behavior:**
   - Gọi `await engine.ExecuteCommandAsync(accountId, new UpgradeItemCommand(0), ct)` hoặc `await context.Dispatcher.DispatchAsync(new UpgradeItemCommand(0), ct)`.

---

## 3. Thêm Hành Vi Tự Động Hóa Mới (`IBehavior<TOptions>`)

Khi phát triển tính năng tự động hóa chạy theo chu kỳ Tick:

1. **Định nghĩa cấu hình `TOptions`:**
   - Tạo `public sealed record AutoQuestOptions(...)` trong thư mục miền của [`Automation/Behaviors/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Automation/Behaviors).
2. **Kế thừa lớp cơ sở `AutomationBehavior<TOptions>` và tái sử dụng Shared Coordinators:**
   - Tạo lớp `AutoQuestBehavior : AutomationBehavior<AutoQuestOptions>` trong [`Automation/Behaviors/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Automation/Behaviors).
   - Tái sử dụng các Coordinator có sẵn ([`GroundLootCoordinator`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Automation/Coordinators/GroundLootCoordinator.cs), [`RecoveryCoordinator`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Automation/Coordinators/RecoveryCoordinator.cs)) thay vì viết lại logic nhặt đồ hoặc hồi máu.
   - Ghi đè `OnTickAsync(ISessionContext context, IProgress<string>? progress, CancellationToken cancellationToken)`:
     - Đọc trạng thái qua `context.CurrentPlayer`, `context.CurrentWorld`.
     - Điều phối lệnh qua `context.Dispatcher.DispatchAsync(...)`.
3. **Đăng ký và lưu trữ:**
   - Đăng ký trong [`DefaultSessionBootstrap.Configure`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/DefaultSessionBootstrap.cs):
     ```csharp
     session.RegisterBehavior(new AutoQuestBehavior(new AutoQuestOptions()));
     ```
   - Bật/tắt qua [`IGameAutomationService`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Abstractions/IGameAutomationService.cs):
     ```csharp
     engine.StartBehavior<AutoQuestBehavior, AutoQuestOptions>(accountId, options);
     ```

---

## 4. Mở Rộng Giao Diện Người Dùng Trên GUI

### 4.1. Thêm Tính Năng Tự Động Hóa Vào Tab Auto (`AutoFeatureViewModelBase`)
1. Tạo lớp `AutoQuestViewModel` kế thừa [`AutoFeatureViewModelBase`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/IAutoFeatureViewModel.cs) trong [`Presentation/Features/Auto/`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto).
2. Khi ViewModel có nhiều phần tử nghiệp vụ phức tạp, tách thành `partial class` theo miền (ví dụ: `.Skills.cs`, `.Mobs.cs`, `.Items.cs`) để giữ ViewModels gọn gàng và giữ nguyên data-binding XAML.
3. Đăng ký vào [`AutoTabViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTabViewModel.cs) và thêm thẻ cấu hình tương ứng vào [`AutoTabView.xaml`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Features/Auto/AutoTabView.xaml).

### 4.2. Thêm Tab Chức Năng Mới Trên Cửa Sổ Chính (`AccountAwareTabViewModel`)
1. Tạo thư mục mới trong `src/DragonBoyAccountManager.GUI/Presentation/Features/<FeatureName>/`.
2. Tạo `<FeatureName>TabViewModel` kế thừa [`AccountAwareTabViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Shell/AccountAwareTabViewModel.cs) và `<FeatureName>TabView.xaml` theo Retro Pixel Design System.
3. Đăng ký ViewModel trong [`App.xaml.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/App.xaml.cs) và thêm vào danh sách tab trong [`MainViewModel.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Shell/MainViewModel.cs).
