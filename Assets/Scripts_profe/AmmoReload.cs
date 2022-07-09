using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmmoReload : MonoBehaviour
{
    public GameManager gm;
    public int ammoToReload;
    public void Awake()
    {
        gm = FindObjectOfType<GameManager>();
    }

    public void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            gm.SelectGun.ammo += ammoToReload;
            Destroy(gameObject);
        }

    }
}
