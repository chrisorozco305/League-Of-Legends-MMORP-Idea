using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// LoL-style right-click move marker. Draws a shrinking, fading ring on the ground
/// at the clicked point. Fully procedural - no prefab or material needed.
/// Attach to any GameObject in the scene.
/// </summary>
public class ClickIndicator : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] Camera cam;
    [SerializeField] float groundHeight = 0f;
    [SerializeField] bool useColliders = false;      // true = raycast real geometry
    [SerializeField] LayerMask groundMask = ~0;

    [Header("Look")]
    [SerializeField] Color color = new Color(0.2f, 1f, 0.35f);
    [SerializeField] float radius = 0.6f;
    [SerializeField] float startScale = 1.6f;
    [SerializeField] float endScale = 0.55f;
    [SerializeField] float duration = 0.45f;
    [SerializeField] float lineWidth = 0.07f;
    [SerializeField] int segments = 48;
    [SerializeField] float yOffset = 0.02f;          // lift off the ground to avoid z-fighting

    Transform marker;
    LineRenderer ring;
    float t = -1f;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        BuildMarker();
    }

    void Update()
    {
        Animate();
    }

    // ---------- public API ----------

    /// <summary>Fire the indicator manually (AI orders, ability targeting, tests).</summary>
    public void Play(Vector3 worldPos)
    {
        marker.position = worldPos + Vector3.up * yOffset;
        marker.gameObject.SetActive(true);
        t = 0f;
    }

    /// <summary>Screen point -> world point on the ground.</summary>
    public bool TryGetGroundPoint(Vector2 screenPos, out Vector3 point)
    {
        point = Vector3.zero;
        if (!cam) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);

        if (useColliders)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
            {
                point = hit.point;
                return true;
            }
            return false;
        }

        // flat math plane - no colliders required
        Plane plane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
        if (plane.Raycast(ray, out float dist))
        {
            point = ray.GetPoint(dist);
            return true;
        }
        return false;
    }

    // ---------- internals ----------

    void BuildMarker()
    {
        var go = new GameObject("ClickMarker");
        go.transform.SetParent(transform, false);
        marker = go.transform;

        ring = go.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = segments;
        ring.widthMultiplier = lineWidth;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.alignment = LineAlignment.TransformZ;   // lie flat on the ground

        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        // rotate flat so TransformZ alignment faces up
        marker.rotation = Quaternion.Euler(90f, 0f, 0f);

        Shader s = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Unlit/Color");
        ring.material = new Material(s);

        go.SetActive(false);
    }

    void Animate()
    {
        if (t < 0f) return;

        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / duration);

        // ease out
        float e = 1f - Mathf.Pow(1f - p, 3f);

        float s = Mathf.Lerp(startScale, endScale, e);
        marker.localScale = new Vector3(s, s, s);

        Color c = color;
        c.a = 1f - e;
        ring.startColor = c;
        ring.endColor = c;

        if (p >= 1f)
        {
            t = -1f;
            marker.gameObject.SetActive(false);
        }
    }
}