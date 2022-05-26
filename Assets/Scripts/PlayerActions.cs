using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerActions : MonoBehaviour, IDamage
{
  
    AudioSource audioDisparo;
 
 
    public int health = 20;
    public int ammo = 10;
 //   public GameObject damageEffect;
    public float saveInterval = 0.5f;
    float saveTime;
    WaitForSeconds wait;

    void Start()
    {
      //  damageEffect.SetActive(false);
        saveTime = 0.0f;
        CanvasController.instance.AddTextHp(health);
        CanvasController.instance.AddTextAmmo(ammo);
        wait = new WaitForSeconds(0.2f);
        audioDisparo = GetComponent<AudioSource>();

    }
    void Update()
    {
        

    }
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            health--;

            if (health <= 0)
            {
                
            }
        }


    }
    public bool DoDamage(int vld, bool isPlayer)
    {
        Debug.Log("HE RECIBIDO DA�O = " + vld + " isPlayer = " + isPlayer);
        if (isPlayer == true) return false;
        else
        {
            if (saveTime <= 0)
            {
                health -=vld;
                CanvasController.instance.AddTextHp(health);
                StartCoroutine(Effect());
            }
                
        }

        return true;
    }
    IEnumerator Effect()
    {
        saveTime = saveInterval;
     
        //damageEffect.SetActive(true);
        yield return wait;
       // damageEffect.SetActive(false);
        if (health <= 0)
        {
           // GameManager.instance.FinGame(false);
        }

    }
    void OnTriggerEnter(Collider other)
    {
        IBox box = other.GetComponent<IBox>();
        if (box != null)
        {
            int res = box.OpenBox();
            if (box.getID() == (int)BoxID.HEALTH)
            {
                health += res;
                CanvasController.instance.AddTextHp(health);
            }
            else if (box.getID() == (int)BoxID.AMMO)
            {
                ammo += res;
                CanvasController.instance.AddTextAmmo(ammo);
            }
            Destroy(other.gameObject);
        }
    }
}
