using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;




public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public float timer;

    public int totalKills;
    public int health;
    public int ammo;
   

    public Text txtTimer;
    public Text txtTotalEnemiesKilled;
    public Text txtHealth;
    public Text txtAmmo;

    public GameObject locomotion;
    public GameObject enemyContainer;
   
   

    
    void Start()
    {
        instance = this;
        totalKills = enemyContainer.GetComponentsInChildren<eNEMYcONTROLLER2>().Length;
        txtTotalEnemiesKilled.text = "Total Enemies: " + totalKills.ToString();        
        txtTimer.text = "TIME: " + timer.ToString("n2");
        txtHealth.text = "HP: " + health.ToString();
        txtAmmo.text = "Ammo: " + ammo.ToString();


    }

    public void AddEnemyKill()
    {
        totalKills++;        
        txtTotalEnemiesKilled.text = "Total Enemies: " + totalKills.ToString();
        if (totalKills <= 0)
        {
           
        }
    }
    public void hp()
    {
        health--;
        txtHealth.text = "Health: " + health.ToString();
     
    }
    
    public void TakeAmmo()
    {
        ammo = 150;
        txtAmmo.text = "Ammo: " + ammo.ToString();
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        txtTimer.text = "TIME: " + timer.ToString("n0");
        if (timer < 0)
        {
            locomotion.SetActive(false);                    

        }      

    }
    

}
