using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Entrenamiento guiado (estilo campo de entrenamiento): un objetivo por paso con
// los botones del control, barra de avance, "COMPLETADO" al cumplirlo, y un anillo
// con haz de luz y una linea guia hasta la siguiente estacion.
// Mantener B + Y (o la tecla K) salta el paso actual.
public class TrainingCourse : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        public string title;
        [TextArea] public string detail;
        [Tooltip("Botones separados por |, por ejemplo GRIP|GATILLO")]
        public string buttons;
        [Tooltip("Nombre de la estacion (hijo de 'stations'); vacio = donde esta el jugador")]
        public string station;
        [Tooltip("Evento que cumple el paso: llegar, mirar, agarrar, blanco:galeria, recarga...")]
        public string eventId;
        public int count = 1;
    }

    public Transform stations;
    public List<Step> steps = new List<Step>();
    [Tooltip("Distancia a la estacion para los pasos 'llegar'")]
    public float arriveDistance = 1.8f;

    static readonly Color Accent = new Color(1f, 0.55f, 0.15f);
    static readonly Color Done = new Color(0.35f, 1f, 0.5f);

    int current = -1;
    int progress;
    float startTime;
    float lookedYaw;
    float lastYaw;
    bool completing;
    XROrigin origin;
    Transform head;

    Canvas canvas;
    TextMeshProUGUI stepText, titleText, detailText, countText, bannerText;
    RectTransform chipsRow, segmentsRow;
    readonly List<Image> segments = new List<Image>();
    GameObject marker;
    LineRenderer guide;
    InputAction skipLeft, skipRight;
    float skipHeld;
    Material glow;

    void Start()
    {
        origin = FindFirstObjectByType<XROrigin>();
        head = origin != null ? origin.Camera.transform : Camera.main.transform;
        glow = new Material(Shader.Find("Sprites/Default"));

        skipLeft = new InputAction("SkipLeft", InputActionType.Button, "<XRController>{LeftHand}/{SecondaryButton}");
        skipRight = new InputAction("SkipRight", InputActionType.Button, "<XRController>{RightHand}/{SecondaryButton}");
        skipRight.AddBinding("<Keyboard>/k");
        skipLeft.Enable();
        skipRight.Enable();

        BuildUi();
        BuildMarker();
        TrainingEvents.Raised += OnTrainingEvent;
        startTime = Time.time;
        lastYaw = head.eulerAngles.y;
        StartCoroutine(BeginAfterIntro());
    }

    void OnDestroy()
    {
        TrainingEvents.Raised -= OnTrainingEvent;
        skipLeft?.Dispose();
        skipRight?.Dispose();
    }

    IEnumerator BeginAfterIntro()
    {
        yield return new WaitForSeconds(1.5f);
        GoTo(0);
    }

    // ---------------------------------------------------------------- pasos

    Step CurrentStep => current >= 0 && current < steps.Count ? steps[current] : null;

    Transform StationOf(Step step)
    {
        if (step == null || string.IsNullOrEmpty(step.station) || stations == null)
            return null;
        return stations.Find(step.station);
    }

    void GoTo(int index)
    {
        current = index;
        progress = 0;
        completing = false;
        lookedYaw = 0f;
        var step = CurrentStep;
        if (step == null)
        {
            Finish();
            return;
        }

        stepText.text = "Paso " + (index + 1) + " de " + steps.Count;
        titleText.text = step.title;
        detailText.text = step.detail;
        countText.text = step.count > 1 ? "0 / " + step.count : "";
        bannerText.gameObject.SetActive(false);
        SetChips(step.buttons);
        for (int i = 0; i < segments.Count; i++)
            segments[i].color = i < index ? Done : (i == index ? Accent : new Color(1f, 1f, 1f, 0.18f));

        var station = StationOf(step);
        marker.SetActive(station != null);
        if (station != null)
            marker.transform.position = station.position;
        PlaceCardInFront();
    }

    void OnTrainingEvent(string id)
    {
        var step = CurrentStep;
        if (step == null || completing || id != step.eventId)
            return;
        AddProgress();
    }

    void AddProgress()
    {
        var step = CurrentStep;
        progress++;
        if (step.count > 1)
            countText.text = Mathf.Min(progress, step.count) + " / " + step.count;
        if (progress >= step.count)
            StartCoroutine(Complete());
    }

    IEnumerator Complete()
    {
        completing = true;
        bannerText.text = "✔  COMPLETADO";
        bannerText.color = Done;
        bannerText.gameObject.SetActive(true);
        segments[current].color = Done;
        ProceduralSfx.Play(ProceduralSfx.Kind.Hit, head.position + head.forward, 0.5f);
        var left = PlayerRig.Hand(true);
        var right = PlayerRig.Hand(false);
        if (left != null) left.SendHapticImpulse(0.3f, 0.1f);
        if (right != null) right.SendHapticImpulse(0.3f, 0.1f);
        yield return new WaitForSeconds(1.3f);
        GoTo(current + 1);
    }

    void Finish()
    {
        float elapsed = Time.time - startTime;
        stepText.text = "Entrenamiento completado";
        titleText.text = "¡Listo para la ciudad!";
        detailText.text = "Tiempo: " + Mathf.FloorToInt(elapsed / 60f) + ":" + Mathf.FloorToInt(elapsed % 60f).ToString("00") +
                          "\nCruza el portal cuando quieras.";
        countText.text = "";
        SetChips("");
        bannerText.gameObject.SetActive(false);
        marker.SetActive(false);
        guide.enabled = false;
    }

    void Update()
    {
        if (head == null)
            return;

        var step = CurrentStep;
        if (step != null && !completing)
        {
            if (step.eventId == "mirar")
            {
                float yaw = head.eulerAngles.y;
                lookedYaw += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
                lastYaw = yaw;
                if (lookedYaw > 150f)
                    AddProgress();
            }
            else if (step.eventId == "llegar")
            {
                var station = StationOf(step);
                if (station != null && FlatDistance(head.position, station.position) < arriveDistance)
                    AddProgress();
            }
        }

        // Saltar el paso: mantener B + Y
        bool skipping = skipRight.IsPressed() && (skipLeft.IsPressed() || Keyboard.current != null && Keyboard.current.kKey.isPressed);
        skipHeld = skipping ? skipHeld + Time.deltaTime : 0f;
        if (skipHeld > 1.5f && step != null && !completing)
        {
            skipHeld = 0f;
            StartCoroutine(Complete());
        }

        FollowHead();
        UpdateGuide();
        AnimateMarker();
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------------------------------------------------------------- visual

    void UpdateGuide()
    {
        var station = StationOf(CurrentStep);
        guide.enabled = station != null && !completing;
        if (!guide.enabled)
            return;
        Vector3 feet = new Vector3(head.position.x, origin != null ? origin.transform.position.y + 0.05f : head.position.y - 1.5f, head.position.z);
        Vector3 start = feet + Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized * 0.6f;
        guide.SetPosition(0, start);
        guide.SetPosition(1, station.position + Vector3.up * 0.05f);
        guide.material.mainTextureOffset = new Vector2(-Time.time * 1.5f, 0f);
    }

    void AnimateMarker()
    {
        if (!marker.activeSelf)
            return;
        float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.08f;
        marker.transform.GetChild(0).localScale = new Vector3(2.4f * pulse, 0.01f, 2.4f * pulse);
        marker.transform.GetChild(2).localPosition = new Vector3(0f, 2.2f + Mathf.Sin(Time.time * 2.5f) * 0.15f, 0f);
        var toHead = Vector3.ProjectOnPlane(head.position - marker.transform.position, Vector3.up);
        if (toHead.sqrMagnitude > 0.01f)
            marker.transform.GetChild(2).rotation = Quaternion.LookRotation(-toHead) * Quaternion.Euler(0f, 0f, 45f + Time.time * 60f);
    }

    void BuildMarker()
    {
        marker = new GameObject("Marcador estacion");
        var ring = Primitive(PrimitiveType.Cylinder, "Anillo", marker.transform, new Color(Accent.r, Accent.g, Accent.b, 0.55f));
        ring.transform.localScale = new Vector3(2.4f, 0.01f, 2.4f);
        var beam = Primitive(PrimitiveType.Cylinder, "Haz", marker.transform, new Color(Accent.r, Accent.g, Accent.b, 0.12f));
        beam.transform.localScale = new Vector3(0.6f, 6f, 0.6f);
        beam.transform.localPosition = new Vector3(0f, 6f, 0f);
        // Rombo flotante sobre la estacion (un cubo plano girado 45 grados)
        var arrow = Primitive(PrimitiveType.Cube, "Flecha", marker.transform, Accent);
        arrow.transform.localScale = new Vector3(0.35f, 0.35f, 0.05f);
        marker.SetActive(false);

        var lineObject = new GameObject("Linea guia");
        guide = lineObject.AddComponent<LineRenderer>();
        guide.positionCount = 2;
        guide.startWidth = 0.08f;
        guide.endWidth = 0.08f;
        guide.material = new Material(glow) { mainTexture = DashTexture() };
        guide.textureMode = LineTextureMode.Tile;
        guide.startColor = new Color(Accent.r, Accent.g, Accent.b, 0.9f);
        guide.endColor = new Color(Accent.r, Accent.g, Accent.b, 0.5f);
        guide.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        guide.enabled = false;
    }

    GameObject Primitive(PrimitiveType type, string name, Transform parent, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = new Material(glow) { color = color };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static Texture2D DashTexture()
    {
        var tex = new Texture2D(32, 4) { wrapMode = TextureWrapMode.Repeat };
        for (int x = 0; x < 32; x++)
            for (int y = 0; y < 4; y++)
                tex.SetPixel(x, y, x < 18 ? Color.white : new Color(1f, 1f, 1f, 0f));
        tex.Apply();
        return tex;
    }

    void BuildUi()
    {
        var go = new GameObject("Tarjeta entrenamiento", typeof(RectTransform));
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 90;
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(900, 420);
        go.transform.localScale = Vector3.one * 0.0011f;

        var background = Panel(go.transform, "Fondo", new Color(0.04f, 0.05f, 0.07f, 0.88f));
        Stretch(background.rectTransform, 0f, 0f, 1f, 1f);

        // Barra superior: ENTRENAMIENTO  ▬▬▬───  Paso n de N
        var label = Label(go.transform, "ENTRENAMIENTO", 30, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.MidlineLeft);
        Stretch(label.rectTransform, 0.04f, 0.84f, 0.4f, 0.97f);
        label.characterSpacing = 12f;
        segmentsRow = new GameObject("Segmentos", typeof(RectTransform)).GetComponent<RectTransform>();
        segmentsRow.SetParent(go.transform, false);
        Stretch(segmentsRow, 0.38f, 0.88f, 0.74f, 0.93f);
        var layout = segmentsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childForceExpandWidth = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        for (int i = 0; i < steps.Count; i++)
            segments.Add(Panel(segmentsRow, "Segmento", new Color(1f, 1f, 1f, 0.18f)));
        stepText = Label(go.transform, "", 30, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.MidlineRight);
        Stretch(stepText.rectTransform, 0.75f, 0.84f, 0.96f, 0.97f);

        // Botones del control
        chipsRow = new GameObject("Botones", typeof(RectTransform)).GetComponent<RectTransform>();
        chipsRow.SetParent(go.transform, false);
        Stretch(chipsRow, 0.04f, 0.62f, 0.96f, 0.8f);
        var chipsLayout = chipsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        chipsLayout.spacing = 14f;
        chipsLayout.childAlignment = TextAnchor.MiddleLeft;
        chipsLayout.childControlWidth = false;
        chipsLayout.childControlHeight = true;
        chipsLayout.childForceExpandWidth = false;

        titleText = Label(go.transform, "", 58, Color.white, TextAlignmentOptions.MidlineLeft);
        titleText.fontStyle = FontStyles.Bold;
        Stretch(titleText.rectTransform, 0.04f, 0.43f, 0.8f, 0.62f);
        countText = Label(go.transform, "", 52, Accent, TextAlignmentOptions.MidlineRight);
        Stretch(countText.rectTransform, 0.7f, 0.43f, 0.96f, 0.62f);
        detailText = Label(go.transform, "", 34, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.TopLeft);
        Stretch(detailText.rectTransform, 0.04f, 0.06f, 0.96f, 0.42f);
        detailText.textWrappingMode = TextWrappingModes.Normal;

        bannerText = Label(go.transform, "", 90, Done, TextAlignmentOptions.Center);
        Stretch(bannerText.rectTransform, 0f, 0.3f, 1f, 0.75f);
        bannerText.fontStyle = FontStyles.Bold;
        var bannerBack = Panel(bannerText.transform, "Fondo", new Color(0.02f, 0.08f, 0.03f, 0.92f));
        Stretch(bannerBack.rectTransform, 0f, 0f, 1f, 1f);
        bannerBack.transform.SetAsFirstSibling();
        bannerText.gameObject.SetActive(false);

        var hint = Label(go.transform, "Mantén B + Y para saltar el paso", 24, new Color(1f, 1f, 1f, 0.4f), TextAlignmentOptions.BottomRight);
        Stretch(hint.rectTransform, 0.5f, 0.01f, 0.97f, 0.08f);
    }

    void SetChips(string buttons)
    {
        foreach (Transform child in chipsRow)
            Destroy(child.gameObject);
        if (string.IsNullOrEmpty(buttons))
            return;
        foreach (var label in buttons.Split('|'))
        {
            var chip = Panel(chipsRow, "Boton", new Color(1f, 1f, 1f, 0.12f));
            var text = Label(chip.transform, label.Trim(), 32, Color.white, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            Stretch(text.rectTransform, 0f, 0f, 1f, 1f);
            ((RectTransform)chip.transform).sizeDelta = new Vector2(text.GetPreferredValues(label).x + 50f, 0f);
            var outline = chip.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);
        }
    }

    static Image Panel(Transform parent, string name, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var label = new GameObject("Texto", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    static void Stretch(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // La tarjeta flota abajo al frente y sigue la mirada con suavidad
    void PlaceCardInFront()
    {
        canvas.transform.position = CardTarget(out var rotation);
        canvas.transform.rotation = rotation;
    }

    void FollowHead()
    {
        var target = CardTarget(out var rotation);
        if (Vector3.Distance(canvas.transform.position, target) > 0.6f || Quaternion.Angle(canvas.transform.rotation, rotation) > 35f)
        {
            canvas.transform.position = Vector3.Lerp(canvas.transform.position, target, Time.deltaTime * 2.5f);
            canvas.transform.rotation = Quaternion.Slerp(canvas.transform.rotation, rotation, Time.deltaTime * 2.5f);
        }
    }

    Vector3 CardTarget(out Quaternion rotation)
    {
        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();
        rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(15f, 0f, 0f);
        return head.position + forward * 1.25f + Vector3.down * 0.45f;
    }
}
