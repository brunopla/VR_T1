using System.Collections.Generic;
using UnityEngine;

// Barrio de la ciudad ampliada. Se apaga cuando el jugador esta lejos para
// que el Quest solo dibuje lo cercano (la niebla tapa el corte).
public class CityDistrict : MonoBehaviour
{
    [Tooltip("Zona que ocupa el barrio (la calcula el generador)")]
    public Bounds area;

    static readonly List<CityDistrict> all = new List<CityDistrict>();
    public static IReadOnlyList<CityDistrict> All => all;

    // Se registra en Awake y no en OnEnable: el barrio sigue en la lista apagado
    void Awake() => all.Add(this);
    void OnDestroy() => all.Remove(this);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => EnsureStreamer();
        EnsureStreamer();
    }

    static void EnsureStreamer()
    {
        if (all.Count > 0 && FindFirstObjectByType<CityStreamer>() == null)
            new GameObject("CityStreamer").AddComponent<CityStreamer>();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireCube(area.center, area.size);
    }
}
