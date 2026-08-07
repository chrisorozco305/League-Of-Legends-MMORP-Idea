using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// LoL-style click-to-move.
///   Right click ground - move order
///   Right click enemy  - attack order
/// Requires a NavMeshAgent and a baked NavMeshSurface in the scene.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ChampionController : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] ClickIndicator indicator;   // optional, for the ring
    [SerializeField] bool holdToRepath = true;   // holding RMB keeps re-issuing the order
    [SerializeField] float targetPickRange = 500f;

    NavMeshAgent agent;
    ChampionCombat combat;
    bool attackOrderActive;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        combat = GetComponent<ChampionCombat>();
        if (!cam) cam = Camera.main;
        if (!indicator) indicator = FindFirstObjectByType<ClickIndicator>();
    }

    void Update()
    {
        if (Mouse.current == null) return;

        bool pressed = Mouse.current.rightButton.wasPressedThisFrame;
        bool held = holdToRepath && Mouse.current.rightButton.isPressed;

        if (!pressed && !held) return;

        if (pressed)
        {
            // a fresh click always re-decides between attack and move
            attackOrderActive = false;

            if (TryIssueAttackOrder())
            {
                attackOrderActive = true;
                return;
            }
        }

        // while the button stays held, don't let move-repathing stomp the attack order
        if (attackOrderActive) return;

        combat?.CancelOrders();
        IssueMoveOrder(pressed);
    }

    // ---------- orders ----------

    /// <summary>Returns true if the cursor was over a valid enemy and an attack was ordered.</summary>
    bool TryIssueAttackOrder()
    {
        if (cam == null || combat == null) return false;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, targetPickRange)) return false;

        var h = hit.collider.GetComponentInParent<Health>();
        if (h == null || !h.IsAlive || h.transform == transform) return false;

        combat.AttackTarget(h.transform);
        return true;
    }

    void IssueMoveOrder(bool showIndicator)
    {
        if (!TryGetGroundPoint(Mouse.current.position.ReadValue(), out Vector3 dest))
            return;

        // snap the order to the nearest valid navmesh position
        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);

            // only flash the ring on the initial click, not every frame while held
            if (showIndicator && indicator) indicator.Play(hit.position);
        }
    }

    // ---------- helpers ----------

    public bool TryGetGroundPoint(Vector2 screenPos, out Vector3 point)
    {
        // reuse the indicator's raycast if we have one, so both agree on the ground
        if (indicator) return indicator.TryGetGroundPoint(screenPos, out point);

        point = Vector3.zero;
        if (!cam) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float dist))
        {
            point = ray.GetPoint(dist);
            return true;
        }
        return false;
    }

    public bool IsMoving => agent.hasPath && agent.remainingDistance > agent.stoppingDistance;

    public void Stop()
    {
        agent.ResetPath();
        attackOrderActive = false;
        combat?.CancelOrders();
    }
}