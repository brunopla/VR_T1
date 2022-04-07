using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet_Spawn : MonoBehaviour
{

    public GameObject bullet_Uzi;
    public float speed;
    public float lifeTime;
    public GameObject bulletOrigin;
    public Rigidbody bullet;
    // Start is called before the first frame update


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
