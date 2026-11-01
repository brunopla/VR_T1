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
            // Sin arma agarrada, la municion va a la reserva de la Uzi
            if (gm.SelectGun != null)
                gm.SelectGun.ammo += ammoToReload;
            else
                gm.ammo += ammoToReload;

            gm.RefreshAmmo();
            Destroy(gameObject);
        }

    }
}
