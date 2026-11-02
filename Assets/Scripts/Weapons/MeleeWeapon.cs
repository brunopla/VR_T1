using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Dano por golpe para objetos que se agarran y se lanzan (cuchillo, martillo...).
// Funciona en la mano y en el aire: mide la velocidad de la punta y golpea lo
// que toque si va lo bastante rapido. Los cuchillos lanzados se clavan.
[RequireComponent(typeof(Rigidbody))]
public class MeleeWeapon : MonoBehaviour
{
    static readonly Collider[] overlaps = new Collider[16];

    public float damage = 3f;
    [Tooltip("Velocidad minima de la punta (m/s) para que el golpe cuente")]
    public float minSpeed = 2.5f;
    [Tooltip("A esta velocidad el golpe hace todo el dano")]
    public float fullDamageSpeed = 8f;
    [Tooltip("Zona de golpe; vacio = el centro del objeto")]
    public Transform tip;
    public float hitRadius = 0.1f;
    public float hitCooldown = 0.4f;
    public LayerMask hitMask = ~((1 << 2) | (1 << 7) | (1 << 8));

    [Header("Clavarse al lanzarlo")]
    public bool stickOnThrow;
    public float stickSpeed = 6f;

    [Header("Sonido")]
    public AudioClip hitClip;

    Rigidbody body;
    XRGrabInteractable grab;
    IXRSelectInteractor holder;
    Vector3 lastTipPosition;
    float speed;
    bool stuck;
    bool restoreDynamicOnRelease;
    readonly Dictionary<IDamageable, float> lastHitTime = new Dictionary<IDamageable, float>();

    public bool IsHeld => holder != null;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        if (tip == null)
            tip = transform;
        lastTipPosition = tip.position;
    }

    void OnEnable()
    {
        if (grab == null)
            return;
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    void OnDisable()
    {
        if (grab == null)
            return;
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
            return;
        holder = args.interactorObject;
        // XRGrabInteractable guarda si el cuerpo era cinematico al agarrarlo;
        // si estaba clavado hay que devolverle la fisica al soltarlo
        if (stuck)
        {
            transform.SetParent(null, true);
            stuck = false;
            restoreDynamicOnRelease = true;
        }
    }

    void OnReleased(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
            return;
        holder = null;
        if (restoreDynamicOnRelease)
        {
            body.isKinematic = false;
            restoreDynamicOnRelease = false;
        }
    }

    void FixedUpdate()
    {
        Vector3 position = tip.position;
        speed = (position - lastTipPosition).magnitude / Time.fixedDeltaTime;
        lastTipPosition = position;

        if (stuck || speed < minSpeed)
            return;

        int count = Physics.OverlapSphereNonAlloc(position, hitRadius, overlaps, hitMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
            TryHit(overlaps[i], position);
    }

    void TryHit(Collider other, Vector3 point)
    {
        if (other.transform.IsChildOf(transform) || PlayerHealth.IsPlayer(other))
            return;

        var hitbox = other.GetComponent<Hitbox>();
        var target = other.GetComponentInParent<IDamageable>();
        if (target == null)
        {
            // Cuchillo lanzado contra una pared o el suelo
            if (!IsHeld && stickOnThrow && speed >= stickSpeed && !other.isTrigger && other.attachedRigidbody == null)
                Stick(other.transform);
            return;
        }

        if (lastHitTime.TryGetValue(target, out float last) && Time.time - last < hitCooldown)
            return;
        lastHitTime[target] = Time.time;
        TrainingEvents.Raise("golpe");
        if (target is TrainingTarget trainingTarget)
            TrainingEvents.Raise("golpe:" + trainingTarget.group);

        float amount = damage * Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(minSpeed, fullDamageSpeed, speed));
        if (hitbox != null)
            hitbox.Hit(amount, point);
        else
            target.TakeHit(amount, point, false);

        if (hitClip != null)
            AudioSource.PlayClipAtPoint(hitClip, point, 0.8f);
        else
            ProceduralSfx.Play(ProceduralSfx.Kind.Hit, point, 0.6f);

        if (holder is XRBaseInputInteractor input)
            input.SendHapticImpulse(Mathf.Lerp(0.3f, 0.9f, amount / Mathf.Max(damage, 0.01f)), 0.08f);

        if (!IsHeld && stickOnThrow && speed >= stickSpeed && target is ZombieController)
            Stick(other.transform);
    }

    void Stick(Transform parent)
    {
        stuck = true;
        body.isKinematic = true;
        transform.SetParent(parent, true);
    }

    // Lo llama el zombie al morir para que el cuchillo no desaparezca con el
    public void Unstick()
    {
        if (!stuck)
            return;
        stuck = false;
        transform.SetParent(null, true);
        body.isKinematic = false;
    }
}
