using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// HUD de VR creado por codigo: avisos cortos, destello de dano, vineta de
// comodidad y pantalla final. No hace falta ponerlo en ninguna escena.
public class VRHud : MonoBehaviour
{
    const float OverlayDistance = 0.25f;
    const float ToastDistance = 1.3f;
    const float EndScreenDistance = 1.6f;
    const float EndScreenAutoContinue = 20f;

    static VRHud instance;

    // Intensidad de la vineta (0..1); la fija ComfortSettings cada frame
    public static float VignetteStrength;

    Camera cam;
    Image vignette;
    Image damage;
    Canvas toastCanvas;
    TextMeshProUGUI toastText;
    float toastUntil;
    float damageAlpha;

    GameObject endScreen;
    Action onContinue;
    float endScreenShownAt;
    InputAction continueAction;

    static VRHud Get()
    {
        if (instance == null)
            instance = new GameObject("VRHud").AddComponent<VRHud>();
        return instance;
    }

    public static void Ensure()
    {
        Get();
    }

    public static void Toast(string message, float seconds = 2.5f)
    {
        var hud = Get();
        if (!hud.EnsureCamera())
            return;

        hud.toastText.text = message;
        hud.toastUntil = Time.unscaledTime + seconds;
        hud.toastCanvas.gameObject.SetActive(true);
        hud.PlaceInFront(hud.toastCanvas.transform, ToastDistance, -0.25f);
    }

    public static void DamageFlash()
    {
        Get().damageAlpha = 0.55f;
    }

    public static void ShowEndScreen(string title, string body, Action onContinue)
    {
        var hud = Get();
        if (!hud.EnsureCamera())
        {
            onContinue?.Invoke();
            return;
        }
        hud.BuildEndScreen(title, body);
        hud.onContinue = onContinue;
        hud.endScreenShownAt = Time.unscaledTime;
    }

    void OnDestroy()
    {
        continueAction?.Dispose();
        if (instance == this)
            instance = null;
    }

    bool EnsureCamera()
    {
        if (cam != null)
            return true;

        cam = Camera.main;
        if (cam == null)
            return false;

        BuildOverlay();
        BuildToast();
        return true;
    }

    void Update()
    {
        if (!EnsureCamera())
            return;

        var v = vignette.color;
        v.a = Mathf.MoveTowards(v.a, Mathf.Clamp01(VignetteStrength), Time.unscaledDeltaTime * 3f);
        vignette.color = v;
        vignette.enabled = v.a > 0.01f;

        damageAlpha = Mathf.MoveTowards(damageAlpha, 0f, Time.unscaledDeltaTime * 1.2f);
        damage.color = new Color(0.7f, 0f, 0f, damageAlpha);
        damage.enabled = damageAlpha > 0.01f;

        if (toastCanvas.gameObject.activeSelf)
        {
            if (Time.unscaledTime > toastUntil)
                toastCanvas.gameObject.SetActive(false);
            else
                FollowLazily(toastCanvas.transform, ToastDistance, -0.25f);
        }

        if (endScreen != null && onContinue != null)
        {
            // Pequena espera para que un disparo en curso no cierre la pantalla
            float shown = Time.unscaledTime - endScreenShownAt;
            bool pressed = shown > 1.5f && continueAction.WasPressedThisFrame();
            if (pressed || shown > EndScreenAutoContinue)
            {
                var callback = onContinue;
                onContinue = null;
                callback();
            }
        }
    }

    void PlaceInFront(Transform t, float distance, float height)
    {
        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = cam.transform.up;
        forward.Normalize();
        t.position = cam.transform.position + forward * distance + Vector3.up * height;
        t.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    void FollowLazily(Transform t, float distance, float height)
    {
        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 target = cam.transform.position + forward * distance + Vector3.up * height;
        t.position = Vector3.Lerp(t.position, target, Time.unscaledDeltaTime * 4f);
        t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(forward, Vector3.up), Time.unscaledDeltaTime * 4f);
    }

    static Canvas CreateWorldCanvas(string name, Transform parent, Vector2 size, float metersPerPixel)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = size;
        go.transform.localScale = Vector3.one * metersPerPixel;
        return canvas;
    }

    static Image CreateImage(Transform parent, string name, Color color, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, float size, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(30, 10);
        rect.offsetMax = new Vector2(-30, -10);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    void BuildOverlay()
    {
        // Lienzo pegado a la camara: vineta y destello de dano
        var canvas = CreateWorldCanvas("HudOverlay", cam.transform, new Vector2(1200, 1200), 0.001f);
        canvas.transform.localPosition = new Vector3(0, 0, OverlayDistance);
        canvas.transform.localRotation = Quaternion.identity;

        vignette = CreateImage(canvas.transform, "Vignette", new Color(0, 0, 0, 0), CreateVignetteSprite());
        damage = CreateImage(canvas.transform, "DamageFlash", new Color(0.7f, 0, 0, 0), CreateVignetteSprite(0.15f));
        vignette.enabled = false;
        damage.enabled = false;
    }

    void BuildToast()
    {
        toastCanvas = CreateWorldCanvas("HudToast", transform, new Vector2(700, 140), 0.0012f);
        CreateImage(toastCanvas.transform, "Background", new Color(0, 0, 0, 0.65f));
        toastText = CreateText(toastCanvas.transform, "Text", 48, Vector2.zero, Vector2.one);
        toastCanvas.gameObject.SetActive(false);
    }

    void BuildEndScreen(string title, string body)
    {
        if (endScreen != null)
            Destroy(endScreen);

        var canvas = CreateWorldCanvas("EndScreen", transform, new Vector2(900, 520), 0.0015f);
        endScreen = canvas.gameObject;
        CreateImage(canvas.transform, "Background", new Color(0.05f, 0.05f, 0.08f, 0.9f));
        CreateText(canvas.transform, "Title", 90, new Vector2(0, 0.62f), new Vector2(1, 1)).text = title;
        CreateText(canvas.transform, "Body", 44, new Vector2(0, 0.22f), new Vector2(1, 0.62f)).text = body;
        var hint = CreateText(canvas.transform, "Hint", 34, new Vector2(0, 0), new Vector2(1, 0.22f));
        hint.text = "Aprieta el gatillo para continuar";
        hint.color = new Color(1, 1, 1, 0.7f);
        PlaceInFront(endScreen.transform, EndScreenDistance, 0f);

        if (continueAction == null)
        {
            continueAction = new InputAction("Continue", InputActionType.Button);
            continueAction.AddBinding("<XRController>/{TriggerButton}");
            continueAction.AddBinding("<Keyboard>/space");
            continueAction.Enable();
        }
    }

    // Degradado radial: transparente al centro y opaco en los bordes
    static Sprite CreateVignetteSprite(float inner = 0.35f)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 0.75f;
                byte a = (byte)(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, 1f, r)) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
