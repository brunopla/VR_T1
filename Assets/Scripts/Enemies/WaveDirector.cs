using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;

// Lanza oleadas cada vez mas grandes desde los EnemySpawnPoint de la escena,
// con un tope de zombies vivos para no ahogar al Quest. Los zombies se
// reutilizan con un pool en vez de crearlos y destruirlos.
public class WaveDirector : MonoBehaviour
{
    public float firstWaveDelay = 5f;
    public int firstWaveSize = 6;
    public int extraPerWave = 3;
    public int maxAlive = 12;
    public float spawnInterval = 1.5f;
    public float timeBetweenWaves = 10f;
    [Tooltip("No aparecen zombies mas cerca que esto del jugador")]
    public float minDistanceToPlayer = 8f;
    [Tooltip("Ni mas lejos que esto (en la ciudad ampliada tardarian demasiado en llegar)")]
    public float maxDistanceToPlayer = 60f;

    readonly Dictionary<GameObject, ObjectPool<ZombieController>> pools = new Dictionary<GameObject, ObjectPool<ZombieController>>();
    readonly List<EnemySpawnPoint> points = new List<EnemySpawnPoint>();
    Transform player;
    int alive;

    bool Playing => GameManager.instance == null || GameManager.instance.CurrentState == GameManager.State.Playing;

    IEnumerator Start()
    {
        foreach (var point in FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None))
            if (point.theEnemy != null && point.theEnemy.GetComponent<ZombieController>() != null)
                points.Add(point);

        if (points.Count == 0)
            yield break;

        var playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;

        yield return new WaitForSeconds(firstWaveDelay);

        for (int wave = 1; Playing; wave++)
        {
            if (GameManager.instance != null)
                GameManager.instance.SetWave(wave);

            int toSpawn = firstWaveSize + (wave - 1) * extraPerWave;
            while (toSpawn > 0 && Playing)
            {
                if (alive < maxAlive && Spawn())
                    toSpawn--;
                yield return new WaitForSeconds(spawnInterval);
            }

            while (alive > 0 && Playing)
                yield return null;

            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    bool Spawn()
    {
        var point = PickPoint();
        if (!NavMesh.SamplePosition(point.transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            return false;

        var pool = GetPool(point.theEnemy);
        var zombie = pool.Get();
        zombie.transform.SetPositionAndRotation(hit.position, Quaternion.identity);
        zombie.gameObject.SetActive(true);
        var agent = zombie.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.Warp(hit.position);

        alive++;
        return true;
    }

    readonly List<EnemySpawnPoint> candidates = new List<EnemySpawnPoint>();

    // Un punto al azar en el anillo [min, max] alrededor del jugador; si no hay,
    // el mas cercano que este fuera de la distancia minima
    EnemySpawnPoint PickPoint()
    {
        if (player == null)
            return points[Random.Range(0, points.Count)];

        candidates.Clear();
        EnemySpawnPoint nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (var point in points)
        {
            float distance = Vector3.Distance(point.transform.position, player.position);
            if (distance < minDistanceToPlayer)
                continue;
            if (distance <= maxDistanceToPlayer)
                candidates.Add(point);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = point;
            }
        }

        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];
        return nearest != null ? nearest : points[Random.Range(0, points.Count)];
    }

    ObjectPool<ZombieController> GetPool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out var pool))
            return pool;

        pool = new ObjectPool<ZombieController>(
            () =>
            {
                var go = Instantiate(prefab, transform);
                go.SetActive(false);
                var zombie = go.GetComponent<ZombieController>();
                zombie.Died = OnZombieDied;
                return zombie;
            },
            null,
            zombie => zombie.gameObject.SetActive(false),
            zombie => Destroy(zombie.gameObject),
            collectionCheck: false, defaultCapacity: maxAlive, maxSize: maxAlive * 2);
        pools[prefab] = pool;
        return pool;

        void OnZombieDied(ZombieController zombie)
        {
            alive--;
            pool.Release(zombie);
        }
    }
}
