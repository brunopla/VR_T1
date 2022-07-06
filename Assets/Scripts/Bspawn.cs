using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bspawn : MonoBehaviour
{


    public float speed;
    public float lifeTime;
 //   public GameManager gm;
    public GameObject bulletOrigin;
    public GameObject bullet_Uzi;
    private void Start()
    {
        //gm = FindObjectOfType<GameManager>();

    }

    public void Shoot()
    {
        GameObject bullet_Instance = Instantiate(bullet_Uzi, bulletOrigin.transform);
        Instantiate(bulletOrigin.transform);
        bullet_Instance.transform.parent = null;
       // gm.ammo--;



        StartCoroutine(BulletDestroy(bullet_Instance, lifeTime));
    }

    public IEnumerator BulletDestroy(GameObject g, float t)
    {
        yield return new WaitForSeconds(t);
        Destroy(g);
    }


}
