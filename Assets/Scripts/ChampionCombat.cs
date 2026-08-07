using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Ranged auto-attack for a gun champion.
///   A            - TOGGLE the attack range ring (red = attack mode armed)
///   Left click   - while armed: attack nearest enemy, then dismiss the ring
///   Right click  - cancel / move order, dismisses the ring
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ChampionCombat : MonoBehaviour
{
    [Header("Combat")]
    [SerializeField] float attackRange = 8f;
    [SerializeField] float attacksPerSecond = 0.85f;
    [SerializeField] float damage = 15f;
    [SerializeField] LayerMask enemyMask = ~0;

    [Header("Projectile")]
    [SerializeField] float projectileSpeed = 28f;
    [SerializeField] Color projectileColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] Transform muzzle;

    [Header("Range Ring")]
    [SerializeField] Key toggleRangeKey = Key.A;
    [SerializeField] Color rangeColor = new Color(0.62f, 0.88f, 1f, 0.95f);   // light blue

    [Header("Facing")]
    [SerializeField] float turnSpeed = 20f;

    [Header("Targeting")]
    [SerializeField] Camera cam;
    [SerializeField] float targetPickRange = 500f;

    NavMeshAgent agent;
    RangeIndicator ring;
    CursorManager cursors;
    ClickIndicator indicator;
    ChampionController controller;
    Transform target;
    float cooldown;
    bool armed;          // A has been toggled on

    public float AttackRange => attackRange;
    public Transform Target => target;
    public bool IsArmed => armed;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        ring = GetComponent<RangeIndicator>();
        if (!ring) ring = gameObject.AddComponent<RangeIndicator>();
        ring.SetRadius(attackRange);
        ring.SetColor(rangeColor);
        ring.Show(false);

        cursors = FindFirstObjectByType<CursorManager>();
        indicator = FindFirstObjectByType<ClickIndicator>();
        controller = GetComponent<ChampionController>();
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        cooldown -= Time.deltaTime;

        HandleToggle();
        HandleArmedClick();

        ValidateTarget();
        if (target != null) TickCombat();
    }

    // ---------- range toggle ----------

    void HandleToggle()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current[toggleRangeKey].wasPressedThisFrame)
            SetArmed(!armed);
    }

    void SetArmed(bool value)
    {
        armed = value;
        ring.Show(armed);
        cursors?.SetAttackOverride(armed);
    }

    /// <summary>Called by ChampionController when a right-click move order is issued.</summary>
    public void CancelOrders()
    {
        target = null;
        if (armed) SetArmed(false);
    }

    // ---------- input while armed ----------

    void HandleArmedClick()
    {
        if (!armed || Mouse.current == null) return;

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        // 1. clicked directly on an enemy - that one wins, even if it isn't closest
        Transform picked = PickEnemyUnderCursor();
        if (picked != null)
        {
            target = picked;
            indicator?.PlayAttack(picked.position);
            SetArmed(false);
            return;
        }

        // 2. clicked empty ground - fall back to nearest enemy in range
        Transform nearest = FindNearest();
        if (nearest != null)
        {
            target = nearest;
            indicator?.PlayAttack(nearest.position);
            SetArmed(false);
            return;
        }

        // 3. nothing to attack - attack-move to the clicked point, still red
        target = null;
        if (TryMoveToCursor(out Vector3 dest))
            indicator?.PlayAttack(dest);

        SetArmed(false);
    }

    /// <summary>
    /// Returns the enemy directly under the cursor, or null.
    /// Deliberately ignores attackRange - clicking a distant enemy issues a
    /// chase order, same as League. TickCombat walks into range before firing.
    /// </summary>
    Transform PickEnemyUnderCursor()
    {
        if (cam == null || Mouse.current == null) return null;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, targetPickRange, enemyMask)) return null;

        var h = hit.collider.GetComponentInParent<Health>();
        if (h == null || !h.IsAlive || h.transform == transform) return null;

        return h.transform;
    }

    /// <summary>Move toward the cursor's ground position. Returns the snapped destination.</summary>
    bool TryMoveToCursor(out Vector3 dest)
    {
        dest = Vector3.zero;
        if (controller == null || Mouse.current == null) return false;

        if (!controller.TryGetGroundPoint(Mouse.current.position.ReadValue(), out Vector3 point))
            return false;

        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return false;

        agent.SetDestination(hit.position);
        dest = hit.position;
        return true;
    }

    // ---------- targeting ----------

    void ValidateTarget()
    {
        if (target == null) return;

        var h = target.GetComponent<Health>();
        if (h == null || !h.IsAlive) target = null;
    }

    Transform FindNearest()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange, enemyMask);
        float best = float.MaxValue;
        Transform found = null;

        foreach (var c in hits)
        {
            var h = c.GetComponentInParent<Health>();
            if (h == null || !h.IsAlive || h.transform == transform) continue;

            float d = (h.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; found = h.transform; }
        }

        return found;
    }

    // ---------- attacking ----------

    void TickCombat()
    {
        float dist = Vector3.Distance(transform.position, target.position);

        if (dist > attackRange)
        {
            agent.SetDestination(target.position);
            return;
        }

        agent.ResetPath();
        FaceTarget();

        if (cooldown <= 0f)
        {
            Fire();
            cooldown = 1f / Mathf.Max(0.01f, attacksPerSecond);
        }
    }

    void FaceTarget()
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
    }

    void Fire()
    {
        Vector3 origin = muzzle
            ? muzzle.position
            : transform.position + Vector3.up * 1.1f + transform.forward * 0.4f;

        Projectile.Spawn(origin, target, projectileSpeed, damage, projectileColor);
    }

    // ---------- public API ----------

    /// <summary>Order an attack on a specific target (right-click on enemy).</summary>
    public void AttackTarget(Transform t)
    {
        var h = t ? t.GetComponent<Health>() : null;
        if (h == null || !h.IsAlive) return;

        target = t;
        if (armed) SetArmed(false);
    }

    void OnValidate()
    {
        if (ring != null)
        {
            ring.SetRadius(attackRange);
            ring.SetColor(rangeColor);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.62f, 0.88f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}