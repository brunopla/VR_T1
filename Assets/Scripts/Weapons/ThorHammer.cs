using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Martillo de Thor: se lanza, hace una onda expansiva al impactar fuerte y
// vuelve a la ultima mano que lo sostuvo con el boton B/Y (tecla H en el editor).
// Al volver golpea a los zombies que encuentra en el camino.
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class ThorHammer : MonoBehaviour
{
    static readonly Collider[] overlaps = new Collider[32];

    [Header("Regreso")]
    public float returnTime = 0.6f;
    [Tooltip("Cuanto se abre la curva de regreso hacia un lado")]
    public float curveSideOffset = 1.5f;
    public float spinSpeed = 1080f;
    public float returnHitRadius = 0.45f;
    public float returnHitDamage = 3f;

    [Header("Onda expansiva")]
    [Tooltip("Velocidad de impacto (m/s) que dispara la onda")]
    public float shockwaveSpeed = 7f;
    public float shockwaveRadius = 3f;
    public float shockwaveDamage = 4f;
    public float shockwaveForce = 8f;
    public GameObject shockwaveEffect;

    XRGrabInteractable grab;
    Rigidbody body;
    IXRSelectInteractor lastHolder;
    InputAction recallAction;
    bool returning;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        body = GetComponent<Rigidbody>();
        recallAction = new InputAction("RecallHammer", InputActionType.Button);
        recallAction.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
        recallAction.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
        recallAction.AddBinding("<Keyboard>/h");
    }

    void OnEnable()
    {
        recallAction.Enable();
        grab.selectEntered.AddListener(OnGrabbed);
    }

    void OnDisable()
    {
        recallAction.Disable();
        grab.selectEntered.RemoveListener(OnGrabbed);
    }

    void OnDestroy() => recallAction.Dispose();

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
            return;
        lastHolder = args.interactorObject;
    }

    void Update()
    {
        if (recallAction.WasPressedThisFrame() && !grab.isSelected && !returning && lastHolder != null)
            StartCoroutine(Return());
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!grab.isSelected && !returning && collision.relativeVelocity.magnitude >= shockwaveSpeed)
            Shockwave(collision.GetContact(0).point);
    }

    IEnumerator Return()
    {
        returning = true;
        body.isKinematic = true;
        var hitDuringReturn = new HashSet<IDamageable>();

        Vector3 start = transform.position;
        Quaternion startRotation = transform.rotation;
        Transform hand = lastHolder.GetAttachTransform(grab);
        Vector3 side = Vector3.Cross(Vector3.up, (hand.position - start).normalized);
        Vector3 control = (start + hand.position) * 0.5f + side * curveSideOffset + Vector3.up;

        for (float t = 0f; t < 1f; t += Time.deltaTime / returnTime)
        {
            // La mano se mueve: se recalcula el destino cada frame
            float eased = t * t * (3f - 2f * t);
            Vector3 end = hand.position;
            transform.position = Vector3.Lerp(Vector3.Lerp(start, control, eased), Vector3.Lerp(control, end, eased), eased);
            transform.rotation = startRotation * Quaternion.AngleAxis(spinSpeed * t * returnTime, Vector3.right);
            HitAlongPath(hitDuringReturn);
            yield return null;
        }

        transform.position = hand.position;
        returning = false;
        // XRGrabInteractable recuerda si era cinematico al agarrarlo: tiene que
        // volver a ser fisico antes, o al soltarlo quedaria flotando
        body.isKinematic = false;

        // Si la mano esta libre, el martillo vuelve a ella; si no, cae
        if (!lastHolder.hasSelection && grab.interactionManager != null)
        {
            grab.interactionManager.SelectEnter(lastHolder, (IXRSelectInteractable)grab);
            if (lastHolder is XRBaseInputInteractor input)
                input.SendHapticImpulse(0.8f, 0.12f);
            TrainingEvents.Raise("martillo_vuelve");
        }
    }

    void HitAlongPath(HashSet<IDamageable> alreadyHit)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, returnHitRadius, overlaps, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var other = overlaps[i];
            if (other.transform.IsChildOf(transform) || PlayerHealth.IsPlayer(other))
                continue;
            var target = other.GetComponentInParent<IDamageable>();
            if (target != null && alreadyHit.Add(target))
            {
                target.TakeHit(returnHitDamage, transform.position, false);
                ProceduralSfx.Play(ProceduralSfx.Kind.Hit, transform.position, 0.7f);
            }
        }
    }

    void Shockwave(Vector3 point)
    {
        if (shockwaveEffect != null)
            Destroy(Instantiate(shockwaveEffect, point, Quaternion.identity), 4f);
        ProceduralSfx.Play(ProceduralSfx.Kind.Thud, point, 1f);

        var damaged = new HashSet<IDamageable>();
        var pushed = new HashSet<Rigidbody>();
        int count = Physics.OverlapSphereNonAlloc(point, shockwaveRadius, overlaps, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var other = overlaps[i];
            if (other.transform.IsChildOf(transform) || PlayerHealth.IsPlayer(other))
                continue;

            float falloff = 1f - Mathf.Clamp01(Vector3.Distance(point, other.bounds.ClosestPoint(point)) / shockwaveRadius);
            var target = other.GetComponentInParent<IDamageable>();
            if (target != null && damaged.Add(target))
                target.TakeHit(shockwaveDamage * falloff, point, false);

            var rb = other.attachedRigidbody;
            if (rb != null && rb != body && !rb.isKinematic && pushed.Add(rb))
                rb.AddExplosionForce(shockwaveForce, point, shockwaveRadius, 0.5f, ForceMode.Impulse);
        }
    }
}
