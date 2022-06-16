using System.Collections;
using System.Collections.Generic;
using UnityEngine;



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

    private UnityEngine.XRController xr;
 



    private void Start()
    {
        xr = (XRController) GameObject.FindObjectOfType(typeof(XRController));
        gm = FindObjectOfType<GameManager>();

        shoot = GetComponent<AudioSource>();
    }

    public void Shoot()
    {      
        if(gm.ammo != 0)
        {
        
            ActivateHaptic();
            GameObject bullet_Instance = Instantiate(bullet_Uzi, bulletOrigin.transform);
            Instantiate(particle, bulletOrigin.transform);
            bullet_Instance.transform.parent = null;
            gm.ammo--;
            StartCoroutine(BulletDestroy(bullet_Instance, lifeTime));
        }
    }

    void ActivateHaptic()
    {
        xr.SendHapticImpulse(0.7f, 2f);
    }


    public override bool SendHapticImpulse(float amplitude, float duration)
    {
        if (inputDevice.TryGetHapticCapabilities(out var capabilities) &&
            capabilities.supportsImpulse)
        {
            return inputDevice.SendHapticImpulse(0u, amplitude, duration);
        }
        return false;
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
