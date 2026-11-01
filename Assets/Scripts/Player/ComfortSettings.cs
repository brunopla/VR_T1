using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

// Opciones de comodidad, guardadas entre partidas. Se agrega sola al XR Origin.
// Boton menu del control izquierdo (o tecla M en el editor):
//   toque corto        -> giro por pasos / continuo
//   mantener 1 s       -> modo sentado / de pie
//   mantener 3 s       -> vineta al moverse si / no
public class ComfortSettings : MonoBehaviour
{
    const string SnapTurnKey = "vrt1.snapTurn";
    const string SeatedKey = "vrt1.seated";
    const string VignetteKey = "vrt1.vignette";
    const float SeatedHold = 1f;
    const float VignetteHold = 3f;
    const float SeatedCameraHeight = 1.36f;

    public static bool SnapTurn { get; private set; }
    public static bool Seated { get; private set; }
    public static bool Vignette { get; private set; }

    XROrigin origin;
    InputAction menuAction;
    float pressedAt = -1f;
    int holdStage;
    Vector3 lastPosition;
    float lastYaw;
    float vignette;

#pragma warning disable 618 // El rig del proyecto usa los proveedores de locomocion de XRI 2.x
    ActionBasedSnapTurnProvider snapTurn;
    ActionBasedContinuousTurnProvider continuousTurn;
#pragma warning restore 618

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (scene, mode) => Attach();
        Attach();
    }

    static void Attach()
    {
        var xrOrigin = FindFirstObjectByType<XROrigin>();
        if (xrOrigin != null && xrOrigin.GetComponent<ComfortSettings>() == null)
            xrOrigin.gameObject.AddComponent<ComfortSettings>();
    }

    void Awake()
    {
        origin = GetComponent<XROrigin>();
        SnapTurn = PlayerPrefs.GetInt(SnapTurnKey, 1) == 1;
        Seated = PlayerPrefs.GetInt(SeatedKey, 0) == 1;
        Vignette = PlayerPrefs.GetInt(VignetteKey, 1) == 1;

        menuAction = new InputAction("ComfortMenu", InputActionType.Button);
        menuAction.AddBinding("<XRController>{LeftHand}/{MenuButton}");
        menuAction.AddBinding("<Keyboard>/m");
    }

    void Start()
    {
        SetupTurnProviders();
        Apply();
        VRHud.Ensure();
        lastPosition = transform.position;
        lastYaw = transform.eulerAngles.y;
    }

    void OnEnable() => menuAction.Enable();
    void OnDisable() => menuAction.Disable();
    void OnDestroy() => menuAction.Dispose();

    void Update()
    {
        HandleMenuButton();
        UpdateVignette();
    }

    void HandleMenuButton()
    {
        if (menuAction.WasPressedThisFrame())
        {
            pressedAt = Time.unscaledTime;
            holdStage = 0;
        }

        if (pressedAt < 0f)
            return;

        float held = Time.unscaledTime - pressedAt;
        // Avisar mientras se mantiene, para que se sepa que pasara al soltar
        if (holdStage == 0 && held >= SeatedHold)
        {
            holdStage = 1;
            VRHud.Toast("Suelta: " + (Seated ? "modo de pie" : "modo sentado"), 2f);
        }
        else if (holdStage == 1 && held >= VignetteHold)
        {
            holdStage = 2;
            VRHud.Toast("Suelta: viñeta " + (Vignette ? "no" : "sí"), 2f);
        }

        if (!menuAction.WasReleasedThisFrame())
            return;

        pressedAt = -1f;
        if (held >= VignetteHold)
            SetVignette(!Vignette);
        else if (held >= SeatedHold)
            SetSeated(!Seated);
        else
            SetSnapTurn(!SnapTurn);
    }

    public void SetSnapTurn(bool value)
    {
        SnapTurn = value;
        PlayerPrefs.SetInt(SnapTurnKey, value ? 1 : 0);
        Apply();
        VRHud.Toast(value ? "Giro por pasos" : "Giro continuo");
    }

    public void SetSeated(bool value)
    {
        Seated = value;
        PlayerPrefs.SetInt(SeatedKey, value ? 1 : 0);
        Apply();
        VRHud.Toast(value ? "Modo sentado" : "Modo de pie");
    }

    public void SetVignette(bool value)
    {
        Vignette = value;
        PlayerPrefs.SetInt(VignetteKey, value ? 1 : 0);
        VRHud.Toast(value ? "Viñeta al moverse: sí" : "Viñeta al moverse: no");
    }

    void Apply()
    {
        if (snapTurn != null)
            snapTurn.enabled = SnapTurn || continuousTurn == null;
        if (continuousTurn != null)
            continuousTurn.enabled = !SnapTurn || snapTurn == null;

        if (origin != null)
        {
            // Sentado: la altura la pone el juego en vez del suelo real
            origin.RequestedTrackingOriginMode = Seated ? XROrigin.TrackingOriginMode.Device : XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = SeatedCameraHeight;
        }
    }

#pragma warning disable 618
    // El rig solo trae giro por pasos: se crea el continuo con las mismas acciones
    void SetupTurnProviders()
    {
        snapTurn = GetComponentInChildren<ActionBasedSnapTurnProvider>(true);
        continuousTurn = GetComponentInChildren<ActionBasedContinuousTurnProvider>(true);
        if (snapTurn == null || continuousTurn != null)
            return;

        continuousTurn = snapTurn.gameObject.AddComponent<ActionBasedContinuousTurnProvider>();
        continuousTurn.system = snapTurn.system;
        continuousTurn.leftHandTurnAction = snapTurn.leftHandSnapTurnAction;
        continuousTurn.rightHandTurnAction = snapTurn.rightHandSnapTurnAction;
        continuousTurn.turnSpeed = 90f;
        continuousTurn.enabled = false;
    }
#pragma warning restore 618

    // La vineta reacciona al movimiento artificial (el del XR Origin), no al de la cabeza
    void UpdateVignette()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float linear = (transform.position - lastPosition).magnitude / dt;
        float angular = Mathf.Abs(Mathf.DeltaAngle(lastYaw, transform.eulerAngles.y)) / dt;
        lastPosition = transform.position;
        lastYaw = transform.eulerAngles.y;

        // El giro por pasos es instantaneo: no necesita vineta
        if (SnapTurn && angular > 360f)
            angular = 0f;

        float target = Vignette ? Mathf.Clamp01(linear / 3f + angular / 120f) * 0.75f : 0f;
        vignette = Mathf.MoveTowards(vignette, target, dt * (target > vignette ? 4f : 2f));
        VRHud.VignetteStrength = vignette;
    }
}
