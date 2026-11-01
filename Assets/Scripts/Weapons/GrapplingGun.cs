using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Gancho: cuerda de largo maximo (permite balancearse, no tira como un resorte),
// mira con asistencia, recoger/soltar cuerda con el stick de la mano que lo
// sostiene y vibracion. StartGrapple/StopGrapple los llaman los eventos del prefab.
public class GrapplingGun : MonoBehaviour
{
    #region Variables

    private LineRenderer lr;

    public float maxDistance = 100f;
    private SpringJoint joint;

    public Transform pivotStartPoint;
    public Transform gunTip, hitp, player;
    private Vector3 grapplePoint;

    public LayerMask whatIsGrappleable;


    [Tooltip("Rigidez de la cuerda (minimo 40)")]
    public float spring;
    [Tooltip("Amortiguacion de la cuerda (minimo 5)")]
    public float damper;
    public float massScale;

    #endregion

    [Header("Mejoras")]
    [Tooltip("Ademas de la capa Grapp, engancharse a cualquier superficie del escenario")]
    public bool grappleAnySurface = true;
    [Tooltip("Radio de la asistencia de punteria")]
    public float aimAssistRadius = 0.25f;
    [Tooltip("Velocidad (m/s) al recoger o soltar cuerda con el stick")]
    public float reelSpeed = 6f;
    public float minRopeLength = 1.5f;
    [Tooltip("Empujon inicial hacia el punto al engancharse")]
    public float pullImpulse = 3f;

    XRGrabInteractable grab;
    IXRSelectInteractor holder;
    Rigidbody playerBody;
    Rigidbody anchorBody;
    Vector3 anchorLocalPoint;
    Transform reticle;
    InputAction leftStick;
    InputAction rightStick;

    void Awake() {
        lr = GetComponent<LineRenderer>();
        grab = GetComponentInParent<XRGrabInteractable>();
        // Un prefab suelto no puede apuntar al XR Origin de la escena
        if (player == null)
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }
        if (player != null)
            playerBody = player.GetComponentInParent<Rigidbody>();
        if (pivotStartPoint == null)
            pivotStartPoint = gunTip != null ? gunTip : transform;

        leftStick = new InputAction("GrappleReelLeft", InputActionType.Value);
        leftStick.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}");
        rightStick = new InputAction("GrappleReelRight", InputActionType.Value);
        rightStick.AddBinding("<XRController>{RightHand}/{Primary2DAxis}");
        reticle = CreateReticle();
    }

    void OnEnable()
    {
        leftStick.Enable();
        rightStick.Enable();
        if (grab == null)
            return;
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    void OnDisable()
    {
        leftStick.Disable();
        rightStick.Disable();
        StopGrapple();
        if (grab == null)
            return;
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    void OnDestroy()
    {
        leftStick.Dispose();
        rightStick.Dispose();
        if (reticle != null)
            Destroy(reticle.gameObject);
    }

    void OnGrabbed(SelectEnterEventArgs args) => holder = args.interactorObject;

    void OnReleased(SelectExitEventArgs args)
    {
        holder = null;
        StopGrapple();
    }

    void Update()
    {
        if (IsGrappling())
        {
            // El punto sigue al objeto si se engancho a algo que se mueve
            if (anchorBody != null)
            {
                grapplePoint = anchorBody.transform.TransformPoint(anchorLocalPoint);
                joint.connectedAnchor = grapplePoint;
            }

            float reel = ReadReelInput();
            if (Mathf.Abs(reel) > 0.2f)
                joint.maxDistance = Mathf.Clamp(joint.maxDistance - reel * reelSpeed * Time.deltaTime, minRopeLength, maxDistance);

            reticle.gameObject.SetActive(false);
            return;
        }

        // Mira: solo mientras se sostiene el gancho
        RaycastHit hit = default;
        bool canAim = (grab == null || holder != null) && TryGetTarget(out hit);
        reticle.gameObject.SetActive(canAim);
        if (canAim)
        {
            reticle.position = hit.point;
            // Mismo tamano aparente a cualquier distancia
            reticle.localScale = Vector3.one * Mathf.Clamp(hit.distance * 0.02f, 0.08f, 1.5f);
            if (hitp != null)
                hitp.position = hit.point;
        }
    }

    void LateUpdate()
    {
        // GrapplingRope dibuja la cuerda con ondas; sin el, una linea recta
        if (lr != null && GetComponent<GrapplingRope>() == null)
            DrawRope();
    }

    /// <summary>
    /// Call whenever we want to start a grapple
    /// </summary>
    public void StartGrapple()
    {
        if (player == null || IsGrappling() || !TryGetTarget(out RaycastHit hit))
            return;

        grapplePoint = hit.point;
        anchorBody = hit.rigidbody;
        anchorLocalPoint = anchorBody != null ? anchorBody.transform.InverseTransformPoint(hit.point) : Vector3.zero;

        joint = player.gameObject.AddComponent<SpringJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedAnchor = grapplePoint;

        // Cuerda: sin largo minimo y con un maximo algo menor que la distancia,
        // asi cuelga y se balancea en vez de rebotar como un resorte
        float distanceFromPoint = Vector3.Distance(player.position, grapplePoint);
        joint.maxDistance = Mathf.Max(minRopeLength, distanceFromPoint * 0.9f);
        joint.minDistance = 0f;
        joint.spring = Mathf.Max(spring, 40f);
        joint.damper = Mathf.Max(damper, 5f);
        joint.massScale = massScale > 0f ? massScale : 4.5f;

        if (playerBody != null)
            playerBody.AddForce((grapplePoint - player.position).normalized * pullImpulse, ForceMode.VelocityChange);

        if (lr != null)
            lr.positionCount = 2;
        SendHaptics(0.6f, 0.1f);
        ProceduralSfx.Play(ProceduralSfx.Kind.Hit, grapplePoint, 0.5f);
    }


    /// <summary>
    /// Call whenever we want to stop a grapple
    /// </summary>
    public void StopGrapple()
    {
        if (lr != null)
            lr.positionCount = 0;
        if (joint != null)
            Destroy(joint);
        joint = null;
        anchorBody = null;
    }

    bool TryGetTarget(out RaycastHit hit)
    {
        int mask = whatIsGrappleable | (grappleAnySurface ? 1 : 0);
        var origin = pivotStartPoint.position;
        var direction = pivotStartPoint.forward;
        if (Physics.Raycast(origin, direction, out hit, maxDistance, mask, QueryTriggerInteraction.Ignore) && !PlayerHealth.IsPlayer(hit.collider))
            return true;
        // Asistencia: si el rayo fino falla, probar con uno mas grueso
        return Physics.SphereCast(origin, aimAssistRadius, direction, out hit, maxDistance, mask, QueryTriggerInteraction.Ignore)
               && !PlayerHealth.IsPlayer(hit.collider);
    }

    float ReadReelInput()
    {
        if (holder is XRBaseInteractor interactor)
        {
            if (interactor.handedness == InteractorHandedness.Left)
                return leftStick.ReadValue<Vector2>().y;
            if (interactor.handedness == InteractorHandedness.Right)
                return rightStick.ReadValue<Vector2>().y;
        }
        // Rig antiguo sin mano asignada: se adivina por el nombre
        bool left = holder != null && holder.transform.name.ToLowerInvariant().Contains("left");
        return (left ? leftStick : rightStick).ReadValue<Vector2>().y;
    }

    void SendHaptics(float amplitude, float duration)
    {
        if (holder is XRBaseInputInteractor input)
            input.SendHapticImpulse(amplitude, duration);
    }

    void DrawRope()
    {
        //If not grappling, don't draw rope
        if (!joint) return;

        lr.SetPosition(0, gunTip.position);
        lr.SetPosition(1, grapplePoint);
    }

    Transform CreateReticle()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "GrappleReticle";
        Destroy(go.GetComponent<Collider>());
        go.transform.localScale = Vector3.one * 0.15f;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.3f, 1f, 0.6f, 0.8f) };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.SetActive(false);
        return go.transform;
    }

    public bool IsGrappling() {
        return joint != null;
    }

    public Vector3 GetGrapplePoint() {
        return grapplePoint;
    }
}
