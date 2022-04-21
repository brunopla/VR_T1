using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletToDoDamage : MonoBehaviour
{
    public void OnCollisionEnter(Collision otherObj)
    {
      if(!
            otherObj.gameObject.CompareTag("Gun"))
        {
            Destroy(gameObject);
        }

    }
}
