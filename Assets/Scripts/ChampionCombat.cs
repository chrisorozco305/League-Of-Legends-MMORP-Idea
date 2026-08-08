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
    public float Damage => damage;
    public float AttacksPerSecond => attacksPerSecond;
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

        // marker always lands where the cursor actually is, not on the
        // target's position - ground point under an enemy is a close enough
        // stand-in for "the spot that was clicked"
        Vector3 clickPoint = Vector3.zero;
        bool haveClickPoint = controller != null &&
            controller.TryGetGroundPoint(Mouse.current.position.ReadValue(), out clickPoint);

        // 1. clicked directly on an enemy - that one wins, even if it isn't closest
        Transform picked = PickEnemyUnderCursor();
        if (picked != null)
        {
            target = picked;
            if (haveClickPoint) indicator?.PlayAttack(clickPoint);
            SetArmed(false);
            return;
        }

        // 2. clicked empty ground - fall back to the nearest enemy anywhere;
        // TickCombat() will chase it into range before firing
        Transform nearest = FindNearest();
        if (nearest != null)
        {
            target = nearest;
            if (haveClickPoint) indicator?.PlayAttack(clickPoint);
            SetArmed(false);
            return;
        }

        // 3. nothing to attack - attack-move to the clicked point, still red
        target = null;
        if (haveClickPoint)
        {
            if (TryMoveToPoint(clickPoint))
                indicator?.PlayAttack(clickPoint);
        }

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

    /// <summary>Move toward a ground point already snapped to the navmesh. Returns success.</summary>
    bool TryMoveToPoint(Vector3 point)
    {
        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return false;

        agent.SetDestination(hit.position);
        return true;
    }

    // ---------- targeting ----------

    void ValidateTarget()
    {
        if (target == null) return;

        var h = target.GetComponent<Health>();
        if (h == null || !h.IsAlive) target = null;
    }

    /// <summary>
    /// Nearest living enemy anywhere on the enemy layer, not just within
    /// attackRange - clicking empty ground should still pick a chase target,
    /// same as clicking directly on a distant enemy.
    /// </summary>
    Transform FindNearest()
    {
        Health[] all = FindObjectsByType<Health>(FindObjectsSortMode.None);
        float best = float.MaxValue;
        Transform found = null;

        foreach (var h in all)
        {
            if (h == null || !h.IsAlive || h.transform == transform) continue;
            if (((1 << h.gameObject.layer) & enemyMask.value) == 0) continue;

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