using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ammo : MonoBehaviour
{
    public float ammo;
    private GameManager gm; 



    void Start()
    {
        gm = FindObjectOfType<GameManager>();
    }

  

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ammo"))
        {
            gm.hp();
        }


    }




}
