# Kiến Trúc Phân Hệ Core (`DragonBoyAccountManager.Core`)

[`DragonBoyAccountManager.Core`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core) là thư viện nền tảng độc lập giao diện, chịu trách nhiệm giao tiếp bộ nhớ ngoài tiến trình (Out-of-Process), thu thập dữ liệu Telemetry, điều phối lệnh CQRS và vận hành các hành vi tự động hóa.

---

## 1. Cấu Trúc Thư Mục & Phân Tầng (1:1 Namespace)

```text
src/DragonBoyAccountManager.Core/
├── Accounts/                           # Mô hình tài khoản & lưu trữ DPAPI JSON
│   ├── Account.cs
│   ├── AccountAutoSettings.cs          # Cấu hình tự động hóa tổng quát (Behaviors Dictionary)
│   ├── IAccountStorageService.cs
│   ├── AccountStorageService.cs
│   └── ServerOption.cs
├── Common/                             # Kiểu dữ liệu & tiện ích dùng chung
│   ├── Results/
│   │   ├── Result.cs
│   │   └── ActionExecutionReport.cs
│   ├── ChatType.cs
│   ├── Constants.cs
│   ├── GameKey.cs
│   ├── GameKeyMapper.cs
│   ├── GameSessionStatus.cs
│   └── TextMaskHelper.cs
├── Interop/                            # Giao tiếp Win32 Native & IL2CPP Runtime x64
│   ├── Abstractions/                   # IGameRuntime, GameRuntimeExtensions
│   ├── Native/                         # P/Invoke kernel32/user32/psapi & WindowsJobObject
│   ├── Memory/                         # ProcessMemory, RemoteScratchpad, x64 Assembler
│   ├── Execution/                      # RemoteInvoker x64 C-API invocation
│   ├── Il2Cpp/                         # Il2CppRuntime
│   ├── Schema/                         # Định nghĩa ánh xạ lớp/trường/phương thức & RVA
│   │   ├── GameDefinitions.cs
│   │   ├── GameMemoryLayout.cs
│   │   ├── GameSchema.cs
│   │   ├── GameSchema.System.cs
│   │   ├── GameSchema.Entities.cs
│   │   ├── GameSchema.World.cs
│   │   ├── GameSchema.UI.cs
│   │   └── VersionOffsetProvider.cs
│   └── GameRuntimeFactory.cs           # Factory khởi tạo và gắn kết runtime
├── Telemetry/                          # Thu thập dữ liệu một chiều (Read-Only Snapshots)
│   ├── Snapshots/
│   │   ├── GameSessionSnapshot.cs      # Túi chứa Snapshot đa hình cho toàn phiên
│   │   ├── PlayerSnapshot.cs           # Thông tin nhân vật, kỹ năng (GameSkillInfo) & hành trang (InventoryItem)
│   │   ├── WorldSnapshot.cs            # IPositioned, IWorldEntity, Quái, Boss, NPC, Vật phẩm rơi & Khu vực
│   │   └── MapSnapshot.cs              # Dữ liệu bản đồ & ma trận Tile phục vụ tìm đường
│   └── Scrapers/
│       ├── ISnapshotScraper.cs         # Giao diện tổng quát ISnapshotScraper<out TSnapshot>
│       ├── PlayerScraper.cs
│       ├── WorldScraper.cs
│       ├── MapScraper.cs
│       └── CollectionHelper.cs
├── Execution/                          # Đường ống thực thi lệnh một chiều (CQRS Write-Only)
│   ├── ICommandDispatcher.cs
│   ├── CommandDispatcher.cs            # Điều phối đa hình có khóa SemaphoreSlim
│   ├── GameInputSender.cs
│   ├── Commands/                       # Phân tách lệnh theo miền nghiệp vụ
│   │   ├── IGameCommand.cs
│   │   ├── CombatCommands.cs
│   │   ├── MovementCommands.cs
│   │   ├── InventoryCommands.cs
│   │   ├── ZoneCommands.cs
│   │   ├── ChatCommands.cs
│   │   ├── WorldCommands.cs
│   │   └── GameConfigCommands.cs
│   └── Handlers/
│       ├── CombatHandler.cs
│       ├── MovementHandler.cs
│       ├── InventoryHandler.cs
│       ├── ZoneHandler.cs
│       ├── ChatHandler.cs
│       ├── WorldHandler.cs
│       └── GameConfigHandler.cs
├── Automation/                         # Các hành vi tự động hóa & dịch vụ điều hướng bản đồ
│   ├── Coordinators/                   # Bộ điều phối tự động hóa dùng chung
│   │   ├── IGroundLootCoordinator.cs   # Hợp đồng điều phối nhặt đồ rơi
│   │   ├── GroundLootCoordinator.cs    # Quét items, lọc blacklist 15s, tính tọa độ tiếp đất, nhặt đồ
│   │   ├── IRecoveryCoordinator.cs     # Hợp đồng hồi phục HP/MP và sạc nạp năng lượng
│   │   ├── RecoveryCoordinator.cs      # Tự động dùng đậu thần, theo dõi & ngắt gồng chiêu
│   │   ├── ICombatCoordinator.cs       # Hợp đồng điều phối chiến đấu và xoay tua kỹ năng
│   │   ├── CombatCoordinator.cs        # Tiếp cận quái đất/quái bay, sticky target, xoay tua chiêu
│   │   ├── IZoneCoordinator.cs         # Hợp đồng điều phối chuyển khu khi trống quái
│   │   └── ZoneCoordinator.cs          # Quản lý cooldown, mở bảng khu và nhảy khu vắng
│   ├── Behaviors/                      # Hành vi điều phối luồng tự động
│   │   ├── IBehavior.cs                # IBehavior & IBehavior<TOptions>
│   │   ├── AutomationBehavior.cs       # Lớp cơ sở vòng lặp hành vi có AttachContext(ISessionContext)
│   │   ├── Boss/
│   │   │   ├── AutoBossBehavior.cs     # Tự động săn Boss (dùng chung GroundLootCoordinator)
│   │   │   └── AutoBossOptions.cs
│   │   ├── Combat/
│   │   │   ├── AutoTrainBehavior.cs    # Tự động luyện tập
│   │   │   └── AutoTrainOptions.cs
│   │   └── Social/
│   │       ├── AutoChatBehavior.cs
│   │       └── AutoChatOptions.cs
│   └── Navigation/
│       ├── AutoNavigator.cs
│       └── Xmap/
├── Sessions/                           # Quản lý vòng đời một phiên tiến trình game
│   ├── ISessionContext.cs              # Giao diện ngữ cảnh phiên (cô lập IGameRuntime)
│   ├── GameSession.cs                  # Container tổng quát chứa Scrapers, Dispatcher, Behaviors
│   ├── DefaultSessionBootstrap.cs      # Đăng ký bộ Scrapers, Handlers & Behaviors mặc định
│   ├── ISessionLauncher.cs             # Hợp đồng khởi chạy tiến trình game
│   ├── SessionLauncher.cs              # Thiết lập thông số & khởi chạy tiến trình Dragonboy252.exe
│   ├── LoginOrchestrator.cs            # Quy trình đăng nhập tuần tự trước khi vào thế giới game
│   ├── GameConfigOptions.cs
│   ├── Controllers/                    # Bộ điều khiển cấp phiên (LoginScreenController, SessionMaskingController, GameConfigController)
│   └── Validation/                     # Kiểm tra tính hợp lệ của client PE/IL2CPP
└── Engine/                             # Điều phối đa phiên & phát sự kiện hợp nhất
    ├── Abstractions/
    │   ├── IGameActionService.cs
    │   ├── IGameAutomationService.cs
    │   ├── IGameEngineEvents.cs
    │   ├── IGameEngine.cs
    │   └── GameEngineExtensions.cs
    ├── Events/
    │   └── SessionEvents.cs            # Hệ thống sự kiện đa hình SessionEvent
    ├── Models/
    │   └── AccountLaunchOptions.cs     # Cấu hình khởi chạy, AutoReconnect & ReconnectPolicyResolver
    ├── GameEngine.cs                   # Hiện thực IGameEngine, Auto-Reconnect & IAsyncDisposable
    └── SessionWorker.cs                # Quản lý luồng vòng đời phiên, IAsyncDisposable
```

---

## 2. Các Trụ Cột Tổng Quát Hóa Của Core

### 2.1. Phân Tách `GameSchema` Theo Miền & `GameRuntimeExtensions`
- Toàn bộ tên lớp, trường, phương thức và RVA offsets của `GameAssembly.dll` được quản lý tập trung tại `Interop/Schema/` (và `Offsets.json`) chia thành 4 tệp `partial class GameSchema`:
  - [`GameSchema.System.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Interop/Schema/GameSchema.System.cs): `Main`, `GameCanvas`, `Service`, `Controller`, `Session_ME`, `Rms`, `mSystem`.
  - [`GameSchema.Entities.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Interop/Schema/GameSchema.Entities.cs): `Char`, `Mob`, `MobTemplate`, `Npc`, `NpcTemplate`, `Item`, `ItemMap`, `ItemOption`, `Skill`, `SkillTemplate`.
  - [`GameSchema.World.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Interop/Schema/GameSchema.World.cs): `TileMap`, `Waypoint`, `PopUp`, `MovePoint`, `InfoDlg`.
  - [`GameSchema.UI.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Interop/Schema/GameSchema.UI.cs): `GameScr`, `LoginScr`, `ServerListScreen`, `SplashScr`, `SelectCharScr`, `ChooseCharScr`, `Panel`, `Command`, `TField`, `MyVector`, `MyHashTable`.

### 2.2. Đường Ống Telemetry Đa Hình (`ISnapshotScraper<out TSnapshot>`)
- Giao diện [`ISnapshotScraper<out TSnapshot>`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Telemetry/Scrapers/ISnapshotScraper.cs) sử dụng Default Interface Implementation cho `SnapshotType` và `Scrape()`.
- Các lớp thực thi ([`PlayerScraper`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Telemetry/Scrapers/PlayerScraper.cs), [`WorldScraper`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Telemetry/Scrapers/WorldScraper.cs), [`MapScraper`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Telemetry/Scrapers/MapScraper.cs)) cung cấp bản sao dữ liệu bất biến, bảo vệ an toàn đa luồng và chống đọc bộ nhớ lặp lại (bão syscall).

### 2.3. Đường Ống Điều Phối Lệnh CQRS (`ICommandDispatcher` & Handlers)
- Toàn bộ thao tác ghi bộ nhớ hoặc can thiệp điều khiển client được chuẩn hóa qua `IGameCommand`.
- [`CommandDispatcher`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Execution/CommandDispatcher.cs) thực thi tuần tự có khóa `SemaphoreSlim`, bảo đảm an toàn đa luồng out-of-process.

### 2.4. Phân Hệ Coordinators & Behaviors
- **Bộ điều phối dùng chung (`Automation/Coordinators/`):**
  - [`GroundLootCoordinator`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Automation/Coordinators/GroundLootCoordinator.cs): Đóng gói toàn bộ logic quét vật phẩm, lọc blacklist 15s, kiểm tra bùa thu hút, tính tọa độ tiếp đất an toàn và gửi lệnh nhặt đồ. Dùng chung cho cả `AutoTrain` và `AutoBoss`.
  - [`RecoveryCoordinator`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Automation/Coordinators/RecoveryCoordinator.cs): Đóng gói hồi phục đậu thần theo ngưỡng HP/MP và quản lý ngắt trạng thái gồng/tụ chiêu.
  - [`CombatCoordinator`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Automation/Coordinators/CombatCoordinator.cs): Chọn mục tiêu (sticky target), tiếp cận an toàn quái đất/quái bay, blacklist kẹt quái, xoay tua kỹ năng theo cooldown local tracker.
  - [`ZoneCoordinator`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Automation/Coordinators/ZoneCoordinator.cs): Xử lý đổi khu khi hết quái mục tiêu, gọi bảng khu và nhảy khu vắng nhất.
- **Thin Behaviors:** Các lớp hành vi như [`AutoTrainBehavior.cs`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Automation/Behaviors/Combat/AutoTrainBehavior.cs) đóng vai trò Orchestrator điều phối pipeline tuần tự.
- **Zero-Allocation Invariant:** 100% tham số nhịp tick được đóng gói qua `readonly record struct` trên stack. Loại bỏ hoàn toàn LINQ (`Where`, `OrderBy`, `FirstOrDefault`) trong vòng lặp nhịp tick 200ms-500ms, sử dụng vòng lặp chỉ mục phẳng `for`.

### 2.5. Luồng Vòng Đời & Dọn Dẹp Bất Đồng Bộ (`IAsyncDisposable`)
- [`GameEngine`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/GameEngine.cs) và [`SessionWorker`](file:///d:/DragonBoy/DBAM/src/DragonBoyAccountManager.Core/Engine/SessionWorker.cs) đồng bộ hiện thực `IAsyncDisposable`.
- Khi dừng ứng dụng hoặc ngắt phiên, quy trình teardown được thực thi phi phong bế qua `DisposeAsync()`, triệt tiêu hoàn toàn `.Wait()` hay `Thread.Sleep()`.
