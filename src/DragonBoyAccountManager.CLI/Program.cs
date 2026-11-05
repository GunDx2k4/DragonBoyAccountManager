using System.Text;
using DragonBoyAccountManager.Core.Accounts;
using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Engine;
using DragonBoyAccountManager.Core.Engine.Abstractions;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Sessions.Validation;
using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;

AnsiConsole.Write(new Rule("[bold cyan]DragonBoy Account Manager - CLI Host[/]").Centered());
AnsiConsole.WriteLine();

if (args.Length > 0 && (args[0] == "--inspect" || args[0] == "--test"))
{
    string targetPath = args.Length > 1 ? args[1] : @"d:\DragonBoy\252\Dragonboy252.exe";
    await RunInspectAsync(targetPath);
    return;
}

IAccountStorageService storage = new AccountStorageService();
var accounts = await storage.LoadAllAsync();

var engine = new GameEngine();

engine.OnSessionEvent += evt =>
{
    switch (evt)
    {
        case SessionStateChangedEvent e:
            if (e.Status == GameSessionStatus.Active && e.Snapshot?.Player != null)
            {
                var p = e.Snapshot.Player;
                AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] [green][[Active]][/] [cyan]{Markup.Escape(p.Name)}[/] | HP: [red]{p.Hp}/{p.MaxHp}[/] | Power: [yellow]{p.Power:N0}[/] | Pos: ({p.X}, {p.Y})");
            }
            else
            {
                AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] [yellow][[Status]][/] Trạng thái: [cyan]{e.Status}[/]");
            }
            break;

        case SessionMessageEvent e:
            AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] [dim][[Log]][/] {Markup.Escape(e.Message)}");
            break;

        case ActionReportedEvent e:
            var color = e.Report.Status switch
            {
                ActionStatus.Success => "green",
                ActionStatus.Failure => "red",
                ActionStatus.Running => "cyan",
                _ => "grey"
            };
            AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] [{color}][[{e.Report.ActionName}]][/] {Markup.Escape(e.Report.Message)}");
            break;

        case BehaviorStateChangedEvent e:
            var state = e.IsRunning ? "[green]BẬT[/]" : "[red]TẮT[/]";
            AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] [yellow][[Hành vi]][/] [cyan]{Markup.Escape(e.BehaviorId)}[/]: {state}");
            break;

        case SessionProcessExitedEvent e:
            AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] [red][[Exited]][/] Tiến trình game đã thoát. (Người dùng yêu cầu: {e.UserInitiated})");
            break;
    }
};

Account selectedAccount;

if (accounts.Count > 0)
{
    var table = new Table();
    table.Border(TableBorder.Rounded);
    table.AddColumn("[bold]STT[/]");
    table.AddColumn("[bold]Tên tài khoản[/]");
    table.AddColumn("[bold]Máy chủ[/]");
    table.AddColumn("[bold]Tự động login[/]");

    for (int i = 0; i < accounts.Count; i++)
    {
        var acc = accounts[i];
        table.AddRow(
            (i + 1).ToString(),
            Markup.Escape(acc.Username),
            Markup.Escape(acc.ServerName ?? "Mặc định"),
            acc.AutoLogin ? "[green]Bật[/]" : "[grey]Tắt[/]");
    }

    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();

    var promptChoices = accounts.Select(a => $"{a.Username} ({a.ServerName ?? "Mặc định"})").ToList();
    promptChoices.Add("Nhập tài khoản mới / Chạy trực tiếp");
    promptChoices.Add("Thoát");

    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Chọn tài khoản để khởi chạy:")
            .HighlightStyle(Style.Parse("cyan"))
            .AddChoices(promptChoices));

    if (choice == "Thoát")
    {
        return;
    }

    if (choice == "Nhập tài khoản mới / Chạy trực tiếp")
    {
        selectedAccount = PromptNewAccount();
    }
    else
    {
        int idx = promptChoices.IndexOf(choice);
        selectedAccount = accounts[idx];
    }
}
else
{
    AnsiConsole.MarkupLine("[yellow]Chưa có tài khoản nào được lưu trữ.[/]");
    selectedAccount = PromptNewAccount();
}

string gamePath = string.Empty;
while (string.IsNullOrWhiteSpace(gamePath) || !File.Exists(gamePath))
{
    gamePath = AnsiConsole.Ask<string>("Nhập đường dẫn game client ([cyan]Dragonboy252.exe[/]):");
    try
    {
        SessionLauncher.Validate(gamePath);
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Đường dẫn không hợp lệ:[/] {Markup.Escape(ex.Message)}");
        gamePath = string.Empty;
    }
}

var launchOptions = new AccountLaunchOptions
{
    AccountId = selectedAccount.Id,
    Username = selectedAccount.Username,
    Password = selectedAccount.Password,
    ServerId = selectedAccount.ServerId,
    ServerName = selectedAccount.ServerName,
    GamePath = gamePath,
    AutoLogin = selectedAccount.AutoLogin,
    AutoAssignSkills = false,
    GameSpeed = selectedAccount.GameSpeed > 0 ? selectedAccount.GameSpeed : 1.0f
};

AnsiConsole.MarkupLine($"[green]Đang khởi chạy tài khoản:[/] [cyan]{Markup.Escape(selectedAccount.Username)}[/]...");

bool started = await engine.StartAccountAsync(launchOptions);
if (!started)
{
    AnsiConsole.MarkupLine("[red]Khởi chạy tài khoản thất bại.[/]");
    return;
}

var accId = selectedAccount.Id;

while (engine.IsRunning(accId))
{
    var menu = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold]Menu điều khiển phiên game:[/]")
            .HighlightStyle(Style.Parse("cyan"))
            .AddChoices(
                "Xem thông tin nhân vật (Snapshot)",
                engine.IsAutoTrainRunning(accId) ? "Dừng tự động luyện tập" : "Bật tự động luyện tập",
                engine.IsAutoChatRunning(accId) ? "Dừng tự động chat" : "Bật tự động chat",
                "Gán phím kỹ năng tự động",
                "Sử dụng Capsule",
                "Sử dụng Đậu thần",
                "Sử dụng TDLT",
                "Đổi khu vực (Zone)",
                "Dừng phiên game (Stop)",
                "Thoát CLI"));

    switch (menu)
    {
        case "Xem thông tin nhân vật (Snapshot)":
            var player = engine.GetPlayerSnapshot(accId);
            if (player == null || !player.IsInGame)
            {
                AnsiConsole.MarkupLine("[yellow]Nhân vật chưa vào game.[/]");
            }
            else
            {
                var pTable = new Table().Border(TableBorder.Rounded);
                pTable.AddColumn("Thuộc tính");
                pTable.AddColumn("Giá trị");
                pTable.AddRow("Tên nhân vật", Markup.Escape(player.Name));
                pTable.AddRow("HP", $"{player.Hp}/{player.MaxHp}");
                pTable.AddRow("MP", $"{player.Mp}/{player.MaxMp}");
                pTable.AddRow("Sức mạnh", player.Power.ToString("N0"));
                pTable.AddRow("Tọa độ", $"({player.X}, {player.Y})");
                pTable.AddRow("Trạng thái TDLT", player.IsTdltActive ? "[green]Bật[/]" : "[grey]Tắt[/]");
                AnsiConsole.Write(pTable);
            }
            break;

        case "Bật tự động luyện tập":
            engine.StartAutoTrain(accId, selectedAccount.AutoSettings.Train);
            break;

        case "Dừng tự động luyện tập":
            engine.StopAutoTrain(accId);
            break;

        case "Bật tự động chat":
            engine.StartAutoChat(accId, new AutoChatOptions
            {
                Messages = ["Auto DBAM CLI", "DragonBoy Account Manager"],
                Delay = TimeSpan.FromSeconds(15)
            });
            break;

        case "Dừng tự động chat":
            engine.StopAutoChat(accId);
            break;

        case "Gán phím kỹ năng tự động":
            await engine.AutoAssignSkillsAsync(accId);
            break;

        case "Sử dụng Capsule":
            await engine.UseCapsuleAsync(accId);
            break;

        case "Sử dụng Đậu thần":
            await engine.UsePeanAsync(accId);
            break;

        case "Sử dụng TDLT":
            await engine.UseTdltAsync(accId);
            break;

        case "Đổi khu vực (Zone)":
            int zoneId = AnsiConsole.Prompt(new TextPrompt<int>("Nhập khu vực muốn đến (0-20):").DefaultValue(0));
            engine.ChangeZone(accId, zoneId);
            break;

        case "Dừng phiên game (Stop)":
            await engine.StopAccountAsync(accId);
            AnsiConsole.MarkupLine("[yellow]Đã dừng phiên game.[/]");
            break;

        case "Thoát CLI":
            await engine.StopAccountAsync(accId);
            goto done;
    }

    AnsiConsole.WriteLine();
}

done:
engine.Dispose();
AnsiConsole.MarkupLine("[green]Đã hoàn tất phiên làm việc CLI.[/]");

static Account PromptNewAccount()
{
    string user = AnsiConsole.Ask<string>("Tên đăng nhập:");
    string pass = AnsiConsole.Prompt(new TextPrompt<string>("Mật khẩu:").Secret());
    int serverId = AnsiConsole.Prompt(new TextPrompt<int>("Server ID:").DefaultValue(0));
    string serverName = AnsiConsole.Prompt(new TextPrompt<string>("Tên Server:").DefaultValue("Vũ trụ 1"));

    return new Account
    {
        Username = user,
        Password = pass,
        ServerId = serverId,
        ServerName = serverName,
        AutoLogin = true,
        AutoSettings = new AccountAutoSettings
        {
            Train = new AutoTrainOptions
            {
                UseTdlt = true,
                AutoPickItems = true,
                AutoUsePean = true,
                AvoidSuperMobs = true
            }
        }
    };
}

static async Task RunInspectAsync(string targetPath)
{
    AnsiConsole.MarkupLine($"[cyan][[INFO]][/] Đang kiểm tra tệp: [white]{Markup.Escape(targetPath)}[/]");

    using var validator = new GameClientValidator();
    var meta = validator.InspectClientMetadata(targetPath);

    var metaTable = new Table().Border(TableBorder.Rounded);
    metaTable.AddColumn("Thông số");
    metaTable.AddColumn("Giá trị");
    metaTable.AddRow("Trạng thái", meta.Status.ToString());
    metaTable.AddRow("Phiên bản nhận diện", meta.DetectedVersion ?? "Không rõ");
    metaTable.AddRow("Mã băm SHA-256", meta.ActualSha256 ?? "Không rõ");
    metaTable.AddRow("Khớp phiên bản chuẩn", meta.IsCorrectVersion ? "[green]Có[/]" : "[red]Không[/]");
    AnsiConsole.Write(metaTable);
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("[cyan][[INFO]][/] Đang khởi chạy game client...");
    var launcher = new SessionLauncher();
    var launchOptions = new LaunchOptions
    {
        GamePath = targetPath,
        ScreenWidth = 1024,
        ScreenHeight = 600,
        LogPrefix = "CLI_Inspect"
    };

    GameSession session;
    try
    {
        session = await launcher.LaunchAsync(launchOptions);
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red][[ERROR]][/] Khởi chạy thất bại: {Markup.Escape(ex.Message)}");
        return;
    }

    try
    {
        AnsiConsole.MarkupLine($"[green][[OK]][/] Đã gắn kết Runtime thành công vào PID: [cyan]{session.ProcessId}[/]");
        var runtime = session.Runtime;

        for (int i = 0; i < 15; i++)
        {
            await Task.Delay(1000);
            string? curName = session.LoginScreen.GetCurrentScreenName();
            bool isDown = session.LoginScreen.IsDownloadingData();
            bool isSvrReady = session.LoginScreen.IsServerListReady();
            AnsiConsole.MarkupLine($"[grey][[{i + 1}s]][/] Screen: [cyan]{curName ?? "null"}[/] | IsDownloading: {isDown} | IsServerReady: {isSvrReady}");
            if (curName == GameSchema.ServerListScreen.ClassName || curName == GameSchema.LoginScr.ClassName)
                break;
        }

        string versionStr = runtime.ReadStaticString(GameSchema.GameMidlet.ClassName, runtime.Layout.GameMidlet.Version);
        int intVersion = runtime.ReadStatic<int>(GameSchema.GameMidlet.ClassName, runtime.Layout.GameMidlet.IntVersion);
        string ip = runtime.ReadStaticString(GameSchema.GameMidlet.ClassName, runtime.Layout.GameMidlet.Ip);
        int port = runtime.ReadStatic<int>(GameSchema.GameMidlet.ClassName, runtime.Layout.GameMidlet.Port);
        int mapId = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.MapId);
        IntPtr myChar = runtime.GetMyCharPtr();

        IntPtr currentScreen = runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, runtime.Layout.GameCanvas.CurrentScreen);
        string? currentScreenName = session.LoginScreen.GetCurrentScreenName();
        bool isDownloading = session.LoginScreen.IsDownloadingData();
        bool isServerReady = session.LoginScreen.IsServerListReady();
        int selectedServerId = session.LoginScreen.GetSelectedServerId();
        bool isWaitDlg = session.LoginScreen.IsWaitDialogPresent();
        bool isNonWaitDlg = session.LoginScreen.IsNonWaitDialogPresent();
        session.LoginScreen.HasActiveDialog(out _, out string? dlgText);
        IntPtr serverScreen = runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, runtime.Layout.GameCanvas.ServerScreen);
        IntPtr loginScr = runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, runtime.Layout.GameCanvas.LoginScr);
        IntPtr service = runtime.GetServicePtr();

        var memTable = new Table().Border(TableBorder.Rounded);
        memTable.AddColumn("Trường dữ liệu bộ nhớ");
        memTable.AddColumn("Giá trị đọc được");
        memTable.AddRow("Runtime Type", runtime.GetType().Name);
        memTable.AddRow("Kiến trúc 64-bit", runtime.Is64Bit ? "[green]True (x64)[/]" : "[yellow]False (x86)[/]");
        memTable.AddRow("GameMidlet StaticFieldsPtr", $"0x{runtime.GetStaticFieldsPtr(GameSchema.GameMidlet.ClassName):X}");
        memTable.AddRow("GameCanvas StaticFieldsPtr", $"0x{runtime.GetStaticFieldsPtr(GameSchema.GameCanvas.ClassName):X}");
        memTable.AddRow("GameMidlet.VERSION", !string.IsNullOrEmpty(versionStr) ? $"[green]{Markup.Escape(versionStr)}[/]" : "[grey]<chưa nạp>[/]");
        memTable.AddRow("GameMidlet.intVERSION", intVersion.ToString());
        memTable.AddRow("GameMidlet.IP", Markup.Escape(ip));
        memTable.AddRow("GameMidlet.PORT", port.ToString());
        memTable.AddRow("TileMap.mapID", mapId.ToString());
        memTable.AddRow("Char.myChar Con trỏ", $"0x{myChar:X}");
        memTable.AddRow("GameCanvas.currentScreen", $"0x{currentScreen:X} ({currentScreenName ?? "null"})");
        memTable.AddRow("GameCanvas.serverScreen", $"0x{serverScreen:X}");
        memTable.AddRow("GameCanvas.loginScr", $"0x{loginScr:X}");
        memTable.AddRow("Service.instance", $"0x{service:X}");
        memTable.AddRow("IsDownloadingData", isDownloading.ToString());
        memTable.AddRow("IsServerListReady", isServerReady.ToString());
        memTable.AddRow("SelectedServerId", selectedServerId.ToString());
        memTable.AddRow("Dialog", $"Wait={isWaitDlg}, NonWait={isNonWaitDlg}, Text={dlgText ?? "null"}");
        AnsiConsole.Write(memTable);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green][[OK]][/] Kiểm tra đọc dữ liệu bộ nhớ IL2CPP hoàn tất thành công!");
    }
    finally
    {
        session.Dispose();
    }
}
