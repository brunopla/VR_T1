using System.Collections;
using UnityEngine;

// Blanco de practica con forma de zombie: cae hacia atras al recibir un golpe,
// avisa al entrenamiento ("blanco:grupo", y "cabeza:grupo" si fue en la cabeza)
// y se vuelve a levantar despues de un rato.
public class TrainingTarget : MonoBehaviour, IDamageable
{
    [Tooltip("Grupo del blanco: galeria, cabeza, cuchillo, martillo, lejos...")]
    public string group = "galeria";
    [Tooltip("Pieza que gira al caer (pivote en la base)")]
    public Transform board;
    public float resetTime = 4f;

    bool down;

    public void TakeHit(float damage, Vector3 point, bool headshot)
    {
        if (down)
            return;
        TrainingEvents.Raise("blanco:" + group);
        if (headshot)
            TrainingEvents.Raise("cabeza:" + group);
        ProceduralSfx.Play(ProceduralSfx.Kind.Hit, point, 0.8f);
        StartCoroutine(FallAndReset());
    }

    IEnumerator FallAndReset()
    {
        down = true;
        var pivot = board != null ? board : transform;
        Quaternion up = pivot.localRotation;
        Quaternion fallen = up * Quaternion.Euler(-85f, 0f, 0f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
        {
            pivot.localRotation = Quaternion.Slerp(up, fallen, t * t);
            yield return null;
        }
        pivot.localRotation = fallen;

        yield return new WaitForSeconds(resetTime);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
        {
            pivot.localRotation = Quaternion.Slerp(fallen, up, t);
            yield return null;
        }
        pivot.localRotation = up;
        down = false;
    }
}
