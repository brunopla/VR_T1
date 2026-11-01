using UnityEngine;

// Vida del jugador. El valor vive en GameManager.health (una sola fuente);
// los zombies hacen dano al golpear, no al chocar.
public class PlayerHealth : MonoBehaviour
{
    private GameManager gma;

    // El collider puede estar en un hijo del XR Origin (que es el que tiene el tag)
    public static bool IsPlayer(Collider c)
    {
        return c.CompareTag("Player") || (c.attachedRigidbody != null && c.attachedRigidbody.CompareTag("Player"));
    }

    private void Start()
    {
        gma = GameManager.instance != null ? GameManager.instance : FindFirstObjectByType<GameManager>();
    }

    public void TakeDamage(int amount)
    {
        if (gma != null)
            gma.TakeDamage(amount);
    }
}
