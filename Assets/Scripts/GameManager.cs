using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;



public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public Bspawn SelectGun;

    public float timer;

    public int totalKills;
    public int health;
    public int ammo;


    public Text txtTimer;
    public Text txtTotalEnemiesKilled;
    public Text txtHealth;
    public Text txtAmmo;

    public GameObject locomotion;

    bool timeUp;



    void Start()
    {
        instance = this;
        totalKills = 0;
        SetText(txtTotalEnemiesKilled, "Kills: " + totalKills.ToString());
        SetText(txtTimer, "TIME: " + timer.ToString("n0"));
        SetText(txtHealth, "HP: " + health.ToString());
        RefreshAmmo();
    }

    public void AddEnemyKill()
    {
        totalKills++;
        SetText(txtTotalEnemiesKilled, "Kills: " + totalKills.ToString());
    }
    public void hp()
    {
        health--;
        SetText(txtHealth, "HP: " + health.ToString());

    }

    public void TakeAmmo()
    {
        ammo = 150;
        RefreshAmmo();
    }

    // Municion de la Uzi (la que no es Bspawn). Devuelve false si no queda.
    public bool UseAmmo()
    {
        if (ammo <= 0)
            return false;

        ammo--;
        RefreshAmmo();
        return true;
    }

    // Muestra la municion del arma agarrada, o la de la Uzi si no hay una.
    public void RefreshAmmo()
    {
        float value = SelectGun != null ? SelectGun.ammo : ammo;
        SetText(txtAmmo, "Ammo: " + value.ToString("n0"));
    }


    public void Selectgun(Bspawn selectGun)
    {
        SelectGun = selectGun;
        RefreshAmmo();
    }
    public void DeselectGun()
    {
        SelectGun = null;
        RefreshAmmo();
    }

    private void Update()
    {
        if (timeUp)
            return;

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            timer = 0;
            timeUp = true;
            if (locomotion != null)
                locomotion.SetActive(false);
        }
        SetText(txtTimer, "TIME: " + timer.ToString("n0"));

    }

    void SetText(Text txt, string value)
    {
        if (txt != null)
            txt.text = value;
    }


}
