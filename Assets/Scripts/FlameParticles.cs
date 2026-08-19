using UnityEngine;

/// <summary>
/// Runtime-built flame particle system, used by BurnEffect.
///
/// Both the sprite texture and the material are generated in code and shared
/// statically across every instance - the project has no particle art, and
/// building one soft dot once beats importing a texture (and beats every
/// burning monster allocating its own material and breaking batching).
///
/// Simulates in world space so flames trail behind a monster that walks while
/// burning, instead of travelling rigidly with it like a hat.
/// </summary>
public class FlameParticles : MonoBehaviour
{
    static Texture2D sharedTexture;
    static Material sharedMaterial;

    ParticleSystem ps;

    /// <summary>Attaches a flame system to <paramref name="parent"/>, scaled to the given body size.</summary>
    public static FlameParticles Create(Transform parent, float radius, float height)
    {
        var go = new GameObject("Flames");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * height * 0.35f;

        var fp = go.AddComponent<FlameParticles>();
        fp.Build(radius, height);
        return fp;
    }

    void Build(float radius, float height)
    {
        ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(height * 0.6f, height * 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.6f, radius * 1.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.15f;              // gentle lift on top of start speed
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.75f, 0.25f), new Color(1f, 0.35f, 0.08f));

        var emission = ps.emission;
        emission.rateOverTime = 28f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius * 0.55f;

        // fade from bright yellow through ember red to nothing
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f),
                new GradientColorKey(new Color(1f, 0.40f, 0.10f), 0.45f),
                new GradientColorKey(new Color(0.45f, 0.08f, 0.02f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.18f),
                new GradientAlphaKey(0f, 1f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // taper as they rise so the plume narrows to a wisp
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.55f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.15f)));

        var rend = GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material = GetSharedMaterial();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.sortingOrder = 1;

        ps.Play();
    }

    /// <summary>
    /// Stops emitting and cleans up once the last particle has died. Destroying
    /// outright would pop the whole plume out of existence mid-frame.
    /// </summary>
    public void StopAndFade()
    {
        if (ps == null) { Destroy(gameObject); return; }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(gameObject, ps.main.startLifetime.constantMax + 0.1f);
    }

    // ---------- shared procedural assets ----------

    static Material GetSharedMaterial()
    {
        if (sharedMaterial != null) return sharedMaterial;

        // URP's particle shader first, then the same fallback chain the other
        // procedural visuals in this project use
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        sharedMaterial = new Material(shader) { name = "FlameParticle (runtime)" };

        // URP names its albedo slot _BaseMap; Sprites/Default uses _MainTex.
        // Material.mainTexture usually resolves this via the [MainTexture]
        // attribute, but set it outright rather than trusting that.
        var tex = GetSharedTexture();
        if (sharedMaterial.HasProperty("_BaseMap")) sharedMaterial.SetTexture("_BaseMap", tex);
        if (sharedMaterial.HasProperty("_MainTex")) sharedMaterial.SetTexture("_MainTex", tex);
        sharedMaterial.mainTexture = tex;

        // additive where the shader exposes URP's surface/blend options - fire
        // should add light, not occlude what is behind it
        if (sharedMaterial.HasProperty("_Surface"))
        {
            sharedMaterial.SetFloat("_Surface", 1f);   // transparent
            sharedMaterial.SetFloat("_Blend", 2f);     // additive
            sharedMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            sharedMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            sharedMaterial.SetFloat("_ZWrite", 0f);

            // the floats above only drive the inspector; URP needs the keyword
            // to actually compile the transparent path
            sharedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sharedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        return sharedMaterial;
    }

    /// <summary>A soft radial dot - the one texture every flame particle uses.</summary>
    static Texture2D GetSharedTexture()
    {
        if (sharedTexture != null) return sharedTexture;

        const int size = 64;
        sharedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "FlameDot (runtime)",
            wrapMode = TextureWrapMode.Clamp
        };

        float centre = (size - 1) * 0.5f;
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre)) / centre;
                // squared falloff keeps a hot core with a soft edge
                float a = Mathf.Clamp01(1f - dist);
                a *= a;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        sharedTexture.SetPixels32(pixels);
        sharedTexture.Apply();
        return sharedTexture;
    }
}
