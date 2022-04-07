using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shoot_Bullet : MonoBehaviour
{
    public GameObject bullet;
    
    public void Shoot()
    {
        Instantiate(bullet, transform.position, Quaternion.identity);
    }

}
