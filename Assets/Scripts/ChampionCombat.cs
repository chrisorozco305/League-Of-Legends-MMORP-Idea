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
    [Tooltip("Optional mesh for the bolt (e.g. Fireball). Falls back to a coloured sphere when empty.")]
    [SerializeField] GameObject projectileVisual;

    [Header("Range Ring")]
    [SerializeField] Key toggleRangeKey = Key.A;
    [SerializeField] Color rangeColor = new Color(0.62f, 0.88f, 1f, 0.95f);   // light blue

    [Header("Burn (passive - 0 duration disables)")]
    [Tooltip("Seconds the target burns after being hit.")]
    [SerializeField] float burnDuration = 0f;
    [Tooltip("Total damage dealt across the whole burn, not per tick.")]
    [SerializeField] float burnDamage = 0f;
    [Tooltip("Seconds between burn ticks.")]
    [SerializeField] float burnTickInterval = 0.5f;

    [Header("Cast Timing")]
    [Tooltip("Seconds into the attack animation before the bolt leaves. Only used as a fallback - a CastRelease animation event on the clip takes priority and is more accurate.")]
    [SerializeField] float castReleaseTime = 0.9f;
    [Tooltip("Release on the timer above if the animation event never arrives. Turn off only if you are certain every attack clip has a CastRelease event.")]
    [SerializeField] bool releaseWithoutEvent = true;

    [Header("Facing")]
    [SerializeField] float turnSpeed = 20f;

    [Header("Targeting")]
    [SerializeField] Camera cam;
    [SerializeField] float targetPickRange = 500f;

    Transform pendingTarget;
    bool shotPending;
    float releaseTimer;

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

    /// <summary>
    /// Raised the moment a shot leaves the muzzle. ChampionAnimator listens so
    /// the cast animation fires on the actual shot rather than being guessed
    /// from the cooldown, which would drift as attack speed changes.
    /// </summary>
    public event System.Action OnAttack;

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

        TickPendingShot();
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

        // Moving cancels a wind-up outright, as in League. Without this the
        // fallback timer would still release a bolt ~0.9s later from a
        // champion who visibly walked out of the cast - a shot with no
        // animation behind it.
        CancelPendingShot();

        if (armed) SetArmed(false);
    }

    /// <summary>Drops a held shot so it can never release. Deliberate, not an interrupt.</summary>
    void CancelPendingShot()
    {
        shotPending = false;
        pendingTarget = null;
        releaseTimer = 0f;
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
        CancelPendingShot();   // this is a move order too, so a held shot dies with it

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

    /// <summary>
    /// Begins the attack: starts the animation and holds the shot until the
    /// cast actually releases. The bolt is NOT spawned here - see
    /// ReleaseProjectile().
    /// </summary>
    void Fire()
    {
        // an attack starting while one is still held means attack speed has
        // outrun the cast animation - let the old shot go rather than eat it
        if (shotPending) ReleaseProjectile();

        pendingTarget = target;
        shotPending = true;
        releaseTimer = castReleaseTime;

        OnAttack?.Invoke();
    }

    /// <summary>
    /// Spawns the bolt. Called by the CastAttack animation event (via
    /// AnimationEventRelay) at the frame the staff orb discharges, so the
    /// projectile leaves exactly when the visual says it should.
    ///
    /// Public because animation events reach it from outside; safe to call
    /// twice, the pending flag makes the second call a no-op.
    /// </summary>
    public void ReleaseProjectile()
    {
        if (!shotPending) return;
        shotPending = false;

        // target may have died or been swapped during the wind-up
        if (pendingTarget == null) return;

        var h = pendingTarget.GetComponent<Health>();
        if (h == null || !h.IsAlive) return;

        Vector3 origin = muzzle
            ? muzzle.position
            : transform.position + Vector3.up * 1.1f + transform.forward * 0.4f;

        Projectile.Spawn(origin, pendingTarget, projectileSpeed, damage, projectileColor,
                         visualPrefab: projectileVisual,
                         burnDuration: burnDuration,
                         burnDamage: burnDamage,
                         burnTickInterval: burnTickInterval);
    }

    /// <summary>
    /// Backstop for the animation event. If the clip has no CastRelease event -
    /// or the event was lost in a re-import from Blender - the shot would
    /// otherwise never leave. Fires on a timer instead so combat still works.
    /// </summary>
    void TickPendingShot()
    {
        if (!shotPending || !releaseWithoutEvent) return;

        releaseTimer -= Time.deltaTime;
        if (releaseTimer <= 0f) ReleaseProjectile();
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