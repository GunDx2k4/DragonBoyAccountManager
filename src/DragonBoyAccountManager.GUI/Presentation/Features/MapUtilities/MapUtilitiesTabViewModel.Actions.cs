using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Automation.Navigation.Xmap;
using DragonBoyAccountManager.Core.Engine.Abstractions;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Dialogs;
using DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Items;

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

    private static int ResolveTargetMapId(object? parameter) => parameter switch
    {
        XmapConnectedNodeItem item => item.TargetMapId,
        int id => id,
        _ => -1
    };

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

    [RelayCommand]
    public async Task StartXmap(object? parameter)
    {
        XmapMapItemViewModel? target = parameter as XmapMapItemViewModel ?? SelectedXmapTarget;
        if (target == null)
        {
            StatusText = "Chưa chọn map đích!";
            return;
        }

        SelectedXmapTarget = target;

        var targetIds = GetTargetAccountIds();
        if (targetIds.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản đang chạy nào!";
            return;
        }

        _xmapCts?.Cancel();
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        _xmapCts = cts;

        IsNavigatingXmap = true;
        XmapStatus = $"Đang Xmap tới {target.DisplayText}...";
        StatusText = $"Bắt đầu Xmap tới {target.DisplayText} ({targetIds.Count} tài khoản)";

        bool isTeleport = FastTeleport;

        try
        {
            var tasks = targetIds.Select(async id =>
            {
                var res = await _gameEngine.XmapMoveAsync(id, target.Id, isTeleport, token);
                return res;
            });

            var results = await Task.WhenAll(tasks);
            int successes = results.Count(r => r.IsSuccess);

            if (!token.IsCancellationRequested)
            {
                StatusText = $"Xmap tới {target.DisplayText}: {successes}/{targetIds.Count} tài khoản thành công";
                XmapStatus = $"Đã tới {target.DisplayText}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Đã dừng Xmap.";
            XmapStatus = "Đã dừng";
        }
        finally
        {
            if (_xmapCts == cts)
            {
                IsNavigatingXmap = false;
                _xmapCts = null;
            }
            cts.Dispose();
        }
    }

    [RelayCommand]
    public void StopXmap()
    {
        _xmapCts?.Cancel();
        IsNavigatingXmap = false;
        XmapStatus = "Đã dừng";
        StatusText = "Đã dừng Xmap.";
    }

    [RelayCommand]
    public void ReloadXmap()
    {
        WorldMapGraph.Instance.Initialize();
        InitializeXmapData();
        StatusText = "Đã nạp lại dữ liệu map.";
    }

    [RelayCommand]
    public void SelectConnectedNode(object? parameter)
    {
        int targetId = ResolveTargetMapId(parameter);
        if (targetId >= 0)
        {
            var target = _allXmapMaps.FirstOrDefault(m => m.Id == targetId);
            if (target != null)
            {
                SelectedXmapTarget = target;
            }
        }
    }

    [RelayCommand]
    public void OpenMapGraphDialog()
    {
        var vm = new XmapNodePickerViewModel(CurrentMapId, CurrentMapName, SelectedXmapTarget?.Id);
        var dialog = new XmapNodePickerDialog(vm)
        {
            Owner = Application.Current?.MainWindow
        };

        bool? result = dialog.ShowDialog();
        if (result == true && dialog.SelectedNode != null)
        {
            var found = _allXmapMaps.FirstOrDefault(m => m.Id == dialog.SelectedNode.Id);
            if (found != null)
            {
                SelectedXmapTarget = found;
            }
        }
    }

    [RelayCommand]
    public void InspectNode(object? parameter)
    {
        int targetId = ResolveTargetMapId(parameter);
        if (targetId >= 0)
        {
            SetFocalMap(targetId, isUserExploration: true);
        }
    }

    [RelayCommand]
    public void ResetFocalToCurrent()
    {
        if (CurrentMapId >= 0)
        {
            SetFocalMap(CurrentMapId, isUserExploration: false);
        }
    }

    [RelayCommand]
    public async Task NavigateDirectNode(object? parameter)
    {
        int targetId = ResolveTargetMapId(parameter);
        if (targetId < 0) return;

        var targetMap = _allXmapMaps.FirstOrDefault(m => m.Id == targetId);
        if (targetMap != null)
        {
            SelectedXmapTarget = targetMap;
            await StartXmap(targetMap);
        }
    }

    [RelayCommand]
    public void SelectQuickTargetMapId()
    {
        if (int.TryParse(QuickTargetMapIdInput.Trim(), out int targetId))
        {
            var target = _allXmapMaps.FirstOrDefault(m => m.Id == targetId);
            if (target != null)
            {
                SelectedXmapTarget = target;
                QuickTargetMapIdInput = string.Empty;
            }
            else
            {
                StatusText = $"Không tìm thấy Map #{targetId}";
            }
        }
    }
}
