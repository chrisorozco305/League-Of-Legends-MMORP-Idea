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
    float burnDuration;
    float burnDamage;
    float burnTickInterval;

    public static Projectile Spawn(Vector3 origin, Transform target, float speed, float damage,
                                   Color color, float size = 0.22f, GameObject visualPrefab = null,
                                   float burnDuration = 0f, float burnDamage = 0f,
                                   float burnTickInterval = 0.5f)
    {
        GameObject go;

        if (visualPrefab != null)
        {
            // Empty root carries the flight logic and aims down the travel
            // direction; the visual rides underneath so it can spin freely
            // without fighting that aim.
            go = new GameObject("Projectile");
            go.transform.position = origin;

            var visual = Instantiate(visualPrefab, go.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // visual only - damage is applied directly, not through physics
            foreach (var c in visual.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * size;

            Destroy(go.GetComponent<Collider>());

            var rend = go.GetComponent<Renderer>();
            rend.material.color = color;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        var p = go.AddComponent<Projectile>();
        p.target = target;
        p.speed = speed;
        p.damage = damage;
        p.burnDuration = burnDuration;
        p.burnDamage = burnDamage;
        p.burnTickInterval = burnTickInterval;
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
                if (h)
                {
                    // burn first: the impact hit may kill, and Apply() bails on
                    // a dead target rather than setting a corpse alight
                    if (burnDuration > 0f)
                        BurnEffect.Apply(h, burnDuration, burnDamage, burnTickInterval);

                    h.TakeDamage(damage);
                }
            }
            Destroy(gameObject);
            return;
        }

        Vector3 dir = delta.normalized;

        // Point down the flight path so a mesh visual reads as travelling
        // nose-first, and its local Z spin becomes roll instead of tumble.
        transform.rotation = Quaternion.LookRotation(dir);
        transform.position += dir * step;
    }
}
