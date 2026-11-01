using UnityEngine;

// Mueve el objeto (el helicoptero) de ida y vuelta entre dos puntos.
// Antes creaba un cilindro visible como objetivo y usaba goto.
public class Waypoint : MonoBehaviour
{
    public float speed = 1.0f;
    public Vector3 pointA = new Vector3(-0.49f, 70f, -8.98f);
    public Vector3 pointB = new Vector3(-160.49f, 70f, -8.98f);

    Vector3 target;

    void Awake()
    {
        transform.position = pointA;
        target = pointB;
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude < 0.0001f)
            target = target == pointA ? pointB : pointA;
    }
}
