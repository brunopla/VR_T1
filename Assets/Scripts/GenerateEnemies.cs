using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenerateEnemies : MonoBehaviour
{
    public GameObject theEnemy;
    public int xPos;
    public int zPos;
    public int enemyCount;

  
    void Start()
    {
        StartCoroutine(EnemyDrop());
    }
    IEnumerator EnemyDrop()
    {
        while (enemyCount <30)
        {
            xPos = Random.Range(1, 50);
            zPos = Random.Range(1, 31);
            Instantiate(theEnemy, new Vector3(xPos, 0, zPos), Quaternion.identity);
            yield return new WaitForSeconds(7f);
            enemyCount += 1;
        }
    }

  
}
