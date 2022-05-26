using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;



public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public Text txtTotalEnemiesKilled;
    public int totalKills;
    public GameObject enemyContainer;

    public float timer;
    public Text txtTimer;
    public GameObject locomotion;


    // Start is called before the first frame update
    void Start()
    {
        instance = this;
        totalKills = enemyContainer.GetComponentsInChildren<EnemyController>().Length;
        txtTotalEnemiesKilled.text = "Total Enemies: " + totalKills.ToString();        
        txtTimer.text = "TIME: " + timer.ToString("n2");
    }

    public void AddEnemyKill()
    {
        totalKills++;        
        txtTotalEnemiesKilled.text = "Total Enemies: " + totalKills.ToString();
        if (totalKills <= 0)
        {
           
        }
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        txtTimer.text = "TIME: " + timer.ToString("n0");
        if (timer < 0)
        {
            //locomotion.SetActive(false);

           //SceneManager.LoadScene(1);
                    

        }

       

    }
    public void FinGame(bool isWin)
    {
        if (isWin == true)
        {
            Debug.Log("HAS GANADO");
            staticValues.winner = 1;

            if (PlayerPrefs.HasKey("record") == true)
            {
                float record = PlayerPrefs.GetFloat("record");
                if (timer < record)
                {
                    PlayerPrefs.SetFloat("record", timer);
                }
            }

            else
            {
                PlayerPrefs.SetFloat("record", timer);
            }
            PlayerPrefs.Save();

        }
        else
        {
            Debug.Log("HAS PERDIDO");
            staticValues.winner = 0;
        }


       
           
    }



}
