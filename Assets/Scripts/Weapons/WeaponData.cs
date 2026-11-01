using UnityEngine;

// Configuracion de un tipo de arma. Se crea con Create > VR_T1 > Weapon Data.
[CreateAssetMenu(menuName = "VR_T1/Weapon Data", fileName = "WeaponData")]
public class WeaponData : ScriptableObject
{
    public string displayName = "Arma";

    [Header("Dano")]
    public float damage = 1f;
    [Tooltip("Proyectiles por disparo (escopeta > 1)")]
    public int pellets = 1;
    [Tooltip("Apertura del disparo en grados")]
    public float spreadAngle = 0f;
    public float range = 100f;

    [Header("Cadencia")]
    [Tooltip("Sigue disparando mientras se mantiene el gatillo")]
    public bool automatic;
    [Tooltip("Disparos por segundo")]
    public float fireRate = 5f;

    [Header("Municion")]
    public int magazineSize = 12;

    [Header("Sonido")]
    public AudioClip shotClip;
    public AudioClip emptyClip;
    public AudioClip reloadClip;

    [Header("Vibracion")]
    [Range(0, 1)] public float hapticAmplitude = 0.5f;
    public float hapticDuration = 0.08f;
}
