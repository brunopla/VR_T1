using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{


    [SerializeField] private GameObject objectToBeSpawn;
   // [SerializeField] int numbersOfItems;
   // [SerializeField] Transform parent;



    void Start()
    {
        Instantiate(objectToBeSpawn, transform.position, Quaternion.identity);
        //for (int i = 0; i < numbersOfItems; i++)
        //{
        //    Vector3 position = new Vector3(Random.Range(0f, 10f), Random.Range(0f, 10f),
        //        Random.Range(0f, 10f));
        //    Instantiate(objectToBeSpawn, position, Quaternion.identity, parent);
        //}
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}