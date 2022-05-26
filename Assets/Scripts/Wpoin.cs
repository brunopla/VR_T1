using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wpoin : MonoBehaviour
{
    // Adjust the speed for the application.
    public float speed = 2.0f;

    // The target (cylinder) position.
    private Transform target;

    void Awake()
    {
        // Position the cube at the origin.
        transform.position = new Vector3(-3, 78f, -9f);

        // Create and position the cylinder.
        GameObject Cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);


        // Grab cylinder values and place on the target.
        target = Cylinder.transform;
        target.transform.position = new Vector3(-3f, 78f, -9f);



    }

    void Update()
    {

    //######################################################

    forward:
        // Move our position a step closer to the target.
        float step = speed * Time.deltaTime; // calculate distance to move
        transform.position = Vector3.MoveTowards(transform.position, target.position, step);

        // Check if the position of the cube and sphere are approximately equal.
        if (Vector3.Distance(transform.position, target.position) < 0.001f)
        {
            target.position = new Vector3(-180.49f, 79f, -8.98f);
            goto back;
        }

    //######################################################

    back:
        {
            // Move our position a step closer to the target.
            transform.position = Vector3.MoveTowards(transform.position, target.position, step);

            // Check if the position of the cube and sphere are approximately equal.
            if (Vector3.Distance(transform.position, target.position) < 0.001f)
            {
                target.position = new Vector3(-03f, 78f, -12f);
                goto forward;
            }
        }
    }
}