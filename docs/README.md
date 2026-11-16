# Tài Liệu Kiến Trúc Giải Pháp DBAM (Solution Architecture)

Tài liệu này mô tả toàn diện kiến trúc giải pháp của **DragonBoyAccountManager (DBAM)**: phân tách đa tầng, giao tiếp bộ nhớ ngoài tiến trình (Out-of-Process), thu thập telemetry một chiều và thực thi lệnh qua Command Dispatcher.

---

## 1. Danh Mục Tài Liệu Kiến Trúc

| Tài Liệu | Nội Dung Trọng Tâm |
|---|---|
| [`README.md`](file:///d:/DragonBoy/DBAM/docs/README.md) | Tổng quan kiến trúc giải pháp, sơ đồ luồng dữ liệu liên tiến trình và các bất biến kỹ thuật cốt lõi. |
| [`core-architecture.md`](file:///d:/DragonBoy/DBAM/docs/core-architecture.md) | Kiến trúc phân hệ [`DragonBoyAccountManager.Core`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core): Interop, Telemetry Snapshot, CQRS Command Pipeline, Coordinators & Behaviors và Session Engine. |
| [`gui-architecture.md`](file:///d:/DragonBoy/DBAM/docs/gui-architecture.md) | Kiến trúc phân hệ [`DragonBoyAccountManager.GUI`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI): Cấu trúc 3 tầng (`Domain`, `Services`, `Presentation`), `IActivatableTab`, `AccountAwareTabViewModel`, phân rã ViewModel chuyên trách và Retro Pixel Design System. |
| [`extensibility-guide.md`](file:///d:/DragonBoy/DBAM/docs/extensibility-guide.md) | Cẩm nang mở rộng tính năng mới (Scraper, Command, Coordinator, Behavior, GUI Tab, Setting). |

---

## 2. Tổng Quan Hệ Sinh Thái Giải Pháp (`DBAM.slnx`)

Giải pháp [`DBAM.slnx`](file:///d:/DragonBoy/DBAM/DBAM.slnx) chạy trên nền tảng **.NET 10 / C# 13** (Windows x64) để điều khiển và tự động hóa hàng loạt tiến trình game client Unity 6 64-bit (`Dragonboy252.exe`) thông qua cơ chế **Out-of-Process Memory I/O & IL2CPP Runtime x64 Reflection**:

```mermaid
flowchart TD
    subgraph Hosts["Các Ứng Dụng Host (.NET 10 x64)"]
        GUI["DragonBoyAccountManager.GUI (WPF MVVM)"]
        CLI["DragonBoyAccountManager.CLI (Console)"]
    end

    subgraph Core["DragonBoyAccountManager.Core"]
        Engine["Engine Layer (IGameEngine / GameEngine / SessionWorker)"]
        Automation["Automation Layer (Coordinators / Behaviors / AutoNavigator / XmapNavigator)"]
        Execution["Execution Layer (CommandDispatcher / IGameCommand / Handlers)"]
        Telemetry["Telemetry Layer (ISnapshotScraper<TSnapshot> / GameSessionSnapshot)"]
        Interop["Interop Layer (IGameRuntime / RemoteInvoker / RemoteScratchpad / GameSchema)"]
    end

    subgraph Target["Tiến Trình Đích (Win32 x64)"]
        Unity["Dragonboy252.exe (GameAssembly.dll / UnityPlayer.dll)"]
    end

    GUI --> Engine
    CLI --> Engine

    Engine --> Automation
    Engine --> Execution
    Engine --> Telemetry
    Automation --> Execution
    Automation --> Telemetry
    Execution --> Interop
    Telemetry --> Interop
    Interop <-->|"ReadProcessMemory / WriteProcessMemory / CreateRemoteThread"| Unity
```

---

## 3. Các Bất Biến Kỹ Thuật Cốt Lõi (Architectural Invariants)

1. **Độc Lập Giao Diện ở Core:**
   - [`DragonBoyAccountManager.Core`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core) hoàn toàn không tham chiếu tới WPF (`System.Windows.*`) hay bất kỳ thư viện giao diện nào. Mọi Host (`GUI`, `CLI`, dịch vụ nền) đều giao tiếp qua [`IGameEngine`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Abstractions/IGameEngine.cs) và dòng sự kiện đa hình [`SessionEvent`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/Events/SessionEvents.cs).
2. **Quy Tắc Đồng Bộ 1:1 Thư Mục Và Namespace:**
   - Mọi tệp `.cs` và `.xaml` trong toàn bộ giải pháp đều có `namespace` khớp chính xác 100% với đường dẫn thư mục vật lý của tệp đó.
3. **Cô Lập Bộ Nhớ Liên Tiến Trình Qua Single Scratchpad Mutex:**
   - Mỗi phiên [`GameSession`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/GameSession.cs) chỉ cấp phát **một vùng nhớ thực thi duy nhất** ([`RemoteScratchpad`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Interop/Memory/RemoteScratchpad.cs)) trong tiến trình đích. Mọi lời gọi hàm IL2CPP qua [`RemoteInvoker`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Interop/Execution/RemoteInvoker.cs) đều đi qua khóa `WithLock` và tự động đính kèm `il2cpp_thread_attach` / `il2cpp_thread_detach`.
4. **Không Bao Giờ Thực Hiện Syscall Trên Luồng Giao Diện:**
   - Luồng WPF `Dispatcher` trên GUI **tuyệt đối không** gọi trực tiếp `ReadProcessMemory`, `WriteProcessMemory` hay `CreateRemoteThread`. Toàn bộ dữ liệu trạng thái nhân vật, kỹ năng, quái vật, khu vực và bản đồ đều được thu thập ở luồng nền [`SessionWorker`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/SessionWorker.cs) vào các bản ghi Snapshot bất biến ([`GameSessionSnapshot`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Telemetry/Snapshots/GameSessionSnapshot.cs)).
5. **Cô Lập Tầng Tự Động Hóa Khỏi `IGameRuntime`:**
   - Giao diện [`ISessionContext`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/ISessionContext.cs) cung cấp cho các Behavior và Coordinator không phơi bày `IGameRuntime`. Mọi hành vi tự động hóa bắt buộc phải đọc dữ liệu từ `GetSnapshot<TSnapshot>()` và gửi lệnh qua [`ICommandDispatcher`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/ICommandDispatcher.cs).
6. **Tuần Tự Hóa Khởi Động Tiến Trình & File RMS:**
   - [`GameEngine.StartAccountAsync`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/GameEngine.cs#L67-L141) sử dụng `SemaphoreSlim _launchGate` bao bọc [`SessionLauncher.LaunchAsync`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/SessionLauncher.cs) để bảo vệ đoạn tới hạn từ lúc ghi file cấu hình máy chủ dùng chung (`svselect`) qua [`GameRmsStorage`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/GameRmsStorage.cs) cho tới khi tiến trình game nạp xong `GameAssembly.dll`, ngăn chặn lỗi nhận nhầm máy chủ khi mở nhiều tài khoản cùng lúc.
7. **Khả Năng Mở Rộng:**
   - Các thành phần cốt lõi của cả Core và GUI đều hoạt động dựa trên cơ chế đăng ký tổng quát ([`DefaultSessionBootstrap`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Sessions/DefaultSessionBootstrap.cs), `RegisterScraper<TSnapshot>`, `RegisterHandler<TCommand>`, `RegisterBehavior<TBehavior>`, [`IActivatableTab`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Shell/IActivatableTab.cs), [`AccountAwareTabViewModel`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.GUI/Presentation/Shell/AccountAwareTabViewModel.cs)), cho phép thêm tính năng mới mà không cần sửa đổi mã nguồn điều phối hiện có.

---

## 4. Chuẩn Quy Trình Kiểm Chứng

Mọi thay đổi kiến trúc hoặc tính năng trong dự án đều phải vượt qua kiểm tra biên dịch không cảnh báo:

```powershell
dotnet build DBAM.slnx --nologo
```
