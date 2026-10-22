using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities;

public sealed partial class UtilitiesTabViewModel
{
    public void ExecuteChangeZone(int targetZone)
    {
        if (targetZone < 0) return;

        ZoneInput = targetZone.ToString();

        var targetIds = GetTargetAccountIds();
        if (targetIds.Count == 0) return;

        int successCount = 0;
        foreach (var id in targetIds)
        {
            if (_gameEngine.ChangeZone(id, targetZone))
            {
                successCount++;
            }
        }

        StatusText = $"Đã chuyển sang Khu {targetZone} ({successCount}/{targetIds.Count} tài khoản)";
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000, CancellationToken.None);
            RunOnUi(RefreshMapData);
        });
    }

    [RelayCommand]
    public void NextZone()
    {
        int next = (CurrentZoneId < 0 ? 0 : CurrentZoneId) + 1;
        ExecuteChangeZone(next);
    }

    [RelayCommand]
    public void PreviousZone()
    {
        int prev = (CurrentZoneId < 0 ? 0 : CurrentZoneId) - 1;
        if (prev < 0) prev = 0;
        ExecuteChangeZone(prev);
    }

    [RelayCommand]
    public void ChangeZone(object? parameter)
    {
        int targetZone = -1;

        if (parameter is int zInt)
        {
            targetZone = zInt;
        }
        else if (parameter is WorldZoneInfo zoneInfo)
        {
            targetZone = zoneInfo.ZoneId;
        }
        else if (parameter is string zStr && int.TryParse(zStr, out int parsed))
        {
            targetZone = parsed;
        }
        else if (int.TryParse(ZoneInput, out int inputZone))
        {
            targetZone = inputZone;
        }

        if (targetZone >= 0)
        {
            ExecuteChangeZone(targetZone);
        }
    }

    [RelayCommand]
    public async Task MoveToWaypoint(object? parameter)
    {
        WorldWaypointInfo? wp = null;
        if (parameter is WaypointButtonItem btnItem)
        {
            wp = btnItem.Waypoint;
        }
        else if (parameter is WorldWaypointInfo wpInfo)
        {
            wp = wpInfo;
        }

        if (wp == null) return;

        var targetIds = GetTargetAccountIds();
        if (targetIds.Count == 0) return;

        _moveWaypointCts?.Cancel();
        _moveWaypointCts?.Dispose();
        var cts = new CancellationTokenSource();
        _moveWaypointCts = cts;

        StatusText = $"Đang di chuyển tới {wp.Name}...";

        bool isTeleport = FastTeleport;

        try
        {
            await Task.WhenAll(targetIds.Select(id => _gameEngine.EnterWaypointAsync(id, wp.X, wp.Y, isTeleport, cts.Token)));

            if (!cts.Token.IsCancellationRequested)
            {
                StatusText = $"Đã chuyển tới {wp.Name} ({targetIds.Count} tài khoản)";
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    public async Task ClickObject(object? parameter)
    {
        if (parameter is not IWorldEntity entity) return;

        var targetIds = GetTargetAccountIds();
        if (targetIds.Count == 0) return;

        StatusText = $"Đang tiếp cận và chọn {entity.Name}...";

        bool isTeleport = FastTeleport;

        await Task.WhenAll(targetIds.Select(id => _gameEngine.FocusEntityAsync(id, entity, moveToEntity: true, isFastTeleport: isTeleport)));
        StatusText = $"Đã chọn {entity.Name} ({targetIds.Count} tài khoản)";
    }

    [RelayCommand]
    public async Task UseCapsuleAsync()
    {
        var targets = GetTargetAccountIds();
        if (targets.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản đang chạy nào!";
            return;
        }

        int successCount = 0;
        string lastMsg = string.Empty;
        foreach (var id in targets)
        {
            var res = await _gameEngine.UseCapsuleAsync(id);
            if (res.IsSuccess)
            {
                successCount++;
            }
            lastMsg = res.Message;
        }

        if (targets.Count == 1)
        {
            StatusText = lastMsg;
        }
        else
        {
            StatusText = $"Dùng Capsule: {successCount}/{targets.Count} tài khoản thành công";
        }
    }

    [RelayCommand]
    public async Task UseTdltAsync()
    {
        var targets = GetTargetAccountIds();
        if (targets.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản đang chạy nào!";
            return;
        }

        int successCount = 0;
        string lastMsg = string.Empty;
        foreach (var id in targets)
        {
            var res = await _gameEngine.UseTdltAsync(id);
            if (res.IsSuccess)
            {
                successCount++;
            }
            lastMsg = res.Message;
        }

        if (targets.Count == 1)
        {
            StatusText = lastMsg;
        }
        else
        {
            StatusText = $"Dùng TDLT: {successCount}/{targets.Count} tài khoản thành công";
        }
    }

    partial void OnGameSpeedChanged(float value)
    {
        if (_isInternalSpeedUpdate) return;
        var targets = ResolveSpeedTargets();
        if (targets.Count == 0) return;

        foreach (var id in targets)
        {
            _gameEngine.SetGameSpeed(id, value);
        }

        StatusText = $"Đã đặt tốc độ {value:0.0}x ({targets.Count} tài khoản)";
    }

    [RelayCommand]
    private void SetGameSpeed(string speedStr)
    {
        if (float.TryParse(speedStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float speed))
        {
            GameSpeed = speed;
        }
    }

    private List<Guid> ResolveSpeedTargets()
    {
        if (IsApplySpeedToAll)
        {
            var all = _selectionService?.GetRunningSpeedTargetAccountIds(true).ToList() ?? GetTargetAccountIds();
            if (all.Count > 0) return all;
        }

        return GetTargetAccountIds();
    }
}
