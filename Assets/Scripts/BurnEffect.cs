using UnityEngine;

/// <summary>
/// Damage-over-time burn, applied by the Wizard's fireball on impact.
///
/// Refreshes rather than stacks: hitting a burning target resets the timer and
/// keeps the stronger tick instead of piling on components. Stacking DoTs is a
/// balance decision, not a default - and N components each running their own
/// timer is how a prototype quietly ends up doing six times the damage.
///
/// The victim tints toward ember orange and carries a flickering light while it
/// burns. Tint goes through a MaterialPropertyBlock, so the monster's shared
/// material is never cloned.
/// </summary>
[DisallowMultipleComponent]
public class BurnEffect : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] Color emberTint = new Color(1f, 0.35f, 0.12f);
    [SerializeField] float tintStrength = 0.55f;

    Health health;
    Renderer[] renderers;
    MaterialPropertyBlock mpb;
    Color[] originalColors;
    int colorProperty;

    Light glow;
    float remaining;
    float damagePerSecond;
    float tickInterval = 0.5f;
    float tickTimer;
    float phase;

    /// <summary>
    /// Sets <paramref name="target"/> alight, or refreshes an existing burn.
    /// Safe on a null or dead target - it simply does nothing.
    /// </summary>
    public static void Apply(Health target, float duration, float totalDamage, float tickInterval)
    {
        if (target == null || !target.IsAlive) return;
        if (duration <= 0f || totalDamage <= 0f) return;

        var burn = target.GetComponent<BurnEffect>();
        if (burn == null) burn = target.gameObject.AddComponent<BurnEffect>();

        burn.Ignite(target, duration, totalDamage / duration, tickInterval);
    }

    void Ignite(Health target, float duration, float dps, float interval)
    {
        health = target;

        // refresh: keep the longer remaining time and the stronger burn
        remaining = Mathf.Max(remaining, duration);
        damagePerSecond = Mathf.Max(damagePerSecond, dps);
        tickInterval = Mathf.Max(0.05f, interval);

        if (renderers == null) CaptureRenderers();
        if (glow == null) CreateGlow();
    }

    void CaptureRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        mpb = new MaterialPropertyBlock();
        originalColors = new Color[renderers.Length];
        phase = Random.Range(0f, 100f);

        // URP Lit uses _BaseColor, built-in shaders use _Color. Decide once
        // rather than probing every frame.
        colorProperty = BaseColorId;
        foreach (var r in renderers)
        {
            if (r == null || r.sharedMaterial == null) continue;
            if (!r.sharedMaterial.HasProperty(BaseColorId) && r.sharedMaterial.HasProperty(ColorId))
                colorProperty = ColorId;
            break;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            var mat = renderers[i] != null ? renderers[i].sharedMaterial : null;
            originalColors[i] = mat != null && mat.HasProperty(colorProperty)
                ? mat.GetColor(colorProperty)
                : Color.white;
        }
    }

    void CreateGlow()
    {
        var go = new GameObject("BurnGlow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.8f;

        glow = go.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.45f, 0.15f);
        glow.range = 3.5f;
        glow.shadows = LightShadows.None;
    }

    void Update()
    {
        // Health keeps the corpse around for its death animation, but stop
        // burning the instant it dies rather than ticking a body
        if (health == null || !health.IsAlive) { Extinguish(); return; }

        remaining -= Time.deltaTime;
        if (remaining <= 0f) { Extinguish(); return; }

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval;
            health.TakeDamage(damagePerSecond * tickInterval);
        }

        float flicker = Mathf.PerlinNoise(phase, Time.time * 11f);

        if (glow != null) glow.intensity = Mathf.Lerp(0.8f, 2.2f, flicker);

        float t = tintStrength * Mathf.Lerp(0.6f, 1f, flicker);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(colorProperty, Color.Lerp(originalColors[i], emberTint, t));
            renderers[i].SetPropertyBlock(mpb);
        }
    }

    void Extinguish()
    {
        // hand the original colours back - a survivor must not stay orange
        if (renderers != null && mpb != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(mpb);
                mpb.SetColor(colorProperty, originalColors[i]);
                renderers[i].SetPropertyBlock(mpb);
            }
        }

        if (glow != null) Destroy(glow.gameObject);
        Destroy(this);
    }

    void OnDestroy()
    {
        if (glow != null) Destroy(glow.gameObject);
    }
}
