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
    //public GameObject particle;
 //   public Transform originParticle;
  //  public ParticleSystem particleSystems;



    #endregion


    private void Start()
    {
        //particleSystems = GetComponent<ParticleSystem>();
    }


    public void Shoot()
    {
        GameObject bullet_Instance = Instantiate(bullet_Uzi, bulletOrigin.transform);

        bullet_Instance.transform.parent = null;

        StartCoroutine(BulletDestroy(bullet_Instance, lifeTime));

       // particleSystems.Play();
    }


    public IEnumerator BulletDestroy(GameObject g, float t)
    {
        yield return new WaitForSeconds(t);
        Destroy(g);
        

    }

    //private void OnCollisionEnter(Collision collision)
    //{
    //    if (collision.gameObject.CompareTag("Enemy"))
    //    {
    //         Instantiate(particleSystems, originParticle.position, Quaternion.identity);
    //    }
            
    //}
}
