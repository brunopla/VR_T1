using UnityEngine;

// Cargador fisico: se agarra y se mete en el socket del arma para recargar.
public class Magazine : MonoBehaviour
{
    public int rounds = 30;
    [Tooltip("Arma compatible; vacio = cualquiera")]
    public WeaponData compatibleWith;
}
