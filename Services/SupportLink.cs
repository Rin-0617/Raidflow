using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;

namespace RaidFlow.Services;

public static class SupportLink
{
    public const string OfuseUrl = "https://ofuse.me/rin0617";

    private const string TooltipText = "OFUSEで開発を応援する";

    public static TitleBarButton CreateTitleBarButton()
    {
        return new TitleBarButton
        {
            Icon = FontAwesomeIcon.Heart,
            IconOffset = new Vector2(2f, 1f),
            Click = _ => Util.OpenLink(OfuseUrl),
            ShowTooltip = () =>
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(TooltipText);
                ImGui.EndTooltip();
            },
        };
    }
}
