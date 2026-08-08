using UnityEngine;

/// <summary>
/// Shared HUD palette.
///
/// The reason a League-looking HUD needs no art is that its identity lives in
/// the colour scheme and layout, not in textures: near-black navy panels, a
/// warm gold frame, cream text. Keeping the values in one place stops the
/// floating bars and the bottom bar from drifting apart.
///
/// Static class - never attached to anything.
/// </summary>
public static class HudStyle
{
    // frame + chrome
    public static readonly Color Gold       = new Color(0.784f, 0.667f, 0.431f, 1f);
    public static readonly Color GoldDim    = new Color(0.478f, 0.404f, 0.259f, 1f);
    public static readonly Color PanelDark  = new Color(0.020f, 0.047f, 0.086f, 0.94f);
    public static readonly Color PanelSlate = new Color(0.063f, 0.098f, 0.145f, 1f);

    // text
    public static readonly Color TextCream  = new Color(0.941f, 0.902f, 0.824f, 1f);
    public static readonly Color TextMuted  = new Color(0.596f, 0.639f, 0.671f, 1f);

    // bars
    public static readonly Color BarTrough  = new Color(0.043f, 0.075f, 0.110f, 1f);
    public static readonly Color BarTick    = new Color(0.020f, 0.039f, 0.063f, 0.9f);
    public static readonly Color HealthAlly = new Color(0.129f, 0.718f, 0.302f, 1f);
    public static readonly Color HealthFoe  = new Color(0.780f, 0.196f, 0.196f, 1f);
    public static readonly Color Mana       = new Color(0.161f, 0.420f, 0.851f, 1f);
}
