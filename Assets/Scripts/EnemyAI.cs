using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Aggro -> chase -> melee attack loop for the monster pack prefabs.
///
/// Drives the Animator by state name via CrossFade rather than parameters,
/// because both bundled controllers (Slime, TurtleShell) ship with
/// m_AnimatorParameters empty - there are no triggers or bools to set. They do
/// share identical state names, so one script covers both with no animator
/// wiring in the Inspector.
///
/// Damage lands on a windup timer rather than an animation event, so the FBX
/// import settings never have to be touched. Attack01 runs 0.83s and is
/// authored as looping, which is why an attack always explicitly returns to
/// idle instead of being left to finish on its own.
///
/// NavMeshAgent is optional. With one the enemy paths properly; without, it
/// walks straight at the target, which is fine on a flat prototype floor.
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] float aggroRange = 12f;
    [SerializeField] float attackRange = 2.4f;
    [Tooltip("Distance from the spawn point at which the enemy gives up and walks home.")]
    [SerializeField] float leashRange = 22f;
    [Tooltip("Tag identifying the champion to hunt.")]
    [SerializeField] string playerTag = "Player";

    [Header("Attack")]
    [SerializeField] float damage = 8f;
    [SerializeField] float attacksPerSecond = 0.7f;
    [Tooltip("Seconds into the swing before damage lands. Attack01 runs 0.83s.")]
    [SerializeField] float windup = 0.42f;
    [Tooltip("How long to hold the attack animation before returning to idle.")]
    [SerializeField] float attackDuration = 0.83f;
    [Tooltip("Grace multiplier on attackRange when damage lands, so a target stepping away mid-swing still takes the hit.")]
    [SerializeField] float damageRangeSlack = 1.35f;

    [Header("Movement (speed is used directly only without a NavMeshAgent)")]
    [SerializeField] float moveSpeed = 3.2f;
    [SerializeField] float turnSpeed = 10f;

    [Header("Animator State Names")]
    [SerializeField] string idleState = "IdleBattle";
    [SerializeField] string moveState = "RunFWD";
    [SerializeField] string attackState = "Attack01";
    [SerializeField] string hitState = "GetHit";
    [SerializeField] string dieState = "Die";
    [SerializeField] float crossFade = 0.1f;

    Health self;
    Health target;
    Animator animator;
    NavMeshAgent agent;

    Vector3 home;
    string currentState;
    float cooldown;
    float attackTimer;
    bool damageApplied;
    float retargetTimer;
    bool dead;

    void Awake()
    {
        self = GetComponent<Health>();
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        home = transform.position;

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRange * 0.85f;
        }
    }

    void OnEnable()
    {
        if (self == null) return;
        self.OnDamaged += OnDamaged;
        self.OnDeath += OnDied;
    }

    void OnDisable()
    {
        if (self == null) return;
        self.OnDamaged -= OnDamaged;
        self.OnDeath -= OnDied;
    }

    void Update()
    {
        if (dead) return;

        cooldown -= Time.deltaTime;

        if (TickAttack()) return;

        AcquireTarget();

        if (target == null) { GoHome(); return; }

        float dist = Vector3.Distance(transform.position, target.transform.position);

        // leash off the spawn point, not off the target, so a kited enemy
        // eventually disengages instead of being dragged across the map
        if (Vector3.Distance(home, transform.position) > leashRange)
        {
            target = null;
            GoHome();
            return;
        }

        if (dist <= attackRange)
        {
            StopMoving();
            FaceTarget();

            if (cooldown <= 0f) BeginAttack();
            else Play(idleState);
            return;
        }

        if (dist <= aggroRange)
        {
            Chase();
            return;
        }

        GoHome();
    }

    // ---------- attack ----------

    /// <summary>Returns true while an attack animation owns the enemy.</summary>
    bool TickAttack()
    {
        if (attackTimer <= 0f) return false;

        attackTimer -= Time.deltaTime;

        StopMoving();
        FaceTarget();

        float elapsed = attackDuration - attackTimer;
        if (!damageApplied && elapsed >= windup)
        {
            damageApplied = true;
            LandHit();
        }

        if (attackTimer <= 0f) Play(idleState);
        return true;
    }

    void BeginAttack()
    {
        attackTimer = attackDuration;
        damageApplied = false;
        cooldown = 1f / Mathf.Max(0.01f, attacksPerSecond);
        Play(attackState, force: true);
    }

    void LandHit()
    {
        if (target == null || !target.IsAlive) return;

        // re-check range at the moment of impact - the swing is committed, but
        // a target that ran clear shouldn't still take the hit
        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist > attackRange * damageRangeSlack) return;

        target.TakeDamage(damage);
    }

    // ---------- movement ----------

    void Chase()
    {
        Play(moveState);
        FaceTarget();

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(target.transform.position);
            return;
        }

        Vector3 dir = target.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        transform.position += dir.normalized * moveSpeed * Time.deltaTime;
    }

    void GoHome()
    {
        float dist = Vector3.Distance(transform.position, home);

        if (dist < 0.3f)
        {
            StopMoving();
            Play(idleState);
            return;
        }

        Play(moveState);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(home);
            return;
        }

        Vector3 dir = home - transform.position;
        dir.y = 0f;
        transform.position += dir.normalized * moveSpeed * Time.deltaTime;
        FaceDirection(dir);
    }

    void StopMoving()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        agent.isStopped = true;
        agent.ResetPath();
    }

    void FaceTarget()
    {
        if (target == null) return;
        FaceDirection(target.transform.position - transform.position);
    }

    void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
    }

    // ---------- targeting ----------

    void AcquireTarget()
    {
        // Also drop the target when it stops being the tagged player - swapping
        // champions moves the tag, and without this check the enemy would keep
        // hunting the body you just walked away from.
        if (target != null && target.IsAlive && target.CompareTag(playerTag)) return;

        // the champion may not exist yet, or may have died - retry on an
        // interval rather than searching every frame
        retargetTimer -= Time.deltaTime;
        if (retargetTimer > 0f) return;
        retargetTimer = 0.5f;

        // by tag, not by ChampionCombat - a champion that can't fight back is
        // still a valid thing to chew on
        var go = GameObject.FindGameObjectWithTag(playerTag);
        target = go != null ? go.GetComponent<Health>() : null;
    }

    // ---------- animation ----------

    void OnDamaged(Health h)
    {
        // don't let a flinch cancel a committed swing
        if (dead || attackTimer > 0f) return;
        Play(hitState, force: true);
    }

    /// <summary>
    /// Health fires this before it destroys anything, which is the whole point
    /// of its destroyDelay - it buys us the length of the Die clip to play out.
    /// </summary>
    void OnDied(Health h)
    {
        if (dead) return;
        dead = true;

        attackTimer = 0f;
        StopMoving();

        // stop the agent steering the corpse around while it collapses
        if (agent != null && agent.enabled) agent.enabled = false;

        Play(dieState, force: true);
    }

    void Play(string state, bool force = false)
    {
        if (animator == null || string.IsNullOrEmpty(state)) return;
        if (!force && currentState == state) return;

        currentState = state;
        animator.CrossFade(state, crossFade, 0);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
