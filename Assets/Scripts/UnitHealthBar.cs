using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating world-space health bar that rides above a unit.
///
/// Never added in the Inspector - HealthBarManager calls Attach() for every
/// Health in the scene. Built entirely from null-sprite Images, so it needs no
/// textures, prefabs, or shaders (same approach as RangeIndicator).
/// </summary>
[DisallowMultipleComponent]
public class UnitHealthBar : MonoBehaviour
{
    // world units per UI "pixel" - lets us author sizes as sensible pixel
    // numbers and still land at a readable size under a top-down camera
    const float WorldScale = 0.01f;

    Health health;
    Camera cam;
    RectTransform fillRect;
    float fullWidth;

    /// <summary>
    /// Creates a bar above <paramref name="target"/> if it doesn't already have one.
    /// Safe to call on every scan - it early-outs on units that are already barred.
    /// </summary>
    public static void Attach(Health target, Color fillColor, float yOffset,
                              float width, float height, float hpPerSegment)
    {
        if (target == null) return;
        if (target.GetComponentInChildren<UnitHealthBar>() != null) return;

        var go = new GameObject("HealthBar", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(target.transform, false);

        var bar = go.AddComponent<UnitHealthBar>();
        bar.Build(target, fillColor, yOffset, width, height, hpPerSegment);
    }

    void Build(Health target, Color fillColor, float yOffset,
               float width, float height, float hpPerSegment)
    {
        health = target;
        cam = Camera.main;
        fullWidth = width;

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var root = (RectTransform)transform;
        root.sizeDelta = new Vector2(width, height);
        root.localPosition = new Vector3(0f, yOffset, 0f);
        root.localScale = Vector3.one * WorldScale;

        // gold frame -> dark trough -> coloured fill, each inset inside the last
        AddImage(root, HudStyle.Gold);

        RectTransform trough = NewRect("Trough", root);
        Stretch(trough, 1f);
        AddImage(trough, HudStyle.BarTrough);

        // fill is left-anchored so shrinking sizeDelta.x drains it rightward,
        // which avoids needing a sprite for Image.Type.Filled
        RectTransform fill = NewRect("Fill", trough);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        fill.sizeDelta = new Vector2(width, 0f);
        AddImage(fill, fillColor);
        fillRect = fill;

        BuildSegments(trough, target.Max, hpPerSegment, width);
        Refresh();
    }

    /// <summary>
    /// Thin dark ticks every hpPerSegment of max health - the most recognisable
    /// trait of a LoL health bar, and it doubles as a readable scale for how
    /// chunky any given hit was.
    /// </summary>
    void BuildSegments(RectTransform parent, float maxHp, float hpPerSegment, float width)
    {
        if (hpPerSegment <= 0f || maxHp <= hpPerSegment) return;

        int count = Mathf.FloorToInt(maxHp / hpPerSegment);
        if (count > 40) return;   // absurd max health - the ticks would just be noise

        for (int i = 1; i <= count; i++)
        {
            float hp = i * hpPerSegment;
            if (hp >= maxHp) break;

            RectTransform tick = NewRect("Tick", parent);
            tick.anchorMin = new Vector2(0f, 0f);
            tick.anchorMax = new Vector2(0f, 1f);
            tick.pivot = new Vector2(0.5f, 0.5f);
            tick.sizeDelta = new Vector2(1.5f, 0f);
            tick.anchoredPosition = new Vector2(width * (hp / maxHp), 0f);
            AddImage(tick, HudStyle.BarTick);
        }
    }

    void LateUpdate()
    {
        // Health now keeps the corpse alive for its death animation, so go by
        // IsAlive rather than waiting for the object to vanish - otherwise a
        // full-width empty bar hangs over the body the whole time it collapses
        if (health == null || !health.IsAlive) { Destroy(gameObject); return; }

        if (cam == null) cam = Camera.main;

        // Billboard by copying the camera's rotation outright rather than
        // LookAt, so every bar stays parallel and none skew at screen edges.
        // This also cancels the unit's own rotation - FaceTarget() spins the champion.
        if (cam != null) transform.rotation = cam.transform.rotation;

        Refresh();
    }

    void Refresh()
    {
        if (fillRect == null || health == null) return;

        float pct = health.Max <= 0f ? 0f : Mathf.Clamp01(health.Current / health.Max);
        fillRect.sizeDelta = new Vector2(fullWidth * pct, 0f);
    }

    // ---------- tiny UI builders ----------

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image AddImage(RectTransform rt, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;   // world-space bars must never eat clicks
        return img;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
