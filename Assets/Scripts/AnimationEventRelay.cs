using UnityEngine;

/// <summary>
/// Forwards animation events up to the champion's gameplay scripts.
///
/// This exists because of one Unity rule: an animation event can only call a
/// method on a component sitting on the SAME GameObject as the Animator. Our
/// Animator lives on the model child while ChampionCombat lives on the champion
/// root, so the event has nowhere to land without a relay here.
///
/// The method names below are exactly what you type into the Function field of
/// an Event in the FBX's Animation tab. Renaming one silently breaks the event -
/// Unity logs a runtime warning rather than failing the import, so watch the
/// console if a cast ever stops firing.
/// </summary>
public class AnimationEventRelay : MonoBehaviour
{
    ChampionCombat combat;

    void Awake() => combat = GetComponentInParent<ChampionCombat>();

    /// <summary>
    /// Called from the CastAttack clip at the frame the staff orb discharges.
    /// Until this fires, ChampionCombat is holding the shot.
    /// </summary>
    public void CastRelease()
    {
        if (combat != null) combat.ReleaseProjectile();
    }
}
