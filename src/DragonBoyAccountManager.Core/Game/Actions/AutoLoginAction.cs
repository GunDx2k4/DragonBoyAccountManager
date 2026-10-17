using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Schema;

namespace DragonBoyAccountManager.Core.Game.Actions;

public class AutoLoginAction(string username, string password, int serverId = 0, int maxAttempts = 10) 
    : GameAction("Auto Login")
{
    public string Username { get; } = username;
    public string Password { get; } = password;
    public int ServerId { get; } = serverId;
    public int MaxAttempts { get; } = maxAttempts;

    protected override async Task<ActionResult> OnExecuteAsync(
        IGameContext context, 
        IProgress<string>? progress, 
        CancellationToken cancellationToken)
    {
        try
        {
            if (!context.IsRunning || cancellationToken.IsCancellationRequested)
                return ActionResult.Failure("Game không hoạt động hoặc bị hủy.");

            // 1. Kiểm tra ngay nếu nhân vật đã vào game
            if (context.Player.IsInGame)
            {
                progress?.Report($"Đã vào game: {context.Player.Name}");
                return ActionResult.Success($"Đã vào game: {context.Player.Name}");
            }

            // 2. Chờ GameCanvas khởi tạo (tối đa 20s)
            progress?.Report("Đang chờ GameCanvas khởi tạo...");
            bool canvasReady = await WaitForGameCanvasAsync(context, TimeSpan.FromSeconds(20), cancellationToken);
            if (!canvasReady)
            {
                progress?.Report("Không thể khởi tạo GameCanvas.");
                return ActionResult.Failure("Không thể khởi tạo GameCanvas.");
            }

            if (context.Player.IsInGame) 
                return ActionResult.Success();

            int attempt = 0;

            // Vòng lặp State-Driven Auto-Login
            while (!cancellationToken.IsCancellationRequested && context.IsRunning)
            {
                if (context.Player.IsInGame)
                {
                    progress?.Report($"Đăng nhập thành công! Nhân vật: {context.Player.Name}");
                    return ActionResult.Success($"Đăng nhập thành công! Nhân vật: {context.Player.Name}");
                }

                attempt++;
                if (attempt > MaxAttempts)
                {
                    progress?.Report($"Đã thử đăng nhập {MaxAttempts} lần nhưng không thành công.");
                    return ActionResult.Failure($"Đã thử đăng nhập {MaxAttempts} lần không thành công.");
                }

                progress?.Report($"[Lần {attempt}/{MaxAttempts}] Đang kiểm tra trạng thái game...");

                // BƯỚC A: Kiểm tra và giải phóng Dialog lỗi / thông báo nếu có
                if (context.UI.HasDialog)
                {
                    bool isWait = context.UI.IsWaitDialog;
                    string? dlgText = context.UI.DialogText;

                    if (isWait)
                    {
                        progress?.Report($"Đang chờ phản hồi: {dlgText ?? "Vui lòng chờ..."}");
                        var waitDeadline = DateTime.UtcNow.AddSeconds(4);
                        while (DateTime.UtcNow < waitDeadline && !cancellationToken.IsCancellationRequested && context.IsRunning)
                        {
                            await Task.Delay(300, cancellationToken);
                            if (context.Player.IsInGame) return ActionResult.Success();
                            if (!context.UI.HasDialog || !context.UI.IsWaitDialog) break;
                        }
                    }

                    if (context.UI.HasDialog)
                    {
                        dlgText = context.UI.DialogText;
                        if (!string.IsNullOrWhiteSpace(dlgText))
                        {
                            progress?.Report($"Thông báo từ game: {dlgText}");
                        }
                        else
                        {
                            progress?.Report("Đang đóng hộp thoại thông báo...");
                        }

                        context.UI.CloseDialog();
                        await Task.Delay(400, cancellationToken);
                    }
                }

                if (context.Player.IsInGame) return ActionResult.Success();

                // BƯỚC B: Nhận diện màn hình hiện tại
                string? curScreen = context.UI.CurrentScreenName;

                // TRƯỜNG HỢP 1: Đang ở màn hình đăng nhập (LoginScr)
                if (curScreen == GameSchema.LoginScr.ClassName)
                {
                    if (context.UI.SelectedServerId != ServerId)
                    {
                        progress?.Report("Đang ở màn hình đăng nhập sai máy chủ, quay lại chọn máy chủ...");
                        context.UI.CancelLogin();
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    progress?.Report("Điền thông tin tài khoản...");
                    bool credsSet = false;
                    for (int i = 0; i < 5 && !cancellationToken.IsCancellationRequested; i++)
                    {
                        if (context.UI.SetLoginCredentials(Username, Password))
                        {
                            credsSet = true;
                            break;
                        }
                        await Task.Delay(200, cancellationToken);
                    }

                    if (!credsSet)
                    {
                        progress?.Report("Không thể điền tài khoản vào LoginScr, thử lại...");
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    await Task.Delay(200, cancellationToken);
                    progress?.Report("Gửi thông tin đăng nhập...");
                    context.UI.SubmitLogin();

                    var loginWaitTimeout = DateTime.UtcNow.AddSeconds(12);
                    bool needRetry = false;
                    while (DateTime.UtcNow < loginWaitTimeout && !cancellationToken.IsCancellationRequested && context.IsRunning)
                    {
                        await Task.Delay(300, cancellationToken);
                        if (context.Player.IsInGame)
                        {
                            progress?.Report($"Đăng nhập thành công! Nhân vật: {context.Player.Name}");
                            return ActionResult.Success();
                        }

                        if (context.UI.HasDialog && !context.UI.IsWaitDialog)
                        {
                            needRetry = true;
                            break;
                        }

                        string? screenNow = context.UI.CurrentScreenName;
                        if (screenNow is GameSchema.ServerListScreen.ClassName or GameSchema.ServerListScreen.ClassNameAlt)
                        {
                            await Task.Delay(300, cancellationToken);
                            progress?.Report("Xác thực hoàn tất, đang vào game...");
                            context.UI.LoginWithSavedCredentials();
                            break;
                        }
                    }

                    if (needRetry) continue;

                    var inGameWait = DateTime.UtcNow.AddSeconds(8);
                    while (DateTime.UtcNow < inGameWait && !cancellationToken.IsCancellationRequested && context.IsRunning)
                    {
                        await Task.Delay(300, cancellationToken);
                        if (context.Player.IsInGame)
                        {
                            progress?.Report($"Đăng nhập thành công! Nhân vật: {context.Player.Name}");
                            return ActionResult.Success();
                        }
                        if (context.UI.HasDialog && !context.UI.IsWaitDialog) break;
                    }

                    if (context.Player.IsInGame) return ActionResult.Success();

                    if (context.UI.CurrentScreenName == GameSchema.LoginScr.ClassName)
                    {
                        context.UI.CancelLogin();
                        await Task.Delay(400, cancellationToken);
                    }
                    continue;
                }

                // TRƯỜNG HỢP 2: Đang ở màn hình chọn máy chủ (ServerListScreen hoặc ServerScr)
                if (curScreen is GameSchema.ServerListScreen.ClassName or GameSchema.ServerListScreen.ClassNameAlt)
                {
                    if (context.UI.SelectedServerId != ServerId)
                    {
                        progress?.Report($"Đổi sang máy chủ ID: {ServerId}...");
                        context.UI.SelectServer(ServerId);
                        await Task.Delay(500, cancellationToken);
                    }

                    if (!context.Network.IsConnected)
                    {
                        progress?.Report("Đang kết nối tới máy chủ...");
                        context.Network.Connect();

                        var connectDeadline = DateTime.UtcNow.AddSeconds(8);
                        while (DateTime.UtcNow < connectDeadline && !cancellationToken.IsCancellationRequested && context.IsRunning)
                        {
                            await Task.Delay(200, cancellationToken);
                            if (context.Network.IsConnected && context.Network.IsKeyComplete) break;
                            if (context.UI.HasDialog && !context.UI.IsWaitDialog) break;
                        }

                        if (!context.Network.IsConnected)
                        {
                            progress?.Report("Chưa thể kết nối tới máy chủ, thử lại...");
                            await Task.Delay(1000, cancellationToken);
                            continue;
                        }
                    }
                    else if (!context.Network.IsKeyComplete)
                    {
                        var keyDeadline = DateTime.UtcNow.AddSeconds(5);
                        while (DateTime.UtcNow < keyDeadline && !cancellationToken.IsCancellationRequested && context.IsRunning)
                        {
                            await Task.Delay(200, cancellationToken);
                            if (context.Network.IsKeyComplete) break;
                        }
                    }

                    context.UI.CloseDialog();
                    await Task.Delay(200, cancellationToken);

                    progress?.Report("Mở bảng đăng nhập...");
                    context.UI.OpenLoginScreen();

                    var openLoginDeadline = DateTime.UtcNow.AddSeconds(4);
                    bool loginOpened = false;
                    while (DateTime.UtcNow < openLoginDeadline && !cancellationToken.IsCancellationRequested && context.IsRunning)
                    {
                        await Task.Delay(200, cancellationToken);
                        if (context.UI.CurrentScreenName == GameSchema.LoginScr.ClassName)
                        {
                            loginOpened = true;
                            break;
                        }
                        if (context.Player.IsInGame) return ActionResult.Success();
                    }

                    if (loginOpened)
                    {
                        progress?.Report("Điền thông tin và gửi đăng nhập...");
                        bool credsSet = false;
                        for (int i = 0; i < 5 && !cancellationToken.IsCancellationRequested; i++)
                        {
                            if (context.UI.SetLoginCredentials(Username, Password))
                            {
                                credsSet = true;
                                break;
                            }
                            await Task.Delay(200, cancellationToken);
                        }

                        if (credsSet)
                        {
                            await Task.Delay(200, cancellationToken);
                            context.UI.SubmitLogin();

                            var waitLogin = DateTime.UtcNow.AddSeconds(12);
                            while (DateTime.UtcNow < waitLogin && !cancellationToken.IsCancellationRequested && context.IsRunning)
                            {
                                await Task.Delay(300, cancellationToken);
                                if (context.Player.IsInGame)
                                {
                                    progress?.Report($"Đăng nhập thành công! Nhân vật: {context.Player.Name}");
                                    return ActionResult.Success();
                                }

                                if (context.UI.HasDialog && !context.UI.IsWaitDialog)
                                    break;

                                if (context.UI.CurrentScreenName is GameSchema.ServerListScreen.ClassName or GameSchema.ServerListScreen.ClassNameAlt)
                                {
                                    await Task.Delay(300, cancellationToken);
                                    progress?.Report("Xác thực hoàn tất, đang vào game...");
                                    context.UI.LoginWithSavedCredentials();
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        progress?.Report("Thử vào game bằng tài khoản đã lưu...");
                        context.UI.LoginWithSavedCredentials();
                        await Task.Delay(2000, cancellationToken);
                    }

                    var finalWait = DateTime.UtcNow.AddSeconds(6);
                    while (DateTime.UtcNow < finalWait && !cancellationToken.IsCancellationRequested && context.IsRunning)
                    {
                        await Task.Delay(300, cancellationToken);
                        if (context.Player.IsInGame)
                        {
                            progress?.Report($"Đăng nhập thành công! Nhân vật: {context.Player.Name}");
                            return ActionResult.Success();
                        }
                        if (context.UI.HasDialog && !context.UI.IsWaitDialog) break;
                    }

                    continue;
                }

                progress?.Report($"Đang ở màn hình '{curScreen ?? "Chưa xác định"}', chờ nạp...");
                await Task.Delay(1000, cancellationToken);
            }

            return context.Player.IsInGame 
                ? ActionResult.Success() 
                : ActionResult.Failure("Không thể hoàn tất đăng nhập.");
        }
        catch (OperationCanceledException)
        {
            return ActionResult.Failure("Đăng nhập bị hủy bỏ.");
        }
        catch (Exception ex)
        {
            progress?.Report($"Lỗi xử lý đăng nhập: {ex.Message}");
            return ActionResult.Failure(ex.Message, ex);
        }
    }

    private static async Task<bool> WaitForGameCanvasAsync(IGameContext context, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < timeout)
        {
            if (cancellationToken.IsCancellationRequested || !context.IsRunning)
                return false;

            if (context.Player.IsInGame || context.UI.CanvasPointer != IntPtr.Zero)
                return true;

            await Task.Delay(300, cancellationToken);
        }
        return false;
    }
}
