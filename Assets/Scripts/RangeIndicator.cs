using UnityEngine;

/// <summary>
/// LoL-style attack range indicator: a crisp edge ring plus a soft filled disc
/// that fades from the rim inward. Fully procedural - no prefab, texture, or shader.
/// The disc is built as a unit circle and scaled, so radius changes are free.
/// </summary>
public class RangeIndicator : MonoBehaviour
{
    [Header("Edge Ring")]
    [SerializeField] Color ringColor = new Color(0.62f, 0.88f, 1f, 0.95f);
    [SerializeField] float lineWidth = 0.09f;
    [SerializeField] int segments = 72;

    [Header("Inner Fill")]
    [SerializeField] bool showFill = true;
    [SerializeField] Color fillColor = new Color(0.62f, 0.88f, 1f);
    [SerializeField, Range(0f, 1f)] float centerAlpha = 0.03f;
    [SerializeField, Range(0f, 1f)] float rimAlpha = 0.20f;
    [SerializeField, Range(0.5f, 6f)] float falloff = 2.6f;   // higher = glow hugs the rim
    [SerializeField, Range(2, 24)] int radialSteps = 14;      // gradient smoothness

    [Header("Placement")]
    [SerializeField] float yOffset = 0.05f;
    [SerializeField] float fillYOffset = 0.04f;               // just under the ring

    Transform root;
    Transform ringT;
    LineRenderer ring;
    MeshRenderer fillRenderer;
    Mesh fillMesh;
    float radius = 1f;
    bool built;

    void Awake()
    {
        if (!built) Build();
    }

    // ---------- construction ----------

    void Build()
    {
        built = true;

        var rootGo = new GameObject("RangeIndicator");
        rootGo.transform.SetParent(transform, false);
        root = rootGo.transform;

        BuildRing();
        if (showFill) BuildFill();

        rootGo.SetActive(false);
    }

    void BuildRing()
    {
        var go = new GameObject("EdgeRing");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, yOffset, 0f);
        // points are generated in local XY, so tip the object flat onto the ground
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ringT = go.transform;

        ring = go.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = segments;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.alignment = LineAlignment.TransformZ;

        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
        }

        ring.material = MakeMaterial();
        ring.startColor = ringColor;
        ring.endColor = ringColor;
    }

    void BuildFill()
    {
        var go = new GameObject("Fill");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, fillYOffset, 0f);

        var mf = go.AddComponent<MeshFilter>();
        fillRenderer = go.AddComponent<MeshRenderer>();
        fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fillRenderer.receiveShadows = false;
        fillRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

        fillMesh = new Mesh { name = "RangeFill" };
        mf.sharedMesh = fillMesh;

        RebuildFillMesh();

        fillRenderer.material = MakeMaterial();
    }

    /// <summary>
    /// Concentric rings of vertices in the XZ plane. Alpha ramps from centerAlpha
    /// at the middle to rimAlpha at the edge, shaped by 'falloff'.
    /// </summary>
    void RebuildFillMesh()
    {
        int rings = Mathf.Max(2, radialSteps);
        int vertsPerRing = segments;
        int vertCount = (rings + 1) * vertsPerRing;

        var verts = new Vector3[vertCount];
        var colors = new Color[vertCount];
        var tris = new int[rings * vertsPerRing * 6];

        for (int r = 0; r <= rings; r++)
        {
            float t = r / (float)rings;               // 0 at center, 1 at rim
            float a = Mathf.Lerp(centerAlpha, rimAlpha, Mathf.Pow(t, falloff));

            Color c = fillColor;
            c.a = a;

            for (int s = 0; s < vertsPerRing; s++)
            {
                float ang = (s / (float)vertsPerRing) * Mathf.PI * 2f;
                int idx = r * vertsPerRing + s;
                verts[idx] = new Vector3(Mathf.Cos(ang) * t, 0f, Mathf.Sin(ang) * t);
                colors[idx] = c;
            }
        }

        int ti = 0;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < vertsPerRing; s++)
            {
                int next = (s + 1) % vertsPerRing;
                int a0 = r * vertsPerRing + s;
                int a1 = r * vertsPerRing + next;
                int b0 = (r + 1) * vertsPerRing + s;
                int b1 = (r + 1) * vertsPerRing + next;

                tris[ti++] = a0; tris[ti++] = b0; tris[ti++] = b1;
                tris[ti++] = a0; tris[ti++] = b1; tris[ti++] = a1;
            }
        }

        fillMesh.Clear();
        fillMesh.vertices = verts;
        fillMesh.colors = colors;
        fillMesh.triangles = tris;
        fillMesh.RecalculateBounds();
    }

    Material MakeMaterial()
    {
        Shader s = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Unlit/Color");

        var m = new Material(s);
        // transparent, no depth write, so overlapping rings don't punch holes in each other
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
        return m;
    }

    // ---------- public API ----------

    public void SetRadius(float r)
    {
        if (!built) Build();

        radius = Mathf.Max(0.01f, r);
        root.localScale = new Vector3(radius, 1f, radius);

        // ring lives in a rotated child, so its scale axes are XY, not XZ
        ringT.localScale = Vector3.one;
        ring.widthMultiplier = lineWidth / radius;
    }

    public void SetColor(Color c)
    {
        if (!built) Build();

        ringColor = c;
        ring.startColor = c;
        ring.endColor = c;

        fillColor = new Color(c.r, c.g, c.b);
        if (fillMesh != null) RebuildFillMesh();
    }

    public void Show(bool visible)
    {
        if (!built) Build();
        if (root.gameObject.activeSelf != visible)
            root.gameObject.SetActive(visible);
    }

    public bool IsVisible => root != null && root.gameObject.activeSelf;
}