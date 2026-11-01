using UnityEngine;

public interface IDamageable
{
    void TakeHit(float damage, Vector3 point, bool headshot);
}

// Zona de impacto de un enemigo (por ejemplo la cabeza) con su multiplicador.
public class Hitbox : MonoBehaviour
{
    public float damageMultiplier = 1f;
    public bool isHead;

    IDamageable owner;

    void Awake()
    {
        owner = GetComponentInParent<IDamageable>();
    }

    public void Hit(float damage, Vector3 point)
    {
        owner?.TakeHit(damage * damageMultiplier, point, isHead);
    }
}
