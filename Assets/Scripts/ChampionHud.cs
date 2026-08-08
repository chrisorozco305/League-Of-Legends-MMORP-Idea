using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-centre champion HUD: portrait plate, health bar, mana bar, stat block.
///
/// Put this on GameManager alongside CursorLock / ClickIndicator / CursorManager.
/// Builds its whole hierarchy at runtime from null-sprite Images, so there is no
/// prefab to wire and nothing to lose if the scene is overwritten.
///
/// Health and the stat block read live values. Mana and level are placeholders -
/// no resource or XP system exists yet - and are labelled as such in the Inspector.
///
/// Deliberately has no GraphicRaycaster: the HUD is display-only, so it needs no
/// EventSystem in the scene. Adding clickable items later means adding both.
/// </summary>
public class ChampionHud : MonoBehaviour
{
    [Header("Bindings (auto-found if left empty)")]
    [SerializeField] Health champion;

    [Header("Placeholders - no systems behind these yet")]
    [SerializeField] float maxMana = 400f;
    [SerializeField] int level = 1;
    [SerializeField] string championName = "CHAMPION";

    [Header("Layout")]
    [SerializeField] Vector2 panelSize = new Vector2(660f, 120f);
    [SerializeField] float bottomMargin = 14f;

    ChampionCombat combat;

    RectTransform hpFill;
    RectTransform manaFill;
    TextMeshProUGUI hpLabel;
    TextMeshProUGUI manaLabel;
    TextMeshProUGUI adValue;
    TextMeshProUGUI asValue;
    TextMeshProUGUI rangeValue;

    float hpFillWidth;
    float manaFillWidth;

    void Start()
    {
        if (champion == null)
        {
            combat = FindFirstObjectByType<ChampionCombat>();
            if (combat != null) champion = combat.GetComponent<Health>();
        }
        else
        {
            combat = champion.GetComponent<ChampionCombat>();
        }

        Build();
        Refresh();
    }

    void Update() => Refresh();

    // ---------- construction ----------

    void Build()
    {
        var canvasGo = new GameObject("ChampionHUD",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;   // split the difference on odd aspect ratios

        // gold frame with a dark plate inside it - the whole League look in two rects
        RectTransform frame = Rect("Panel", canvasGo.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, bottomMargin), panelSize);
        AddImage(frame, HudStyle.Gold);

        RectTransform plate = Stretched("Plate", frame, 2f);
        AddImage(plate, HudStyle.PanelDark);

        BuildPortrait(plate);
        BuildBars(plate);
        BuildStats(plate);
    }

    void BuildPortrait(RectTransform plate)
    {
        RectTransform frame = Rect("Portrait", plate,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(14f, 0f), new Vector2(92f, 92f));
        AddImage(frame, HudStyle.Gold);

        RectTransform inner = Stretched("Inner", frame, 2f);
        AddImage(inner, HudStyle.PanelSlate);

        // stand-in until there is portrait art
        Text("Placeholder", inner, championName.Length > 0 ? championName.Substring(0, 1) : "?",
             40f, HudStyle.GoldDim, TextAlignmentOptions.Center);

        // level badge, pinned to the frame's bottom-left corner like LoL
        RectTransform badge = Rect("LevelBadge", frame,
            Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
        AddImage(badge, HudStyle.Gold);

        RectTransform badgeInner = Stretched("Inner", badge, 2f);
        AddImage(badgeInner, HudStyle.PanelDark);

        Text("Level", badgeInner, level.ToString(), 15f, HudStyle.TextCream, TextAlignmentOptions.Center);
    }

    void BuildBars(RectTransform plate)
    {
        const float barX = 122f;
        const float barW = 330f;

        RectTransform nameRow = Rect("Name", plate,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(barX, 40f), new Vector2(barW, 18f));
        Text("Value", nameRow, championName, 14f, HudStyle.TextMuted, TextAlignmentOptions.Left);

        hpFill = BuildBar(plate, new Vector2(barX, 14f), new Vector2(barW, 26f),
                          HudStyle.HealthAlly, out hpLabel, out hpFillWidth);

        manaFill = BuildBar(plate, new Vector2(barX, -16f), new Vector2(barW, 18f),
                            HudStyle.Mana, out manaLabel, out manaFillWidth);
    }

    /// <summary>Gold frame -> dark trough -> left-anchored fill -> centred label.</summary>
    RectTransform BuildBar(RectTransform plate, Vector2 pos, Vector2 size, Color fillColor,
                           out TextMeshProUGUI label, out float fillWidth)
    {
        const float inset = 1.5f;

        RectTransform frame = Rect("Bar", plate,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), pos, size);
        AddImage(frame, HudStyle.GoldDim);

        RectTransform trough = Stretched("Trough", frame, inset);
        AddImage(trough, HudStyle.BarTrough);

        fillWidth = size.x - inset * 2f;

        RectTransform fill = Rect("Fill", trough,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
            new Vector2(fillWidth, size.y - inset * 2f));
        AddImage(fill, fillColor);

        label = Text("Label", trough, "", 12f, HudStyle.TextCream, TextAlignmentOptions.Center);

        return fill;
    }

    void BuildStats(RectTransform plate)
    {
        adValue    = StatRow(plate,  26f, "Attack Damage");
        asValue    = StatRow(plate,   0f, "Attack Speed");
        rangeValue = StatRow(plate, -26f, "Range");
    }

    TextMeshProUGUI StatRow(RectTransform plate, float y, string name)
    {
        RectTransform row = Rect("Stat", plate,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-14f, y), new Vector2(170f, 22f));

        Text("Label", row, name, 12f, HudStyle.TextMuted, TextAlignmentOptions.Left);
        return Text("Value", row, "-", 13f, HudStyle.TextCream, TextAlignmentOptions.Right);
    }

    // ---------- live values ----------

    void Refresh()
    {
        if (champion != null)
        {
            float pct = champion.Max <= 0f ? 0f : Mathf.Clamp01(champion.Current / champion.Max);
            if (hpFill != null) hpFill.sizeDelta = new Vector2(hpFillWidth * pct, hpFill.sizeDelta.y);
            if (hpLabel != null)
                hpLabel.text = $"{Mathf.CeilToInt(champion.Current)} / {Mathf.CeilToInt(champion.Max)}";
        }
        else if (hpLabel != null)
        {
            // champion died - Health destroys the GameObject, so drain the bar
            if (hpFill != null) hpFill.sizeDelta = new Vector2(0f, hpFill.sizeDelta.y);
            hpLabel.text = "0 / 0";
        }

        // placeholder: pinned full until a resource system exists
        if (manaFill != null) manaFill.sizeDelta = new Vector2(manaFillWidth, manaFill.sizeDelta.y);
        if (manaLabel != null) manaLabel.text = $"{Mathf.CeilToInt(maxMana)} / {Mathf.CeilToInt(maxMana)}";

        if (combat != null)
        {
            if (adValue != null) adValue.text = combat.Damage.ToString("0");
            if (asValue != null) asValue.text = combat.AttacksPerSecond.ToString("0.00");
            if (rangeValue != null) rangeValue.text = combat.AttackRange.ToString("0.#");
        }
    }

    // ---------- tiny UI builders ----------

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot,
                              Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretched(string name, Transform parent, float inset)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    static Image AddImage(RectTransform rt, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Text(string name, Transform parent, string content, float size,
                                Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }
}
