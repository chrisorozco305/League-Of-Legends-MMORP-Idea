using UnityEngine;

/// <summary>
/// Homing bullet. Spawned procedurally - no prefab required.
/// Tracks its target; if the target dies mid-flight it continues to the
/// last known position and expires without dealing damage.
/// </summary>
public class Projectile : MonoBehaviour
{
    Transform target;
    Vector3 aimPoint;
    float speed;
    float damage;
    float life = 5f;

    public static Projectile Spawn(Vector3 origin, Transform target, float speed, float damage, Color color, float size = 0.22f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Projectile";
        go.transform.position = origin;
        go.transform.localScale = Vector3.one * size;

        // visual only - damage is applied directly, not through physics
        Destroy(go.GetComponent<Collider>());

        var rend = go.GetComponent<Renderer>();
        rend.material.color = color;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var p = go.AddComponent<Projectile>();
        p.target = target;
        p.speed = speed;
        p.damage = damage;
        p.aimPoint = target ? AimAt(target) : origin;
        return p;
    }

    static Vector3 AimAt(Transform t) => t.position + Vector3.up * 1f;   // roughly center mass

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        if (target) aimPoint = AimAt(target);

        Vector3 delta = aimPoint - transform.position;
        float step = speed * Time.deltaTime;

        if (delta.sqrMagnitude <= step * step)
        {
            if (target)
            {
                var h = target.GetComponent<Health>();
                if (h) h.TakeDamage(damage);
            }
            Destroy(gameObject);
            return;
        }

        transform.position += delta.normalized * step;
    }
}
