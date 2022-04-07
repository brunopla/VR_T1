using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AddForceBullet : MonoBehaviour
{
    public Rigidbody rigid;
    public float speed;
    // Start is called before the first frame update
    void Start()
    {
        rigid = transform.GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        rigid.velocity = transform.forward * speed;
    }
}
