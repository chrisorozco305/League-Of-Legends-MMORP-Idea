using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Locomotion and attack animation for a champion.
///
/// Reads agent.velocity rather than ChampionController.IsMoving, because
/// IsMoving is true the instant an order is issued - including through the
/// agent's acceleration ramp and while it brakes - so the feet would slide.
/// Velocity is what the model is genuinely doing.
///
/// Drives states by name via CrossFade rather than animator parameters, so the
/// controller needs no triggers or bools wired up. Same approach as EnemyAI.
///
/// Attacks hang off ChampionCombat.OnAttack, which fires on the actual shot.
/// Moving cancels the cast, matching League - you can walk out of your own
/// attack wind-down.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ChampionAnimator : MonoBehaviour
{
    [Header("States")]
    [SerializeField] string idleState = "Idle";
    [SerializeField] string walkState = "Walk";
    [Tooltip("Leave empty if the model has no attack clip.")]
    [SerializeField] string attackState = "CastAttack";

    [Header("Attack")]
    [Tooltip("How long to hold the cast animation before returning to locomotion. Should match the clip length.")]
    [SerializeField] float attackDuration = 1.47f;
    [Tooltip("Let movement cut the cast animation short, League-style.")]
    [SerializeField] bool movementCancelsAttack = true;

    [Header("Tuning")]
    [Tooltip("Speed below which the champion counts as standing still.")]
    [SerializeField] float moveThreshold = 0.1f;
    [Tooltip("Scale walk playback to the agent's speed so the stride matches the ground.")]
    [SerializeField] bool matchSpeedToVelocity = true;
    [Tooltip("Agent speed the walk clip was authored for.")]
    [SerializeField] float referenceSpeed = 3.5f;
    [SerializeField] float crossFade = 0.12f;

    Animator animator;
    NavMeshAgent agent;
    ChampionCombat combat;

    string currentState;
    float attackHold;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        combat = GetComponent<ChampionCombat>();
    }

    void OnEnable()
    {
        if (combat != null) combat.OnAttack += OnAttackFired;
    }

    void OnDisable()
    {
        if (combat != null) combat.OnAttack -= OnAttackFired;
    }

    void Update()
    {
        if (animator == null) return;

        bool moving = agent.velocity.sqrMagnitude > moveThreshold * moveThreshold;

        // An actual move order, not just leftover momentum. TickCombat calls
        // ResetPath() and Fire() in the same frame, but ResetPath only clears
        // the path - velocity keeps decaying for several frames afterwards
        // (more so with autoBraking off). Cancelling on velocity therefore ate
        // the animation of the first attack after walking into range, every
        // time. hasPath goes false the instant the order is dropped.
        bool ordered = agent.hasPath && agent.remainingDistance > agent.stoppingDistance;

        if (attackHold > 0f)
        {
            attackHold -= Time.deltaTime;

            // walking out of a cast is allowed, and reads far better than
            // sliding across the ground mid-animation
            if (ordered && movementCancelsAttack) attackHold = 0f;
            else if (attackHold > 0f) return;
        }

        Play(moving ? walkState : idleState);

        if (moving && matchSpeedToVelocity)
        {
            float ratio = agent.velocity.magnitude / Mathf.Max(0.01f, referenceSpeed);
            animator.speed = Mathf.Clamp(ratio, 0.4f, 2f);
        }
        else
        {
            animator.speed = 1f;
        }
    }

    void OnAttackFired()
    {
        if (string.IsNullOrEmpty(attackState) || animator == null) return;

        attackHold = attackDuration;
        animator.speed = 1f;
        Play(attackState, force: true);
    }

    void Play(string state, bool force = false)
    {
        if (string.IsNullOrEmpty(state)) return;
        if (!force && currentState == state) return;

        currentState = state;
        animator.CrossFade(state, crossFade, 0);
    }
}
