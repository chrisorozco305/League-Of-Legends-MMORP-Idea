using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Plays a champion's walk cycle while the NavMeshAgent is actually moving.
///
/// Reads agent.velocity rather than ChampionController.IsMoving, because
/// IsMoving is true the instant an order is issued - including through the
/// agent's acceleration ramp and while it brakes - so the feet would slide.
/// Velocity is what the model is genuinely doing.
///
/// The Wizard currently ships with a walk clip and nothing else. With no idle
/// clip to blend to, standing still parks the walk on its first frame rather
/// than playing anything. Fill in idleState once an idle animation exists and
/// this cross-fades properly instead.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ChampionAnimator : MonoBehaviour
{
    [Header("States")]
    [SerializeField] string walkState = "Walk";
    [Tooltip("Leave empty if the model has no idle clip - the walk freezes on frame 0 instead.")]
    [SerializeField] string idleState = "";

    [Header("Tuning")]
    [Tooltip("Speed below which the champion counts as standing still.")]
    [SerializeField] float moveThreshold = 0.1f;
    [Tooltip("Scale playback to the agent's speed so the stride matches the ground.")]
    [SerializeField] bool matchSpeedToVelocity = true;
    [Tooltip("Agent speed the walk clip was authored for.")]
    [SerializeField] float referenceSpeed = 3.5f;
    [SerializeField] float crossFade = 0.12f;

    Animator animator;
    NavMeshAgent agent;
    bool moving;
    bool initialised;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (animator == null) return;

        bool nowMoving = agent.velocity.sqrMagnitude > moveThreshold * moveThreshold;

        if (nowMoving != moving || !initialised)
        {
            moving = nowMoving;
            initialised = true;

            if (moving) StartWalking();
            else StopWalking();
        }

        if (moving && matchSpeedToVelocity)
        {
            float ratio = agent.velocity.magnitude / Mathf.Max(0.01f, referenceSpeed);
            animator.speed = Mathf.Clamp(ratio, 0.4f, 2f);
        }
    }

    void StartWalking()
    {
        animator.speed = 1f;
        animator.CrossFade(walkState, crossFade, 0);
    }

    void StopWalking()
    {
        if (!string.IsNullOrEmpty(idleState))
        {
            animator.speed = 1f;
            animator.CrossFade(idleState, crossFade, 0);
            return;
        }

        // No idle clip: park on the walk's first frame. Update(0) forces the
        // animator to evaluate that pose before speed 0 freezes it - without
        // it the model just stops wherever the stride happened to be.
        animator.Play(walkState, 0, 0f);
        animator.Update(0f);
        animator.speed = 0f;
    }
}
