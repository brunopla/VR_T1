using UnityEngine;

// Punto de aparicion de zombies. Antes (GenerateEnemies) cada punto creaba
// hasta 50 zombies en coordenadas al azar; ahora WaveDirector usa la posicion
// y el prefab de cada punto para lanzar oleadas con un limite de vivos.
public class EnemySpawnPoint : MonoBehaviour
{
    public GameObject theEnemy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => EnsureDirector();
        EnsureDirector();
    }

    static void EnsureDirector()
    {
        if (FindFirstObjectByType<EnemySpawnPoint>() != null && FindFirstObjectByType<WaveDirector>() == null)
            new GameObject("WaveDirector").AddComponent<WaveDirector>();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.6f);
    }
}
