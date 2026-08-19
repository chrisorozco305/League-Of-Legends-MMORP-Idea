using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// League of Legends style top-down camera. New Input System version.
/// Attach to your Camera. Set 'target' to the player transform (optional).
/// </summary>
[RequireComponent(typeof(Camera))]
public class MobaCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Rig")]
    [SerializeField] float pitch = 55f;
    [SerializeField] float yaw = 0f;
    [SerializeField] float groundHeight = 0f;

    [Header("Zoom")]
    [SerializeField] float distance = 18f;
    [SerializeField] float minDistance = 12f;
    [SerializeField] float maxDistance = 26f;
    [SerializeField] float zoomStep = 2f;
    [SerializeField] float zoomSmooth = 12f;

    [Header("Pan")]
    [SerializeField] float panSpeed = 28f;
    [SerializeField] int edgeSize = 14;
    [SerializeField] bool edgePanInEditor = true;
    [SerializeField] bool arrowKeyPan = true;

    [Header("Follow")]
    [SerializeField] float followSmooth = 0.09f;

    [Header("Keys")]
    [Tooltip("Tap to recentre on the champion, hold to keep following.")]
    [SerializeField] Key centerKey = Key.Space;
    [Tooltip("Toggles permanent camera lock to the champion.")]
    [SerializeField] Key lockKey = Key.Y;

    [Header("Bounds (x = world X, y = world Z)")]
    [SerializeField] bool useBounds = true;
    [SerializeField] Rect bounds = new Rect(-60f, -60f, 120f, 120f);

    Vector3 focus;
    Vector3 followVel;
    float currentDistance;
    bool locked = true;

    void Awake()
    {
        currentDistance = distance;
        focus = target ? Flatten(target.position) : Vector3.zero;
        ApplyTransform();
    }

    void LateUpdate()
    {
        HandleLockInput();
        HandleCenterInput();
        HandleZoom();

        bool centering = locked || (Keyboard.current != null && Keyboard.current[centerKey].isPressed);
        if (centering && target) FollowTarget();
        else Pan();

        ClampFocus();
        ApplyTransform();
    }

    // ---------- input ----------

    void HandleLockInput()
    {
        if (Keyboard.current != null && Keyboard.current[lockKey].wasPressedThisFrame)
            locked = !locked;
    }

    /// <summary>
    /// Space centres on the champion, League-style: snap on the press, then
    /// keep following for as long as it's held.
    ///
    /// The press matters on its own. Holding already fed FollowTarget(), but
    /// that SmoothDamps over followSmooth - so a quick tap advanced the ease by
    /// a single frame and looked like nothing happened. Snapping first makes
    /// the tap read as an instant recentre and leaves the hold unchanged.
    /// </summary>
    void HandleCenterInput()
    {
        if (Keyboard.current == null || !target) return;
        if (!Keyboard.current[centerKey].wasPressedThisFrame) return;

        SnapToTarget();
    }

    void HandleZoom()
    {
        if (Mouse.current != null)
        {
            float raw = Mouse.current.scroll.ReadValue().y;
            // Input System reports either +/-1 or +/-120 depending on version/platform.
            // Treat any nonzero value as one notch.
            if (Mathf.Abs(raw) > 0.01f)
                distance = Mathf.Clamp(distance - Mathf.Sign(raw) * zoomStep, minDistance, maxDistance);
        }

        currentDistance = Mathf.Lerp(currentDistance, distance,
            1f - Mathf.Exp(-zoomSmooth * Time.unscaledDeltaTime));
    }

    void FollowTarget()
    {
        focus = Vector3.SmoothDamp(focus, Flatten(target.position), ref followVel, followSmooth);
    }

    void Pan()
    {
        Vector2 dir = Vector2.zero;

        if (EdgePanAllowed() && Mouse.current != null)
        {
            Vector2 m = Mouse.current.position.ReadValue();
            bool inWindow = m.x >= 0 && m.y >= 0 && m.x <= Screen.width && m.y <= Screen.height;
            if (inWindow)
            {
                if (m.x <= edgeSize) dir.x -= 1f;
                if (m.x >= Screen.width - edgeSize) dir.x += 1f;
                if (m.y <= edgeSize) dir.y -= 1f;
                if (m.y >= Screen.height - edgeSize) dir.y += 1f;
            }
        }

        if (arrowKeyPan && Keyboard.current != null)
        {
            var k = Keyboard.current;
            if (k.leftArrowKey.isPressed) dir.x -= 1f;
            if (k.rightArrowKey.isPressed) dir.x += 1f;
            if (k.downArrowKey.isPressed) dir.y -= 1f;
            if (k.upArrowKey.isPressed) dir.y += 1f;
        }

        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
        Vector3 move = (flat * Vector3.right * dir.x + flat * Vector3.forward * dir.y).normalized;

        float scale = currentDistance / maxDistance;
        focus += move * panSpeed * scale * Time.unscaledDeltaTime;
        followVel = Vector3.zero;
    }

    bool EdgePanAllowed()
    {
#if UNITY_EDITOR
        return edgePanInEditor;
#else
        return Application.isFocused;
#endif
    }

    // ---------- transform ----------

    void ClampFocus()
    {
        if (!useBounds) return;
        focus.x = Mathf.Clamp(focus.x, bounds.xMin, bounds.xMax);
        focus.z = Mathf.Clamp(focus.z, bounds.yMin, bounds.yMax);
    }

    void ApplyTransform()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rot;
        transform.position = focus - (rot * Vector3.forward) * currentDistance;
    }

    Vector3 Flatten(Vector3 p) => new Vector3(p.x, groundHeight, p.z);

    // ---------- public API ----------

    /// <summary>Jump the camera to a world position (minimap click, ping, ward jump).</summary>
    public void FocusOn(Vector3 worldPos)
    {
        focus = Flatten(worldPos);
        followVel = Vector3.zero;
        locked = false;
        ClampFocus();
        ApplyTransform();
    }

    /// <summary>Instantly recenter on the target with no easing (respawn, round start).</summary>
    public void SnapToTarget()
    {
        if (!target) return;
        focus = Flatten(target.position);
        followVel = Vector3.zero;   // kill any in-flight SmoothDamp velocity
        ClampFocus();               // a champion outside bounds must not drag the camera out
        ApplyTransform();
    }

    public void SetLocked(bool value) => locked = value;
    public bool IsLocked => locked;

    void OnDrawGizmosSelected()
    {
        if (!useBounds) return;
        Gizmos.color = Color.yellow;
        Vector3 c = new Vector3(bounds.center.x, groundHeight, bounds.center.y);
        Gizmos.DrawWireCube(c, new Vector3(bounds.width, 0.1f, bounds.height));
    }
}