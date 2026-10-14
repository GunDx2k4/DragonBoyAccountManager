namespace DragonBoyAccountManager.Core.Game;

public class AutoLoginAction(string username, string password, int serverId = 0, int maxAttempts = 10) : IGameAction
{
    public string Name => "Auto Login";

    public string Username { get; } = username;
    public string Password { get; } = password;
    public int ServerId { get; } = serverId;
    public int MaxAttempts { get; } = maxAttempts;

    public async Task<bool> ExecuteAsync(GameInstance instance, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!instance.IsRunning || cancellationToken.IsCancellationRequested)
                return false;

            // 1. Kiểm tra ngay nếu nhân vật đã vào game
            if (instance.MyChar.IsInGame)
            {
                progress?.Report($"Đã vào game: {instance.MyChar.Name}");
                return true;
            }

            // 2. Chờ GameCanvas khởi tạo (tối đa 20s)
            progress?.Report("Đang chờ GameCanvas khởi tạo...");
            bool canvasReady = await WaitForGameCanvasAsync(instance, TimeSpan.FromSeconds(20), cancellationToken);
            if (!canvasReady)
            {
                progress?.Report("Không thể khởi tạo GameCanvas.");
                return false;
            }

            if (instance.MyChar.IsInGame) return true;

            var canvas = instance.Canvas;
            var session = instance.Session;
            var serverListScreen = instance.ServerScreen;
            var loginScreen = instance.LoginScreen;

            int attempt = 0;

            // Vòng lặp State-Driven Auto-Login: Tự động phát hiện trạng thái thực tế và retry khi gặp lỗi
            while (!cancellationToken.IsCancellationRequested && instance.IsRunning)
            {
                if (instance.MyChar.IsInGame)
                {
                    progress?.Report($"Đăng nhập thành công! Nhân vật: {instance.MyChar.Name}");
                    return true;
                }

                attempt++;
                if (attempt > MaxAttempts)
                {
                    progress?.Report($"Đã thử đăng nhập {MaxAttempts} lần nhưng không thành công.");
                    return false;
                }

                progress?.Report($"[Lần {attempt}/{MaxAttempts}] Đang kiểm tra trạng thái game...");

                // BƯỚC A: Kiểm tra và giải phóng Dialog lỗi / thông báo nếu có
                if (canvas.CurrentDialog != IntPtr.Zero)
                {
                    bool isWait = canvas.IsWaitDialog;
                    string? dlgText = canvas.GetDialogText();

                    if (isWait)
                    {
                        progress?.Report($"Đang chờ phản hồi: {dlgText ?? "Vui lòng chờ..."}");
                        // Chờ xem wait dialog có tự giải quyết hoặc vào game không (tối đa 4s)
                        var waitDeadline = DateTime.UtcNow.AddSeconds(4);
                        while (DateTime.UtcNow < waitDeadline && !cancellationToken.IsCancellationRequested && instance.IsRunning)
                        {
                            await Task.Delay(300, cancellationToken);
                            if (instance.MyChar.IsInGame) return true;
                            if (canvas.CurrentDialog == IntPtr.Zero || !canvas.IsWaitDialog) break;
                        }
                    }

                    // Nếu sau thời gian chờ vẫn còn dialog hoặc là dialog lỗi (OK dialog)
                    if (canvas.CurrentDialog != IntPtr.Zero)
                    {
                        dlgText = canvas.GetDialogText();
                        if (!string.IsNullOrWhiteSpace(dlgText))
                        {
                            progress?.Report($"Thông báo từ game: {dlgText}");
                        }
                        else
                        {
                            progress?.Report("Đang đóng hộp thoại thông báo...");
                        }

                        canvas.EndDlg();
                        await Task.Delay(400, cancellationToken);
                    }
                }

                if (instance.MyChar.IsInGame) return true;

                // BƯỚC B: Nhận diện màn hình hiện tại để có hành vi phù hợp
                string? curScreen = canvas.CurrentScreenName;

                // TRƯỜNG HỢP 1: Đang ở màn hình đăng nhập (LoginScr)
                if (curScreen == "LoginScr")
                {
                    // Nếu đang chọn sai server, quay về ServerListScreen để chọn lại
                    if (serverListScreen.IpSelect != ServerId)
                    {
                        progress?.Report("Đang ở màn hình đăng nhập sai máy chủ, quay lại chọn máy chủ...");
                        loginScreen.Perform(101); // Quay lại ServerListScreen
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    // Điền thông tin tài khoản và mật khẩu
                    progress?.Report("Điền thông tin tài khoản...");
                    bool credsSet = false;
                    for (int i = 0; i < 5 && !cancellationToken.IsCancellationRequested; i++)
                    {
                        if (loginScreen.SetCredentials(Username, Password))
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
                    loginScreen.Perform(2008);

                    // Chờ kết quả đăng nhập (tối đa 12s)
                    var loginWaitTimeout = DateTime.UtcNow.AddSeconds(12);
                    bool needRetry = false;
                    while (DateTime.UtcNow < loginWaitTimeout && !cancellationToken.IsCancellationRequested && instance.IsRunning)
                    {
                        await Task.Delay(300, cancellationToken);
                        if (instance.MyChar.IsInGame)
                        {
                            progress?.Report($"Đăng nhập thành công! Nhân vật: {instance.MyChar.Name}");
                            return true;
                        }

                        // Nếu xuất hiện dialog lỗi từ máy chủ
                        if (canvas.CurrentDialog != IntPtr.Zero && !canvas.IsWaitDialog)
                        {
                            needRetry = true;
                            break;
                        }

                        // Nếu game chuyển về ServerListScreen sau khi xác thực
                        string? screenNow = canvas.CurrentScreenName;
                        if (screenNow is "ServerListScreen" or "ServerScr")
                        {
                            await Task.Delay(300, cancellationToken);
                            progress?.Report("Xác thực hoàn tất, đang vào game...");
                            serverListScreen.Perform(3);
                            break;
                        }
                    }

                    if (needRetry)
                    {
                        // Sẽ được vòng lặp tiếp theo xử lý dialog và retry
                        continue;
                    }

                    // Tiếp tục chờ vào game thêm tối đa 8s
                    var inGameWait = DateTime.UtcNow.AddSeconds(8);
                    while (DateTime.UtcNow < inGameWait && !cancellationToken.IsCancellationRequested && instance.IsRunning)
                    {
                        await Task.Delay(300, cancellationToken);
                        if (instance.MyChar.IsInGame)
                        {
                            progress?.Report($"Đăng nhập thành công! Nhân vật: {instance.MyChar.Name}");
                            return true;
                        }
                        if (canvas.CurrentDialog != IntPtr.Zero && !canvas.IsWaitDialog) break;
                    }

                    if (instance.MyChar.IsInGame) return true;

                    // Nếu sau thời gian chờ vẫn chưa vào game và đang kẹt ở LoginScr, bấm quay lại để reset
                    if (canvas.CurrentScreenName == "LoginScr")
                    {
                        loginScreen.Perform(101);
                        await Task.Delay(400, cancellationToken);
                    }
                    continue;
                }

                // TRƯỜNG HỢP 2: Đang ở màn hình chọn máy chủ (ServerListScreen hoặc ServerScr)
                if (curScreen is "ServerListScreen" or "ServerScr")
                {
                    // 1. Kiểm tra máy chủ đã đúng chưa
                    if (serverListScreen.IpSelect != ServerId)
                    {
                        progress?.Report($"Đổi sang máy chủ ID: {ServerId}...");
                        if (canvas.MenuPointer != IntPtr.Zero)
                        {
                            canvas.SetMenuSelectedItem(ServerId);
                        }
                        serverListScreen.Perform(6);
                        await Task.Delay(500, cancellationToken);
                    }

                    // 2. Kiểm tra kết nối mạng của Session
                    if (!session.IsConnected)
                    {
                        progress?.Report("Đang kết nối tới máy chủ...");
                        canvas.Connect();

                        var connectDeadline = DateTime.UtcNow.AddSeconds(8);
                        while (DateTime.UtcNow < connectDeadline && !cancellationToken.IsCancellationRequested && instance.IsRunning)
                        {
                            await Task.Delay(200, cancellationToken);
                            if (session.IsConnected && session.IsKeyComplete) break;
                            if (canvas.CurrentDialog != IntPtr.Zero && !canvas.IsWaitDialog) break;
                        }

                        if (!session.IsConnected)
                        {
                            progress?.Report("Chưa thể kết nối tới máy chủ, thử lại...");
                            await Task.Delay(1000, cancellationToken);
                            continue;
                        }
                    }
                    else if (!session.IsKeyComplete)
                    {
                        var keyDeadline = DateTime.UtcNow.AddSeconds(5);
                        while (DateTime.UtcNow < keyDeadline && !cancellationToken.IsCancellationRequested && instance.IsRunning)
                        {
                            await Task.Delay(200, cancellationToken);
                            if (session.IsKeyComplete) break;
                        }
                    }

                    // 3. Đóng dialog cũ nếu còn sót
                    canvas.EndDlg();
                    await Task.Delay(200, cancellationToken);

                    // 4. Mở bảng đăng nhập
                    progress?.Report("Mở bảng đăng nhập...");
                    serverListScreen.Perform(7);

                    // Chờ màn hình LoginScr xuất hiện (tối đa 4s)
                    var openLoginDeadline = DateTime.UtcNow.AddSeconds(4);
                    bool loginOpened = false;
                    while (DateTime.UtcNow < openLoginDeadline && !cancellationToken.IsCancellationRequested && instance.IsRunning)
                    {
                        await Task.Delay(200, cancellationToken);
                        if (canvas.CurrentScreenName == "LoginScr")
                        {
                            loginOpened = true;
                            break;
                        }
                        if (instance.MyChar.IsInGame) return true;
                    }

                    if (loginOpened)
                    {
                        // Điền credentials
                        progress?.Report("Điền thông tin và gửi đăng nhập...");
                        bool credsSet = false;
                        for (int i = 0; i < 5 && !cancellationToken.IsCancellationRequested; i++)
                        {
                            if (loginScreen.SetCredentials(Username, Password))
                            {
                                credsSet = true;
                                break;
                            }
                            await Task.Delay(200, cancellationToken);
                        }

                        if (credsSet)
                        {
                            await Task.Delay(200, cancellationToken);
                            loginScreen.Perform(2008);

                            // Chờ vào game hoặc phản hồi (tối đa 12s)
                            var waitLogin = DateTime.UtcNow.AddSeconds(12);
                            while (DateTime.UtcNow < waitLogin && !cancellationToken.IsCancellationRequested && instance.IsRunning)
                            {
                                await Task.Delay(300, cancellationToken);
                                if (instance.MyChar.IsInGame)
                                {
                                    progress?.Report($"Đăng nhập thành công! Nhân vật: {instance.MyChar.Name}");
                                    return true;
                                }

                                if (canvas.CurrentDialog != IntPtr.Zero && !canvas.IsWaitDialog)
                                    break;

                                if (canvas.CurrentScreenName is "ServerListScreen" or "ServerScr")
                                {
                                    await Task.Delay(300, cancellationToken);
                                    progress?.Report("Xác thực hoàn tất, đang vào game...");
                                    serverListScreen.Perform(3);
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Nếu không mở được LoginScr qua perform(7), thử perform(3) trực tiếp
                        // vì nếu RMS đã lưu tài khoản thì perform(3) (Login_New) sẽ đăng nhập luôn
                        progress?.Report("Thử vào game bằng tài khoản đã lưu...");
                        serverListScreen.Perform(3);
                        await Task.Delay(2000, cancellationToken);
                    }

                    // Kiểm tra xem đã vào game chưa
                    var finalWait = DateTime.UtcNow.AddSeconds(6);
                    while (DateTime.UtcNow < finalWait && !cancellationToken.IsCancellationRequested && instance.IsRunning)
                    {
                        await Task.Delay(300, cancellationToken);
                        if (instance.MyChar.IsInGame)
                        {
                            progress?.Report($"Đăng nhập thành công! Nhân vật: {instance.MyChar.Name}");
                            return true;
                        }
                        if (canvas.CurrentDialog != IntPtr.Zero && !canvas.IsWaitDialog) break;
                    }

                    continue;
                }

                // TRƯỜNG HỢP 3: Màn hình khác (SplashScr, loading, v.v.)
                progress?.Report($"Đang ở màn hình '{curScreen ?? "Chưa xác định"}', chờ nạp...");
                await Task.Delay(1000, cancellationToken);
            }

            return instance.MyChar.IsInGame;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            progress?.Report($"Lỗi xử lý đăng nhập: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> WaitForGameCanvasAsync(GameInstance instance, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < timeout)
        {
            if (cancellationToken.IsCancellationRequested || !instance.IsRunning)
                return false;

            if (instance.MyChar.IsInGame)
                return true;

            if (instance.Canvas.IsAlive)
                return true;

            await Task.Delay(300, cancellationToken);
        }
        return false;
    }
}
