using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet_Spawn : MonoBehaviour
{
    #region

    public float speed;
    public float lifeTime;
    public float rafaga;
    public int balasPorRafaga = 15;
    public float muzzleLifeTime = 1f;

    private bool playerIsShooting;

    public GameObject bulletOrigin;
    public GameObject bullet_Uzi;
    public GameManager gm;
    public GameObject muzzlePrefab;
    public GameObject muzzlePosition;

    public AudioClip uzi;
    AudioSource shoot;




    #endregion


    private void Start()
    {
        gm = GameManager.instance != null ? GameManager.instance : FindFirstObjectByType<GameManager>();

        shoot = GetComponent<AudioSource>();
    }

    public void Shoot()
    {
        TryShoot();
    }

    bool TryShoot()
    {
        if (!gm.UseAmmo())
            return false;

        GameObject bullet_Instance = Instantiate(bullet_Uzi, bulletOrigin.transform);

        bullet_Instance.transform.parent = null;

        StartCoroutine(BulletDestroy(bullet_Instance, lifeTime));
        return true;
    }

    #region
    public IEnumerator RafagaBullets()
    {
        var wait = new WaitForSeconds(rafaga / 18);

        for (int i = 0; i < balasPorRafaga; i++)
        {
            yield return wait;

            if (!TryShoot())
                break;
        }

        yield return wait;
        playerIsShooting = false;
    }
    #endregion

    public void EmpezarRafaga()
    {
        if (gm.ammo <= 0)
            return;

        if(playerIsShooting == false)
        {
            playerIsShooting = true;

            StartCoroutine(RafagaBullets());
            shoot.PlayOneShot(uzi);

        }
        var flash = Instantiate(muzzlePrefab, muzzlePosition.transform);
        Destroy(flash, muzzleLifeTime);

    }

    public IEnumerator BulletDestroy(GameObject g, float t)
    {
        yield return new WaitForSeconds(t);
        Destroy(g);
    }


}
