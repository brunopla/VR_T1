using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// HUD en la muneca izquierda: aparece al girarla hacia la cara. Muestra vida,
// municion, oleada y tiempo, y un selector de armas:
//   A (toque)        -> siguiente arma
//   A (mantener)     -> el arma elegida vuela a la mano derecha
// (en el editor: tecla N)
public class WristHud : MonoBehaviour
{
    const float HoldToSummon = 0.45f;
    const float FlightTime = 0.3f;

    Transform head;
    Transform wrist;
    CanvasGroup group;
    TextMeshProUGUI statusText;
    TextMeshProUGUI selectorText;
    InputAction selectAction;
    float pressedAt = -1f;
    readonly List<string> equipment = new List<string>();
    int selected;
    float nextRefresh;

    public static bool Visible { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (scene, mode) => Attach();
        Attach();
    }

    static void Attach()
    {
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin != null && origin.GetComponent<WristHud>() == null)
            origin.gameObject.AddComponent<WristHud>();
    }

    void Awake()
    {
        selectAction = new InputAction("WristSelect", InputActionType.Button);
        selectAction.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
        selectAction.AddBinding("<Keyboard>/n");
    }

    void OnEnable() => selectAction.Enable();
    void OnDisable() => selectAction.Disable();
    void OnDestroy() => selectAction.Dispose();

    void Start()
    {
        head = GetComponent<XROrigin>().Camera.transform;
        wrist = PlayerRig.Controller(true);
        if (wrist != null)
            Build();
    }

    void Build()
    {
        var go = new GameObject("HUD muneca", typeof(RectTransform));
        go.transform.SetParent(wrist, false);
        // Sobre el dorso de la mano, legible desde arriba con los dedos "hacia arriba"
        go.transform.localPosition = new Vector3(0f, 0.045f, -0.1f);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = Vector3.one * 0.00025f;
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = new Vector2(560, 340);
        group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var background = new GameObject("Fondo", typeof(RectTransform)).AddComponent<Image>();
        background.transform.SetParent(go.transform, false);
        Stretch(background.rectTransform);
        background.color = new Color(0.04f, 0.05f, 0.07f, 0.85f);

        statusText = Text(go.transform, 40, new Vector2(0f, 0.38f), new Vector2(1f, 1f), TextAlignmentOptions.TopLeft);
        selectorText = Text(go.transform, 38, new Vector2(0f, 0f), new Vector2(1f, 0.38f), TextAlignmentOptions.Center);
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI Text(Transform parent, float size, Vector2 min, Vector2 max, TextAlignmentOptions align)
    {
        var text = new GameObject("Texto", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.rectTransform.anchorMin = min;
        text.rectTransform.anchorMax = max;
        text.rectTransform.offsetMin = new Vector2(24, 12);
        text.rectTransform.offsetMax = new Vector2(-24, -12);
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    void Update()
    {
        if (group == null)
            return;

        // Visible cuando el dorso de la mano mira hacia la cara
        Vector3 toHead = (head.position - wrist.position).normalized;
        bool facing = Vector3.Dot(wrist.up, toHead) > 0.55f && Vector3.Distance(head.position, wrist.position) < 0.7f;
        Visible = facing;
        group.alpha = Mathf.MoveTowards(group.alpha, facing ? 1f : 0f, Time.unscaledDeltaTime * 6f);

        if (Time.unscaledTime > nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 2f;
            RefreshEquipment();
        }
        if (group.alpha > 0.01f)
            RefreshTexts();
        HandleInput(facing);
    }

    void RefreshEquipment()
    {
        string current = equipment.Count > 0 ? equipment[Mathf.Clamp(selected, 0, equipment.Count - 1)] : null;
        equipment.Clear();
        equipment.AddRange(FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None)
            .Select(PlayerRig.EquipmentName).Where(n => n != null).Distinct().OrderBy(n => n));
        selected = current != null && equipment.Contains(current) ? equipment.IndexOf(current) : 0;
    }

    void RefreshTexts()
    {
        var gm = GameManager.instance;
        string ammo = gm != null && gm.SelectedWeapon != null ? gm.SelectedWeapon.Ammo + " / " + gm.ammo : (gm != null ? gm.ammo.ToString() : "-");
        if (gm != null && gm.isActiveAndEnabled)
        {
            int minutes = Mathf.FloorToInt(gm.timer / 60f);
            statusText.text = "<color=#FF6B5A>VIDA</color>  " + gm.health +
                              "\n<color=#FFD36B>MUNICIÓN</color>  " + ammo +
                              "\n<color=#9FD8FF>OLEADA</color>  " + gm.Wave + "   ·   " + gm.totalKills + " kills" +
                              "\n<color=#BBBBBB>TIEMPO</color>  " + minutes + ":" + Mathf.FloorToInt(gm.timer % 60f).ToString("00");
        }
        else
        {
            statusText.text = "<color=#FFD36B>MUNICIÓN</color>  " + ammo + "\n<color=#BBBBBB>Entrenamiento</color>";
        }

        selectorText.text = equipment.Count == 0
            ? "Sin armas cerca"
            : "<size=30><color=#AAAAAA>A: cambiar · mantener A: traer</color></size>\n◀  <b>" + equipment[selected] + "</b>  ▶";
    }

    void HandleInput(bool facing)
    {
        if (selectAction.WasPressedThisFrame() && facing)
            pressedAt = Time.unscaledTime;
        if (pressedAt < 0f)
            return;

        if (Time.unscaledTime - pressedAt >= HoldToSummon)
        {
            pressedAt = -1f;
            Summon();
        }
        else if (selectAction.WasReleasedThisFrame())
        {
            pressedAt = -1f;
            if (equipment.Count > 0)
                selected = (selected + 1) % equipment.Count;
        }
    }

    // El arma elegida mas cercana vuela a la mano derecha (sale de una funda si hace falta)
    void Summon()
    {
        if (equipment.Count == 0)
            return;
        var hand = PlayerRig.Hand(false);
        if (hand == null)
            return;
        if (hand.hasSelection)
        {
            VRHud.Toast("Suelta lo que tienes en la mano derecha", 1.5f);
            return;
        }

        string wanted = equipment[selected];
        var item = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None)
            .Where(g => PlayerRig.EquipmentName(g) == wanted && !PlayerRig.IsHeldByHand(g))
            .OrderBy(g => Vector3.Distance(g.transform.position, hand.transform.position))
            .FirstOrDefault();
        if (item == null)
            return;
        StartCoroutine(FlyToHand(item, hand));
    }

    IEnumerator FlyToHand(XRGrabInteractable item, XRBaseInputInteractor hand)
    {
        var manager = item.interactionManager;
        foreach (var socket in item.interactorsSelecting.OfType<XRSocketInteractor>().ToList())
            manager.SelectExit(socket, (IXRSelectInteractable)item);

        var body = item.GetComponent<Rigidbody>();
        if (body != null)
            body.isKinematic = true;
        Vector3 start = item.transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / FlightTime)
        {
            item.transform.position = Vector3.Lerp(start, hand.transform.position, t * t * (3f - 2f * t));
            yield return null;
        }

        // XRGrabInteractable recuerda si era cinematico: devolverle la fisica antes
        if (body != null)
            body.isKinematic = false;
        if (!hand.hasSelection)
        {
            manager.SelectEnter(hand, (IXRSelectInteractable)item);
            hand.SendHapticImpulse(0.6f, 0.08f);
            TrainingEvents.Raise("muneca");
        }
    }
}
