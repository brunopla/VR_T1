using UnityEngine;

// Enciende los barrios cercanos a la camara y apaga los lejanos.
// La distancia de apagado es mayor que la de encendido para que no parpadeen.
public class CityStreamer : MonoBehaviour
{
    // La niebla de level_00 tapa todo a partir de ~75 m
    public float showDistance = 80f;
    public float hideDistance = 100f;
    public float interval = 0.25f;

    float nextCheck;

    void Start() => Refresh();

    void Update()
    {
        if (Time.unscaledTime < nextCheck)
            return;
        nextCheck = Time.unscaledTime + interval;
        Refresh();
    }

    void Refresh()
    {
        var cam = Camera.main;
        if (cam == null)
            return;

        Vector3 position = cam.transform.position;
        foreach (var district in CityDistrict.All)
        {
            if (district == null)
                continue;
            // Distancia en el plano: la altura no importa
            var area = district.area;
            var flat = new Bounds(new Vector3(area.center.x, position.y, area.center.z), new Vector3(area.size.x, 1f, area.size.z));
            float distance = Mathf.Sqrt(flat.SqrDistance(position));
            bool active = district.gameObject.activeSelf;
            bool shouldBeActive = distance < (active ? hideDistance : showDistance);
            if (active != shouldBeActive)
                district.gameObject.SetActive(shouldBeActive);
        }
    }
}
