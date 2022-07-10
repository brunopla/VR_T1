using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bspawn : MonoBehaviour
{
    public float speed;
    public float lifeTime;
    public float ammo;
    public GameManager gm;
    public GameObject bulletOrigin;
    public GameObject bullet_Uzi;

    public AudioClip GunShotClip;
    public AudioSource source;
  

    public GameObject muzzlePrefab;
    public GameObject muzzlePosition;

    private void Start()
    {
        gm = FindObjectOfType<GameManager>();
        source = GetComponent<AudioSource>();
    }

    public void Shoot()
    {
     

        if(isEscopeta && ammo > 0)
        {
            
            GameObject bullet_Instance = Instantiate(bullet_Uzi, bulletOrigin.transform);
            Instantiate(bulletOrigin.transform);
            bullet_Instance.transform.parent = null;
            ammo--;
            source.PlayOneShot(GunShotClip);
            StartCoroutine(BulletDestroy(bullet_Instance, lifeTime));
            var flash = Instantiate(muzzlePrefab, muzzlePosition.transform);

        }
      
    }

    public IEnumerator BulletDestroy(GameObject g, float t)
    {
        yield return new WaitForSeconds(t);
        Destroy(g);
    }

    public bool isEscopeta = false;


}
