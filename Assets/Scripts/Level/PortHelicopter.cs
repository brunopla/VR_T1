using UnityEngine;
using UnityEngine.SceneManagement;

// Helicoptero de escape: subir a el gana la partida.
public class PortHelicopter : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        if (GameManager.instance != null && GameManager.instance.isActiveAndEnabled)
            GameManager.instance.Win();
        else
            SceneManager.LoadScene(0);
    }
}
