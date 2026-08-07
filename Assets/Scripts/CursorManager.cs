using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Swaps the hardware cursor based on what's under the mouse.
/// Default cursor normally, attack cursor when hovering anything with Health.
/// </summary>
public class CursorManager : MonoBehaviour
{
    [Header("Textures")]
    [SerializeField] Texture2D normalCursor;
    [SerializeField] Texture2D attackCursor;

    [Header("Hotspots (pixels from top-left of the texture)")]
    [SerializeField] Vector2 normalHotspot = new Vector2(4f, 2f);
    [SerializeField] Vector2 attackHotspot = new Vector2(4f, 2f);

    [Header("Mode")]
    [Tooltip("Software mode renders the cursor in-engine. Slower but works when the OS rejects the texture.")]
    [SerializeField] bool forceSoftwareCursor = false;

    [Header("Detection")]
    [SerializeField] Camera cam;
    [SerializeField] float rayDistance = 500f;
    [SerializeField] LayerMask hoverMask = ~0;
    [SerializeField] float checkInterval = 0.05f;

    bool attackState;
    bool overrideAttack;
    float nextCheck;

    void Awake()
    {
        if (!cam) cam = Camera.main;

        if (normalCursor == null)
            Debug.LogWarning("[CursorManager] Normal Cursor texture is not assigned.", this);
        if (attackCursor == null)
            Debug.LogWarning("[CursorManager] Attack Cursor texture is not assigned.", this);

        Cursor.visible = true;
        Apply(false, true);
    }

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + checkInterval;

        Apply(overrideAttack || IsHoveringEnemy(), false);
    }

    bool IsHoveringEnemy()
    {
        if (cam == null || Mouse.current == null) return false;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, hoverMask)) return false;

        var h = hit.collider.GetComponentInParent<Health>();
        return h != null && h.IsAlive && h.gameObject != gameObject;
    }

    void Apply(bool attack, bool force)
    {
        if (!force && attack == attackState) return;
        attackState = attack;

        Texture2D tex = attack ? attackCursor : normalCursor;
        Vector2 spot = attack ? attackHotspot : normalHotspot;
        if (tex == null) return;

        Cursor.SetCursor(tex, spot, forceSoftwareCursor ? CursorMode.ForceSoftware : CursorMode.Auto);
    }

    /// <summary>Force the attack cursor on, e.g. while attack-mode is armed.</summary>
    public void SetAttackOverride(bool on)
    {
        overrideAttack = on;
        Apply(on || IsHoveringEnemy(), false);
    }
}