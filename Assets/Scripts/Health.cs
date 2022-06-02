using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Health : MonoBehaviour
{
    public float health;
    private GameManager gma;

    private void Start()
    {
        gma = FindObjectOfType<GameManager>().GetComponent<GameManager>();
    }



    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            health--;
            gma.TakeDamage();

            Debug.Log(health);
           
            if (health <= 0)
            {
              
                SceneManager.LoadScene(0);

            }
        }


    }


}
