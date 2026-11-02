using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Barril que explota al recibir dano o un golpe fuerte. Dana y empuja todo en
// el radio (con caida por distancia) y hace explotar a otros barriles en cadena.
public class ExplosiveBarrel : MonoBehaviour, IDamageable
{
    static readonly Collider[] overlaps = new Collider[64];

    public float health = 2f;
    public float radius = 5f;
    [Tooltip("Dano a los enemigos en el centro de la explosion")]
    public float maxDamage = 10f;
    [Tooltip("Dano maximo al jugador si esta dentro del radio")]
    public int maxPlayerDamage = 30;
    public float force = 12f;
    [Tooltip("Golpe (m/s) que lo hace explotar, por ejemplo al caer o recibir un martillazo")]
    public float impactToExplode = 9f;
    public float chainDelay = 0.15f;
    public GameObject explosionEffect;
    public float effectScale = 1.5f;

    bool exploded;

    public void TakeHit(float damage, Vector3 point, bool headshot)
    {
        if (exploded)
            return;
        health -= damage;
        if (health <= 0f)
            Explode(0f);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude >= impactToExplode)
            Explode(0f);
    }

    public void Explode(float delay)
    {
        if (exploded)
            return;
        exploded = true;
        StartCoroutine(ExplodeAfter(delay));
    }

    IEnumerator ExplodeAfter(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Vector3 center = transform.position;
        if (explosionEffect != null)
        {
            var effect = Instantiate(explosionEffect, center, Quaternion.identity);
            effect.transform.localScale *= effectScale;
            Destroy(effect, 4f);
        }
        ProceduralSfx.Play(ProceduralSfx.Kind.Explosion, center, 1f);
        TrainingEvents.Raise("explosion");

        var damaged = new HashSet<IDamageable>();
        var pushed = new HashSet<Rigidbody>();
        bool playerHit = false;
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlaps, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var other = overlaps[i];
            if (other == null || other.transform.IsChildOf(transform))
                continue;

            float falloff = 1f - Mathf.Clamp01(Vector3.Distance(center, other.bounds.ClosestPoint(center)) / radius);

            if (PlayerHealth.IsPlayer(other))
            {
                // Al jugador no se le empuja: lanzarlo por el aire marea en VR
                if (!playerHit && GameManager.instance != null)
                    GameManager.instance.TakeDamage(Mathf.CeilToInt(maxPlayerDamage * falloff));
                playerHit = true;
                continue;
            }

            var target = other.GetComponentInParent<IDamageable>();
            if (target != null && damaged.Add(target))
            {
                if (target is ExplosiveBarrel barrel)
                    barrel.Explode(chainDelay);
                else
                    target.TakeHit(maxDamage * falloff, other.bounds.ClosestPoint(center), false);
            }

            var rb = other.attachedRigidbody;
            if (rb != null && !rb.isKinematic && pushed.Add(rb))
                rb.AddExplosionForce(force, center, radius, 0.5f, ForceMode.Impulse);
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
