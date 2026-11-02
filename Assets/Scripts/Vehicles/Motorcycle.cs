using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

// Moto arcade para VR. Agarra un puno del manubrio para subirte.
//   Con las dos manos en el manubrio se gira moviendolo; con una, stick izquierdo.
//   Gatillo derecho: acelerar. Gatillo izquierdo: frenar y marcha atras.
//   A / X: bajarse (hay que ir despacio).
// Solo se inclina la moto, no el jugador, para no marear.
[RequireComponent(typeof(Rigidbody))]
public class Motorcycle : MonoBehaviour
{
    [Header("Manejo")]
    public float maxSpeed = 18f;
    public float reverseSpeed = 4f;
    public float acceleration = 6f;
    public float brakeDeceleration = 14f;
    public float coastDeceleration = 2f;
    [Tooltip("Giro del manubrio que cuenta como giro completo")]
    public float maxSteerAngle = 35f;
    [Tooltip("Grados por segundo a velocidad, con el manubrio a tope")]
    public float turnRate = 70f;
    public float leanAngle = 25f;
    [Tooltip("Agarre lateral: mas alto = derrapa menos")]
    public float grip = 8f;

    [Header("Atropellar")]
    public float runOverSpeed = 4f;
    [Tooltip("Dano por cada m/s de velocidad")]
    public float runOverDamage = 1.5f;

    [Header("Piezas")]
    [Tooltip("Parte visual que se inclina en las curvas")]
    public Transform visual;
    [Tooltip("Manubrio y rueda delantera, giran con la direccion")]
    public Transform steeringPivot;
    public XRSimpleInteractable leftGrip;
    public XRSimpleInteractable rightGrip;
    [Tooltip("Donde queda la cabeza del jugador al subirse")]
    public Transform riderHead;
    public Transform[] wheels;
    public float wheelRadius = 0.33f;
    public AudioSource engineAudio;

    public bool HasRider => rider != null;
    public float Speed => speed;

    Rigidbody body;
    XROrigin rider;
    Rigidbody riderBody;
    bool riderWasKinematic;
    readonly List<Behaviour> disabledWhileRiding = new List<Behaviour>();
    readonly List<Collider> riderColliders = new List<Collider>();
    Transform leftHand;
    Transform rightHand;
    float speed;
    float steer;
    bool grounded;
    float wheelSpin;

    InputAction throttleAction;
    InputAction brakeAction;
    InputAction steerAction;
    InputAction dismountAction;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        throttleAction = new InputAction("Throttle", InputActionType.Value);
        throttleAction.AddBinding("<XRController>{RightHand}/{Trigger}");
        throttleAction.AddBinding("<Keyboard>/w");
        brakeAction = new InputAction("Brake", InputActionType.Value);
        brakeAction.AddBinding("<XRController>{LeftHand}/{Trigger}");
        brakeAction.AddBinding("<Keyboard>/s");
        steerAction = new InputAction("Steer", InputActionType.Value);
        steerAction.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}");
        steerAction.AddCompositeBinding("2DVector")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow");
        dismountAction = new InputAction("Dismount", InputActionType.Button);
        dismountAction.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
        dismountAction.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
        dismountAction.AddBinding("<Keyboard>/e");

        if (engineAudio != null)
        {
            engineAudio.clip = ProceduralSfx.Get(ProceduralSfx.Kind.Engine);
            engineAudio.loop = true;
            engineAudio.spatialBlend = 1f;
            engineAudio.volume = 0f;
            engineAudio.Play();
        }
    }

    void OnEnable()
    {
        throttleAction.Enable();
        brakeAction.Enable();
        steerAction.Enable();
        dismountAction.Enable();
        Subscribe(leftGrip, true);
        Subscribe(rightGrip, true);
    }

    void OnDisable()
    {
        throttleAction.Disable();
        brakeAction.Disable();
        steerAction.Disable();
        dismountAction.Disable();
        Subscribe(leftGrip, false);
        Subscribe(rightGrip, false);
        if (HasRider)
            Dismount(true);
    }

    void OnDestroy()
    {
        throttleAction.Dispose();
        brakeAction.Dispose();
        steerAction.Dispose();
        dismountAction.Dispose();
    }

    void Subscribe(XRSimpleInteractable handle, bool add)
    {
        if (handle == null)
            return;
        if (add)
        {
            handle.selectEntered.AddListener(OnGripGrabbed);
            handle.selectExited.AddListener(OnGripReleased);
        }
        else
        {
            handle.selectEntered.RemoveListener(OnGripGrabbed);
            handle.selectExited.RemoveListener(OnGripReleased);
        }
    }

    void OnGripGrabbed(SelectEnterEventArgs args)
    {
        var hand = args.interactorObject.transform;
        if ((Object)args.interactableObject == leftGrip)
            leftHand = hand;
        else
            rightHand = hand;

        if (!HasRider)
            Mount();
    }

    void OnGripReleased(SelectExitEventArgs args)
    {
        if ((Object)args.interactableObject == leftGrip)
            leftHand = null;
        else
            rightHand = null;
    }

    void Mount()
    {
        rider = FindFirstObjectByType<XROrigin>();
        if (rider == null)
            return;

        riderBody = rider.GetComponent<Rigidbody>();
        if (riderBody != null)
        {
            riderWasKinematic = riderBody.isKinematic;
            riderBody.isKinematic = true;
        }

        // Solo los colliders del cuerpo: los de las manos detectan los punos
        riderColliders.Clear();
        foreach (var c in rider.GetComponents<Collider>())
        {
            if (c.enabled && !c.isTrigger)
            {
                c.enabled = false;
                riderColliders.Add(c);
            }
        }

        disabledWhileRiding.Clear();
        foreach (var provider in rider.GetComponentsInChildren<LocomotionProvider>())
        {
            if (provider.enabled)
            {
                provider.enabled = false;
                disabledWhileRiding.Add(provider);
            }
        }

        // Mirar hacia adelante de la moto y poner la cabeza en el asiento
        var cameraForward = Vector3.ProjectOnPlane(rider.Camera.transform.forward, Vector3.up);
        rider.RotateAroundCameraUsingOriginUp(Vector3.SignedAngle(cameraForward, transform.forward, Vector3.up));
        rider.MoveCameraToWorldLocation(riderHead.position);
        rider.transform.SetParent(transform, true);

        VRHud.Toast("Gatillo der.: acelerar · Gatillo izq.: frenar · A/X: bajarse", 4f);
    }

    void Dismount(bool force = false)
    {
        if (!HasRider)
            return;
        if (!force && Mathf.Abs(speed) > 3f)
        {
            VRHud.Toast("Frena para bajarte", 1.5f);
            return;
        }

        var origin = rider;
        origin.transform.SetParent(null, true);
        origin.transform.rotation = Quaternion.Euler(0f, origin.transform.eulerAngles.y, 0f);
        origin.transform.position = FindDismountSpot();

        foreach (var c in riderColliders)
            if (c != null)
                c.enabled = true;
        foreach (var provider in disabledWhileRiding)
            if (provider != null)
                provider.enabled = true;
        if (riderBody != null)
        {
            riderBody.isKinematic = riderWasKinematic;
            if (!riderBody.isKinematic)
                riderBody.linearVelocity = Vector3.zero;
        }

        rider = null;
        riderBody = null;
        leftHand = null;
        rightHand = null;
    }

    // Al costado de la moto, en el suelo y sin paredes
    Vector3 FindDismountSpot()
    {
        foreach (var side in new[] { transform.right, -transform.right, -transform.forward })
        {
            Vector3 candidate = transform.position + side * 1.1f;
            if (Physics.CheckCapsule(candidate + Vector3.up * 0.4f, candidate + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                continue;
            if (Physics.Raycast(candidate + Vector3.up, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;
        }
        return transform.position + Vector3.up * 1.2f;
    }

    void Update()
    {
        if (HasRider && dismountAction.WasPressedThisFrame())
            Dismount();

        steer = HasRider ? ReadSteer() : Mathf.MoveTowards(steer, 0f, Time.deltaTime * 2f);

        float speedFactor = Mathf.Clamp01(Mathf.Abs(speed) / 8f);
        if (visual != null)
            visual.localRotation = Quaternion.Euler(0f, 0f, -steer * leanAngle * speedFactor);
        if (steeringPivot != null)
            steeringPivot.localRotation = Quaternion.Euler(0f, steer * maxSteerAngle, 0f);

        wheelSpin += speed / Mathf.Max(0.05f, wheelRadius) * Mathf.Rad2Deg * Time.deltaTime;
        if (wheels != null)
            foreach (var wheel in wheels)
                if (wheel != null)
                    wheel.localRotation = Quaternion.Euler(wheelSpin, 0f, 90f);

        if (engineAudio != null)
        {
            engineAudio.volume = Mathf.MoveTowards(engineAudio.volume, HasRider ? 0.35f : 0f, Time.deltaTime);
            engineAudio.pitch = 0.7f + Mathf.Abs(speed) / maxSpeed * 1.6f;
        }
    }

    // Manubrio real si las dos manos lo sostienen; si no, el stick
    float ReadSteer()
    {
        if (leftHand != null && rightHand != null)
        {
            Vector3 across = Vector3.ProjectOnPlane(rightHand.position - leftHand.position, transform.up);
            float angle = Vector3.SignedAngle(transform.right, across, transform.up);
            return Mathf.Clamp(angle / maxSteerAngle, -1f, 1f);
        }
        return Mathf.MoveTowards(steer, steerAction.ReadValue<Vector2>().x, Time.deltaTime * 4f);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        grounded = IsGrounded();

        float throttle = HasRider ? throttleAction.ReadValue<float>() : 0f;
        float brake = HasRider ? brakeAction.ReadValue<float>() : 0f;

        if (grounded)
        {
            if (throttle > 0.05f)
                speed = Mathf.MoveTowards(speed, maxSpeed * throttle, acceleration * dt);
            else if (brake > 0.05f)
                speed = speed > 0.1f
                    ? Mathf.MoveTowards(speed, 0f, brakeDeceleration * brake * dt)
                    : Mathf.MoveTowards(speed, -reverseSpeed * brake, acceleration * 0.5f * dt);
            else
                speed = Mathf.MoveTowards(speed, 0f, coastDeceleration * dt);
        }

        // Si algo nos freno (un choque), la velocidad real manda
        Vector3 velocity = body.linearVelocity;
        float measured = Vector3.Dot(velocity, transform.forward);
        if (Mathf.Abs(measured) < Mathf.Abs(speed) - 3f)
            speed = measured;

        float yawRate = steer * turnRate * Mathf.Clamp01(Mathf.Abs(speed) / 4f) * Mathf.Sign(speed);
        body.MoveRotation(body.rotation * Quaternion.Euler(0f, yawRate * dt, 0f));

        if (grounded)
        {
            Vector3 desired = transform.forward * speed;
            Vector3 horizontal = Vector3.Lerp(new Vector3(velocity.x, 0f, velocity.z), desired, Mathf.Clamp01(grip * dt));
            body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }
    }

    static readonly RaycastHit[] groundHits = new RaycastHit[8];

    // Ignora los colliders de la moto y del jugador montado (que es hijo de la moto)
    bool IsGrounded()
    {
        int count = Physics.RaycastNonAlloc(transform.position + transform.up * 0.5f, -transform.up, groundHits, 0.9f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!groundHits[i].collider.transform.IsChildOf(transform))
                return true;
        return false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (Mathf.Abs(speed) < runOverSpeed)
            return;

        var target = collision.collider.GetComponentInParent<IDamageable>();
        if (target == null)
            return;

        target.TakeHit(Mathf.Abs(speed) * runOverDamage, collision.GetContact(0).point, false);
        if (target is ZombieController)
        {
            speed *= 0.85f;
            ProceduralSfx.Play(ProceduralSfx.Kind.Thud, collision.GetContact(0).point, 0.8f);
        }
    }
}
