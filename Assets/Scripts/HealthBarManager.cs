using UnityEngine;

/// <summary>
/// Gives every Health in the scene a floating bar.
///
/// Put this on GameManager alongside CursorLock / ClickIndicator / CursorManager.
///
/// Rescans on an interval rather than requiring anything to register itself, so
/// enemies spawned at runtime pick up a bar without Health.cs having to know the
/// UI exists. At prototype unit counts the scan is cheaper than the plumbing it
/// replaces; swap to an explicit register call if the scene ever holds hundreds
/// of units.
/// </summary>
public class HealthBarManager : MonoBehaviour
{
    [Header("Bar Size")]
    [SerializeField] float barWidth = 130f;
    [SerializeField] float barHeight = 16f;

    [Header("Placement")]
    [Tooltip("Height above the unit's origin, in world units.")]
    [SerializeField] float championHeight = 2.4f;
    [SerializeField] float enemyHeight = 2.0f;

    [Header("Segments")]
    [Tooltip("A dark tick every this many points of max health. 0 disables ticks.")]
    [SerializeField] float hpPerSegment = 100f;

    [Header("Scanning")]
    [Tooltip("Seconds between sweeps for units that don't have a bar yet.")]
    [SerializeField] float rescanInterval = 0.5f;

    float timer;

    void Start() => Scan();

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = rescanInterval;
        Scan();
    }

    void Scan()
    {
        Health[] all = FindObjectsByType<Health>(FindObjectsSortMode.None);

        foreach (var h in all)
        {
            if (h == null || !h.IsAlive) continue;

            // the champion is the one that can shoot back - everything else is a foe
            bool isChampion = h.GetComponent<ChampionCombat>() != null;

            UnitHealthBar.Attach(
                h,
                isChampion ? HudStyle.HealthAlly : HudStyle.HealthFoe,
                isChampion ? championHeight : enemyHeight,
                barWidth,
                barHeight,
                hpPerSegment);
        }
    }
}
