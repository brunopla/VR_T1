using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet_Spawn : MonoBehaviour
{
    #region

    public float speed;
    public float lifeTime;
    public GameObject bulletOrigin;
    public GameObject bullet_Uzi;
    public GameObject particle;
    public GameManager gm;
 



    #endregion


    private void Start()
    {
        gm = FindObjectOfType<GameManager>();
    }


    public void Shoot()
    {
        GameObject bullet_Instance = Instantiate(bullet_Uzi, bulletOrigin.transform);
        Instantiate(particle, bulletOrigin.transform);
        bullet_Instance.transform.parent = null;
        gm.ammo--;

        StartCoroutine(BulletDestroy(bullet_Instance, lifeTime));

    }


    public IEnumerator BulletDestroy(GameObject g, float t)
    {
        yield return new WaitForSeconds(t);
        Destroy(g);
        

    }

   
}
