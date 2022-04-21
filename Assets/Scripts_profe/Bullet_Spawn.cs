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
    public GameObject Enemy;
    

    #endregion


   


    public void Shoot()
    {
        GameObject bullet_Instance = Instantiate(bullet_Uzi, bulletOrigin.transform);

        bullet_Instance.transform.parent = null;

        StartCoroutine(BulletDestroy(bullet_Instance, lifeTime));

    }

    public IEnumerator BulletDestroy(GameObject g, float t)
    {
        yield return new WaitForSeconds(t);
        Destroy(g);
        

    }
}
