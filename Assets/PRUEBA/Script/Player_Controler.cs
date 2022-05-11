using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Controler : MonoBehaviour
{
    // Start is called before the first frame update
    public float speed;
    private Rigidbody playerRb;
    private Vector3 movement;


    void Start()
    {
        playerRb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        movement = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
    }

    private void FixedUpdate()
    {
        MoverJugador(movement);
    }

    void MoverJugador(Vector3 direction)
    {
        playerRb.MovePosition(transform.position + direction * speed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(direction);
    }

}
