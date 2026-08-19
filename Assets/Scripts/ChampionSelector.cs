using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Decides which champion the player is currently driving.
///
/// Put this on GameManager alongside CursorLock / ClickIndicator / CursorManager
/// / HealthBarManager / ChampionHud.
///
///   1..9   select that champion
///   Tab    cycle to the next one
///
/// Two champions in a scene both run their own ChampionController, so without
/// an arbiter a single right-click issues a move order to both at once. This is
/// that arbiter: exactly one champion keeps its control scripts enabled and
/// carries the Player tag, and everything that asks "who is the player?" - the
/// HUD, the enemies, the camera - follows that tag.
/// </summary>
public class ChampionSelector : MonoBehaviour
{
    [Header("Roster (auto-filled from the scene if left empty)")]
    [SerializeField] List<ChampionController> champions = new List<ChampionController>();
    [SerializeField] int startIndex = 0;

    [Header("Input")]
    [SerializeField] Key cycleKey = Key.Tab;
    [SerializeField] bool numberKeysSelect = true;

    [Header("Wiring (auto-found if left empty)")]
    [SerializeField] MobaCamera cam;
    [SerializeField] ChampionHud hud;

    [Header("Tags")]
    [SerializeField] string playerTag = "Player";
    [Tooltip("Tag applied to champions that are not currently controlled.")]
    [SerializeField] string benchedTag = "Untagged";

    static readonly Key[] DigitKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
        Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    int activeIndex = -1;

    public ChampionController Active =>
        activeIndex >= 0 && activeIndex < champions.Count ? champions[activeIndex] : null;

    public int Count => champions.Count;

    void Start()
    {
        EnsureRoster();
        if (!cam) cam = FindFirstObjectByType<MobaCamera>();
        if (!hud) hud = FindFirstObjectByType<ChampionHud>();

        if (champions.Count == 0)
        {
            Debug.LogWarning("[ChampionSelector] No ChampionController in the scene - nothing to control.");
            return;
        }

        Select(Mathf.Clamp(startIndex, 0, champions.Count - 1));
    }

    void Update()
    {
        if (Keyboard.current == null || champions.Count == 0) return;

        if (Keyboard.current[cycleKey].wasPressedThisFrame)
        {
            SelectNext();
            return;
        }

        if (!numberKeysSelect) return;

        int max = Mathf.Min(champions.Count, DigitKeys.Length);
        for (int i = 0; i < max; i++)
        {
            if (Keyboard.current[DigitKeys[i]].wasPressedThisFrame)
            {
                Select(i);
                return;
            }
        }
    }

    // ---------- selection ----------

    public void SelectNext()
    {
        if (champions.Count == 0) return;
        Select((activeIndex + 1) % champions.Count);
    }

    public void Select(ChampionController champion) => Select(champions.IndexOf(champion));

    public void Select(int index)
    {
        if (index < 0 || index >= champions.Count) return;
        if (index == activeIndex) return;

        activeIndex = index;

        for (int i = 0; i < champions.Count; i++)
        {
            var c = champions[i];
            if (c == null) continue;
            ApplyControl(c, i == activeIndex);
        }

        var active = Active;
        if (active == null) return;

        // camera hands over to the new body; snap rather than glide so the
        // switch reads as instant instead of the map sliding across
        if (cam != null)
        {
            cam.target = active.transform;
            cam.SetLocked(true);
            cam.SnapToTarget();
        }

        // the HUD binds by tag, and the tag only just moved - make it look again
        if (hud != null) hud.Rebind();
    }

    void ApplyControl(ChampionController champion, bool controlled)
    {
        var go = champion.gameObject;

        // The tag is the single source of truth for "who is the player".
        // HUD binding, health bar colour, and enemy targeting all read it.
        go.tag = controlled ? playerTag : benchedTag;

        var combat = champion.GetComponent<ChampionCombat>();
        if (combat != null)
        {
            // drop any live attack order and the range ring before benching,
            // otherwise the ring hangs on a champion nobody is driving
            if (!controlled) combat.CancelOrders();
            combat.enabled = controlled;
        }

        if (!controlled)
        {
            // stop mid-order so a benched champion doesn't keep walking to
            // wherever it was last sent
            champion.Stop();

            var agent = champion.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) agent.ResetPath();
        }

        champion.enabled = controlled;
    }

    // ---------- roster ----------

    /// <summary>
    /// Guarantees every champion in the scene is on the roster.
    ///
    /// This runs even when the list was filled by hand, because the roster is
    /// also the bench list: a champion that isn't in it never gets its
    /// controller disabled, and you end up driving two bodies with one click.
    /// Partially filling the list in the Inspector is the obvious thing to do,
    /// so it has to be the safe thing to do.
    /// </summary>
    void EnsureRoster()
    {
        bool authored = champions.Count > 0;

        // Include inactive: benched champions have their controller disabled,
        // and a plain search would quietly drop them from the roster.
        var found = FindObjectsByType<ChampionController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var c in found)
            if (c != null && !champions.Contains(c)) champions.Add(c);

        champions.RemoveAll(c => c == null);

        // Only impose an order when we built the list ourselves - a hand-filled
        // roster keeps whatever order was chosen, so 1/2 stay where expected.
        if (!authored)
            champions.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
    }
}
