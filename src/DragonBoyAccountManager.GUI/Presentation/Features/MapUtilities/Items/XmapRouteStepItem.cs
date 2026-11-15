using DragonBoyAccountManager.Core.Automation.Navigation.Xmap;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Items;

public sealed class XmapRouteStepItem
{
    public int StepIndex { get; init; }
    public int MapId { get; init; }
    public string MapName { get; init; } = string.Empty;
    public string TransitionText { get; init; } = string.Empty;
    public string DirectionText { get; init; } = string.Empty;
    public bool IsLast { get; init; }
    public bool IsCurrent { get; init; }

    public static void PopulateRouteSteps(
        ICollection<XmapRouteStepItem> target,
        int currentMapId,
        string currentMapName,
        IReadOnlyList<NavigationStep> route,
        string startTransitionText = "Bắt đầu")
    {
        target.Add(new XmapRouteStepItem
        {
            StepIndex = 1,
            MapId = currentMapId,
            MapName = currentMapName,
            TransitionText = startTransitionText,
            DirectionText = "Gốc",
            IsCurrent = true,
            IsLast = false
        });

        for (int i = 0; i < route.Count; i++)
        {
            var step = route[i];
            string dirText = step.Transition.Direction switch
            {
                MapDirection.Left => "Trái",
                MapDirection.Right => "Phải",
                MapDirection.Center => "Giữa",
                MapDirection.Npc => "NPC",
                _ => "Cổng"
            };

            target.Add(new XmapRouteStepItem
            {
                StepIndex = i + 2,
                MapId = step.TargetMapId,
                MapName = step.TargetMapName,
                TransitionText = step.Transition.IsNpc ? "NPC" : "Cổng",
                DirectionText = dirText,
                IsCurrent = false,
                IsLast = i == route.Count - 1
            });
        }
    }
}
