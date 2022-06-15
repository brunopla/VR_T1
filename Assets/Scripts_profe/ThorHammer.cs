using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThorHammer : MonoBehaviour
{

    private Rigidbody rb;

    public float throwForce;
    public Transform target, curve_point;
    public bool isReturning = false;

    public bool testing = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Throw()
    {
        rb.isKinematic = false;
    }

    private void Update()
    {
        if(testing)
        {
            ReturningAxe();
        }
    }

    public void ReturningAxe()
    {

        isReturning = true;
        rb.velocity = Vector3.zero;
        
        rb.isKinematic = true;
    }
}
