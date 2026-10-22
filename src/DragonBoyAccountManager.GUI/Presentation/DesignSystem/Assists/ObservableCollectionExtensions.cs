using System.Collections.ObjectModel;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.GUI.Presentation.DesignSystem.Assists;

public static class ObservableCollectionExtensions
{
    public static void SyncWith<T>(this ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        if (target.Count == source.Count && target.SequenceEqual(source))
        {
            return;
        }

        int commonCount = Math.Min(target.Count, source.Count);
        for (int i = 0; i < commonCount; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(target[i], source[i]))
            {
                target[i] = source[i];
            }
        }

        if (target.Count > source.Count)
        {
            for (int i = target.Count - 1; i >= source.Count; i--)
            {
                target.RemoveAt(i);
            }
        }
        else if (target.Count < source.Count)
        {
            for (int i = target.Count; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }
    }

    public static void SyncZones(
        this ObservableCollection<WorldZoneInfo> target,
        IReadOnlyList<WorldZoneInfo> newZones,
        int currentZoneId,
        ref WorldZoneInfo? selectedZone,
        ref bool isRefreshing,
        Action notifySelectedZoneChanged)
    {
        isRefreshing = true;
        try
        {
            bool needRebuild = target.Count != newZones.Count;
            if (!needRebuild)
            {
                for (int i = 0; i < newZones.Count; i++)
                {
                    if (target[i].ZoneId != newZones[i].ZoneId)
                    {
                        needRebuild = true;
                        break;
                    }
                }
            }

            if (needRebuild)
            {
                target.Clear();
                foreach (var z in newZones)
                {
                    target.Add(z);
                }
            }
            else
            {
                for (int i = 0; i < newZones.Count; i++)
                {
                    if (target[i] != newZones[i])
                    {
                        target[i] = newZones[i];
                    }
                }
            }

            if (selectedZone?.ZoneId != currentZoneId)
            {
                selectedZone = target.FirstOrDefault(z => z.ZoneId == currentZoneId);
                notifySelectedZoneChanged();
            }
        }
        finally
        {
            isRefreshing = false;
        }
    }
}
