using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Port : MonoBehaviour
{
    public void OnCollisionEnter(Collision otherObj)
    {
        if (!
              otherObj.gameObject.CompareTag("Player"))
        {
            SceneManager.LoadScene(1);
        }

    }
}
