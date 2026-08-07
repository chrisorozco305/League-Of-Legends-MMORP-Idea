using System;
using UnityEngine;

/// <summary>Minimal health. Put this on anything that can be shot.</summary>
public class Health : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;

    public float Max => maxHealth;
    public float Current { get; private set; }
    public bool IsAlive => Current > 0f;

    public event Action<Health> OnDamaged;
    public event Action<Health> OnDeath;

    void Awake() => Current = maxHealth;

    public void TakeDamage(float amount)
    {
        if (!IsAlive) return;

        Current = Mathf.Max(0f, Current - amount);
        OnDamaged?.Invoke(this);

        if (Current <= 0f)
        {
            OnDeath?.Invoke(this);
            Destroy(gameObject);
        }
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        Current = Mathf.Min(maxHealth, Current + amount);
    }
}