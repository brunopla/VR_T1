using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 8f;
    public float lifeDuration = 2f;
    float lifeTimer;
    public int attack = 10;

    public bool shooByPlayer;

    public void OnEnable()
    {
        lifeTimer = lifeDuration;
    }

    // Update is called once per frame
    void Update()
    {
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0)
        {
            Destroy(gameObject);
        }

    }
    private void FixedUpdate()
    {
        //transform.localPosition += transform.forward * speed * Time.fixedDeltaTime;
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Bullet choca = " + other.name);

        IDamage damage = other.GetComponent<IDamage>();
        bool remove = true;
        if (damage != null)
        {
            remove = damage.DoDamage(attack, shooByPlayer);
        }
        if (remove == true) gameObject.SetActive(false);
    }

}

