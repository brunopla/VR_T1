using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmmoReload : MonoBehaviour
{
    public GameManager gm;
    public int ammoToReload;
    public void Awake()
    {
        gm = GameManager.instance != null ? GameManager.instance : FindFirstObjectByType<GameManager>();
    }

    public void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            // Las armas antiguas (Bspawn) guardan su propia municion;
            // el resto va a la reserva compartida
            if (gm.SelectGun != null && gm.SelectedWeapon == null)
                gm.SelectGun.ammo += ammoToReload;
            else
                gm.AddReserve(ammoToReload);

            gm.RefreshAmmo();
            VRHud.Toast("+" + ammoToReload + " de munición", 1.5f);
            Destroy(gameObject);
        }

    }
}
