using CommunityToolkit.Mvvm.Input;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities;

public sealed partial class MapUtilitiesTabViewModel
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
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        _moveWaypointCts = cts;

        string destinationName = !string.IsNullOrWhiteSpace(wp.Name)
            ? wp.Name
            : (parameter as WaypointButtonItem)?.Name ?? "Waypoint";

        StatusText = $"Đang di chuyển tới {destinationName}...";

        bool isTeleport = FastTeleport;

        try
        {
            await Task.WhenAll(targetIds.Select(id => _gameEngine.EnterWaypointAsync(id, wp.X, wp.Y, isTeleport, token)));

            if (!token.IsCancellationRequested)
            {
                StatusText = $"Đã chuyển tới {destinationName} ({targetIds.Count} tài khoản)";
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_moveWaypointCts == cts)
            {
                _moveWaypointCts = null;
            }
            cts.Dispose();
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

    private async Task ExecuteBatchActionAsync(string actionName, Func<Guid, Task<DragonBoyAccountManager.Core.Common.Results.Result>> action)
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
            var res = await action(id);
            if (res.IsSuccess)
            {
                successCount++;
            }
            lastMsg = res.Message;
        }

        StatusText = targets.Count == 1
            ? lastMsg
            : $"{actionName}: {successCount}/{targets.Count} tài khoản thành công";
    }

    [RelayCommand]
    public Task UseCapsuleAsync() => ExecuteBatchActionAsync("Dùng Capsule", id => _gameEngine.UseCapsuleAsync(id));

    [RelayCommand]
    public Task UseTdltAsync() => ExecuteBatchActionAsync("Dùng TDLT", id => _gameEngine.UseTdltAsync(id));
}
