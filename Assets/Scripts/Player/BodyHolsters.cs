using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Fundas magneticas en el cuerpo: cadera izquierda y derecha para armas cortas,
// espalda (sobre el hombro derecho) para las largas. Suelta el arma cerca y se
// engancha; agarrala desde ahi para sacarla. Cada funda solo se activa cuando la
// mano trae un arma cerca, para no "robar" armas de una mesa al pasar al lado.
public class BodyHolsters : MonoBehaviour
{
    const float ActivationDistance = 0.35f;
    const float LongItem = 0.6f;

    class Slot
    {
        public string name;
        public Vector3 offset;     // respecto a la cabeza: derecha, arriba, adelante
        public Vector3 euler;      // como cuelga el arma
        public bool forLongItems;
        public Transform root;
        public XRSocketInteractor socket;
        public Renderer indicator;
    }

    readonly List<Slot> slots = new List<Slot>
    {
        new Slot { name = "Funda cadera izquierda", offset = new Vector3(-0.22f, -0.72f, 0.06f), euler = new Vector3(90f, 0f, 0f) },
        new Slot { name = "Funda cadera derecha", offset = new Vector3(0.22f, -0.72f, 0.06f), euler = new Vector3(90f, 0f, 0f) },
        new Slot { name = "Funda espalda", offset = new Vector3(0.2f, -0.28f, -0.2f), euler = new Vector3(-70f, 0f, 0f), forLongItems = true },
    };

    Transform head;
    XRBaseInputInteractor leftHand, rightHand;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (scene, mode) => Attach();
        Attach();
    }

    static void Attach()
    {
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin != null && origin.GetComponent<BodyHolsters>() == null)
            origin.gameObject.AddComponent<BodyHolsters>();
    }

    void Start()
    {
        head = GetComponent<XROrigin>().Camera.transform;
        leftHand = PlayerRig.Hand(true);
        rightHand = PlayerRig.Hand(false);
        foreach (var slot in slots)
            Build(slot);
    }

    void Build(Slot slot)
    {
        var go = new GameObject(slot.name);
        slot.root = go.transform;

        // Un trigger que se mueve necesita un Rigidbody cinematico para detectar
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        var trigger = go.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.15f;

        slot.socket = go.AddComponent<XRSocketInteractor>();
        slot.socket.showInteractableHoverMeshes = false;
        slot.socket.recycleDelayTime = 0.6f;
        slot.socket.attachTransform = go.transform;
        slot.socket.socketActive = false;
        slot.socket.selectEntered.AddListener(OnHolstered);

        // Aro que aparece al acercar un arma
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(go.transform, false);
        ring.transform.localScale = new Vector3(0.22f, 0.003f, 0.22f);
        slot.indicator = ring.GetComponent<Renderer>();
        slot.indicator.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.3f, 1f, 0.6f, 0.45f) };
        slot.indicator.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        slot.indicator.enabled = false;
    }

    void LateUpdate()
    {
        if (head == null)
            return;
        // Las fundas siguen el giro del cuerpo (la cabeza sin inclinacion)
        var yaw = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
        foreach (var slot in slots)
        {
            if (slot.root == null)
                continue;
            slot.root.SetPositionAndRotation(head.position + yaw * slot.offset, yaw * Quaternion.Euler(slot.euler));

            bool handNear = HandBringsFittingItem(slot, leftHand) || HandBringsFittingItem(slot, rightHand);
            slot.socket.socketActive = slot.socket.hasSelection || handNear;
            slot.indicator.enabled = handNear && !slot.socket.hasSelection;
        }
    }

    bool HandBringsFittingItem(Slot slot, XRBaseInputInteractor hand)
    {
        if (hand == null || !hand.hasSelection)
            return false;
        var grab = hand.interactablesSelected[0] as XRGrabInteractable;
        if (grab == null || PlayerRig.EquipmentName(grab) == null)
            return false;
        // La espalda acepta cualquier arma; la cadera, solo las cortas
        if (PlayerRig.Length(grab) > LongItem && !slot.forLongItems)
            return false;
        return Vector3.Distance(grab.transform.position, slot.root.position) < ActivationDistance;
    }

    void OnHolstered(SelectEnterEventArgs args)
    {
        ProceduralSfx.Play(ProceduralSfx.Kind.Hit, args.interactorObject.transform.position, 0.35f);
        if (leftHand != null) leftHand.SendHapticImpulse(0.4f, 0.06f);
        if (rightHand != null) rightHand.SendHapticImpulse(0.4f, 0.06f);
        TrainingEvents.Raise("funda");
    }
}
