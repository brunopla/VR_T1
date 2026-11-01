using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;



public class GameManager : MonoBehaviour
{
    public enum State { Playing, Won, Lost }

    public static GameManager instance;

    // Armas antiguas (Bspawn) que aun llaman a Selectgun desde UnityEvents
    public Bspawn SelectGun;
    public Weapon SelectedWeapon;

    public float timer;

    public int totalKills;
    public int health;
    // Reserva de municion compartida por todas las armas
    public int ammo;


    public Text txtTimer;
    public Text txtTotalEnemiesKilled;
    public Text txtHealth;
    public Text txtAmmo;

    public GameObject locomotion;

    [Tooltip("Escena a la que se vuelve al ganar (0 = tutorial)")]
    public int sceneAfterWin = 0;

    public State CurrentState { get; private set; }
    public int Wave { get; private set; }

    bool timeUp;


    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        totalKills = 0;
        RefreshKills();
        SetText(txtTimer, "TIME: " + timer.ToString("n0"));
        RefreshHealth();
        RefreshAmmo();
    }

    // En el tutorial el GameManager esta desactivado: ahi no hay vida ni derrota
    bool Active => isActiveAndEnabled && CurrentState == State.Playing;

    public void AddEnemyKill()
    {
        totalKills++;
        RefreshKills();
    }

    public void SetWave(int wave)
    {
        Wave = wave;
        RefreshKills();
        VRHud.Toast("Oleada " + wave);
    }

    public void hp()
    {
        TakeDamage(1);
    }

    public void TakeDamage(int amount)
    {
        if (!Active)
            return;

        health = Mathf.Max(0, health - amount);
        RefreshHealth();
        VRHud.DamageFlash();

        if (health <= 0)
            Lose("Te alcanzaron los zombies");
    }

    public void TakeAmmo()
    {
        ammo = 150;
        RefreshAmmo();
    }

    public void AddReserve(int amount)
    {
        ammo += amount;
        RefreshAmmo();
    }

    // Saca hasta 'requested' balas de la reserva y devuelve cuantas habia
    public int TakeReserve(int requested)
    {
        int taken = Mathf.Clamp(requested, 0, ammo);
        ammo -= taken;
        RefreshAmmo();
        return taken;
    }

    // Municion de la Uzi antigua (Bullet_Spawn). Devuelve false si no queda.
    public bool UseAmmo()
    {
        if (ammo <= 0)
            return false;

        ammo--;
        RefreshAmmo();
        return true;
    }

    public void RefreshAmmo()
    {
        if (SelectedWeapon != null)
            SetText(txtAmmo, "Ammo: " + SelectedWeapon.Ammo + " / " + ammo);
        else if (SelectGun != null)
            SetText(txtAmmo, "Ammo: " + SelectGun.ammo.ToString("n0"));
        else
            SetText(txtAmmo, "Ammo: " + ammo);
    }

    public void SelectWeapon(Weapon weapon)
    {
        SelectedWeapon = weapon;
        RefreshAmmo();
    }

    public void DeselectWeapon(Weapon weapon)
    {
        if (SelectedWeapon == weapon)
            SelectedWeapon = null;
        RefreshAmmo();
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

    public void Win()
    {
        if (!Active)
            return;

        CurrentState = State.Won;
        StopPlayer();
        VRHud.ShowEndScreen("¡Escapaste!",
            "Zombies eliminados: " + totalKills + "\nTiempo restante: " + timer.ToString("n0") + " s",
            () => SceneManager.LoadScene(sceneAfterWin));
    }

    public void Lose(string reason)
    {
        if (!Active)
            return;

        CurrentState = State.Lost;
        StopPlayer();
        VRHud.ShowEndScreen("Fin del juego",
            reason + "\nZombies eliminados: " + totalKills,
            () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
    }

    void StopPlayer()
    {
        if (locomotion != null)
            locomotion.SetActive(false);
    }

    private void Update()
    {
        if (timeUp || CurrentState != State.Playing)
            return;

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            timer = 0;
            timeUp = true;
            Lose("Se acabó el tiempo");
        }
        SetText(txtTimer, "TIME: " + timer.ToString("n0"));

    }

    void RefreshKills()
    {
        string wave = Wave > 0 ? "Oleada " + Wave + " · " : "";
        SetText(txtTotalEnemiesKilled, wave + "Kills: " + totalKills);
    }

    void RefreshHealth()
    {
        SetText(txtHealth, "HP: " + health);
    }

    void SetText(Text txt, string value)
    {
        if (txt != null)
            txt.text = value;
    }


}
