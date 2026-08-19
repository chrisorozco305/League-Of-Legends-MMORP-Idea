using UnityEngine;

/// <summary>
/// Brings a static fireball mesh to life: spin, pulse, emissive flicker, and a
/// flickering point light.
///
/// The model ships with no animation takes, so all of this is procedural -
/// nothing to author in Blender and nothing to re-export when the numbers
/// change. Same approach as RangeIndicator and ClickIndicator.
///
/// Every instance picks a random phase on Awake. Without it a volley of
/// fireballs pulses and flickers in perfect lockstep, which instantly reads as
/// fake - the eye catches the synchrony long before it catches the motion.
///
/// Emission goes through a MaterialPropertyBlock rather than renderer.material,
/// which would clone the material per projectile and break batching.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class FireballEffect : MonoBehaviour
{
    [Header("Spin")]
    [Tooltip("Degrees per second. Z is the direction of travel, so Z spin reads as roll.")]
    [SerializeField] Vector3 spin = new Vector3(60f, 0f, 520f);

    [Header("Pulse")]
    [Tooltip("Fraction of base scale the pulse swings by.")]
    [SerializeField] float pulseAmount = 0.09f;
    [SerializeField] float pulseSpeed = 7f;

    [Header("Emissive Flicker")]
    [SerializeField] bool flicker = true;
    [SerializeField] Color emissionColor = new Color(1f, 0.42f, 0.08f);
    [SerializeField] float emissionMin = 1.8f;
    [SerializeField] float emissionMax = 4.6f;
    [Tooltip("Higher is a more frantic flicker.")]
    [SerializeField] float flickerSpeed = 13f;

    [Header("Light")]
    [SerializeField] bool castLight = true;
    [SerializeField] Color lightColor = new Color(1f, 0.55f, 0.18f);
    [SerializeField] float lightRange = 4.5f;
    [SerializeField] float lightIntensityMin = 1.4f;
    [SerializeField] float lightIntensityMax = 3.2f;

    [Header("Spawn")]
    [Tooltip("Seconds to grow from nothing to full size, so it ignites rather than popping in.")]
    [SerializeField] float igniteTime = 0.1f;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    Renderer rend;
    MaterialPropertyBlock mpb;
    Light glow;

    Vector3 baseScale;
    float phase;
    float age;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;

        // de-sync this instance from every other fireball in flight
        phase = Random.Range(0f, 100f);

        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (castLight)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(transform, false);

            glow = go.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = lightColor;
            glow.range = lightRange;
            glow.shadows = LightShadows.None;   // a projectile shouldn't cost a shadow map
        }
    }

    void OnEnable()
    {
        age = 0f;
        transform.localScale = igniteTime > 0f ? Vector3.zero : baseScale;
    }

    void Update()
    {
        age += Time.deltaTime;

        transform.Rotate(spin * Time.deltaTime, Space.Self);

        // Perlin rather than a sine: fire is irregular, and a clean sine reads
        // as a pulsing balloon instead of a flame.
        float noise = Mathf.PerlinNoise(phase, Time.time * flickerSpeed);

        float ignite = igniteTime > 0f ? Mathf.Clamp01(age / igniteTime) : 1f;
        float pulse = 1f + Mathf.Sin((Time.time + phase) * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * pulse * Mathf.SmoothStep(0f, 1f, ignite);

        if (flicker)
        {
            float intensity = Mathf.Lerp(emissionMin, emissionMax, noise);
            rend.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionId, emissionColor * intensity);
            rend.SetPropertyBlock(mpb);
        }

        if (glow != null)
            glow.intensity = Mathf.Lerp(lightIntensityMin, lightIntensityMax, noise) * ignite;
    }
}
