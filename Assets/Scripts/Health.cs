using System;
using UnityEngine;

/// <summary>Minimal health. Put this on anything that can be shot.</summary>
public class Health : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;

    [Header("Death")]
    [Tooltip("Seconds the corpse sticks around after dying, so a death animation " +
             "has time to play. 0 destroys at the end of the frame, as before.")]
    [SerializeField] float destroyDelay = 1.5f;
    [Tooltip("Switch colliders off on death so corpses can't be clicked, hovered, " +
             "or picked as an attack target while they play out.")]
    [SerializeField] bool disableCollidersOnDeath = true;

    bool isDying;

    public float Max => maxHealth;
    public float Current { get; private set; }

    /// <summary>
    /// False the instant health hits zero, not when the object is finally
    /// destroyed - so targeting and health bars treat a corpse as dead for the
    /// whole death animation instead of chasing it for another second.
    /// </summary>
    public bool IsAlive => !isDying && Current > 0f;

    public event Action<Health> OnDamaged;
    public event Action<Health> OnDeath;

    void Awake() => Current = maxHealth;

    public void TakeDamage(float amount)
    {
        if (!IsAlive) return;

        Current = Mathf.Max(0f, Current - amount);
        OnDamaged?.Invoke(this);

        if (Current <= 0f) Die();
    }

    void Die()
    {
        if (isDying) return;
        isDying = true;

        // fired before anything is torn down - listeners start their death
        // animation here, and they need the object intact to do it
        OnDeath?.Invoke(this);

        if (disableCollidersOnDeath)
        {
            foreach (var c in GetComponentsInChildren<Collider>())
                c.enabled = false;
        }

        Destroy(gameObject, Mathf.Max(0f, destroyDelay));
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        Current = Mathf.Min(maxHealth, Current + amount);
    }
}