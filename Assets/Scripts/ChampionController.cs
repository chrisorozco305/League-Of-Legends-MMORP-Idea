using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// LoL-style click-to-move. Right click to order a move.
/// Requires a NavMeshAgent and a baked NavMeshSurface in the scene.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ChampionController : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] ClickIndicator indicator;   // optional, for the ring
    [SerializeField] bool holdToRepath = true;   // holding RMB keeps re-issuing the order

    NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!cam) cam = Camera.main;
        if (!indicator) indicator = FindFirstObjectByType<ClickIndicator>();
    }

    void Update()
    {
        if (Mouse.current == null) return;

        bool pressed = Mouse.current.rightButton.wasPressedThisFrame;
        bool held = holdToRepath && Mouse.current.rightButton.isPressed;

        if (!pressed && !held) return;

        if (!TryGetGroundPoint(Mouse.current.position.ReadValue(), out Vector3 dest))
            return;

        // snap the order to the nearest valid navmesh position
        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);

            // only flash the ring on the initial click, not every frame while held
            if (pressed && indicator) indicator.Play(hit.position);
        }
    }

    bool TryGetGroundPoint(Vector2 screenPos, out Vector3 point)
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
    public void Stop() => agent.ResetPath();
}