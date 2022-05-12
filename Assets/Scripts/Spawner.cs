using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{


    [SerializeField] private GameObject objectToBeSpawn;
    [SerializeField] int numbersOfItems;
    [SerializeField] Transform parent;

    




    void Start()
    {
        
        for (int i = 0; i < numbersOfItems; i++)
        {
            Vector3 position = new Vector3(0.0f, 0.0f, 0.0f);
            Instantiate(objectToBeSpawn, transform.position, Quaternion.identity);
        }

    }

    // Update is called once per frame
    void Update()
    {
      

    }
}
