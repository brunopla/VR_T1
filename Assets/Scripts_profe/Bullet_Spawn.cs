using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;



public class Bullet_Spawn : MonoBehaviour
{
    #region

    public float speed;
    public float lifeTime;
    public float rafaga;

    private bool playerIsShooting;
   
    public GameObject bulletOrigin;
    public GameObject bullet_Uzi;
    public GameObject particle;
    public GameManager gm;

    public AudioClip uzi;
    AudioSource shoot;




    #endregion

 



    private void Start()
    {
       
        gm = FindObjectOfType<GameManager>();

        shoot = GetComponent<AudioSource>();
    }

    public void Shoot()
    {      
        if(gm.ammo != 0)
        {
        
            
            GameObject bullet_Instance = Instantiate(bullet_Uzi, bulletOrigin.transform);
            Instantiate(particle, bulletOrigin.transform);
            bullet_Instance.transform.parent = null;
            gm.ammo--;
            StartCoroutine(BulletDestroy(bullet_Instance, lifeTime));
        }
    }



    #region
    public IEnumerator RafagaBullets()
    {
        

        yield return new WaitForSeconds(rafaga /18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);

        Shoot();

        yield return new WaitForSeconds(rafaga / 18);
        playerIsShooting = false;
    }
    #endregion

    public void EmpezarRafaga()
    {
        if(playerIsShooting == false)
        {
            playerIsShooting = true;

            StartCoroutine(RafagaBullets());
           
        }

        shoot.PlayOneShot(uzi);
    }

    public IEnumerator BulletDestroy(GameObject g, float t)
    {
        yield return new WaitForSeconds(t);
        Destroy(g);
    }

   
}
