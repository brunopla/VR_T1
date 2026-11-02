using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Mira telescopica: una camara con poco campo de vision dibuja en el lente y
// encima va una reticula transparente. Solo renderiza mientras el arma esta en
// la mano y el ojo esta cerca del lente: el resto del tiempo el lente se ve
// oscuro y el Quest no gasta una camara extra.
public class SniperScope : MonoBehaviour
{
    static readonly int MainTex = Shader.PropertyToID("_MainTex");

    [Tooltip("Disco donde se ve la imagen ampliada; su eje Y apunta al ojo")]
    public Renderer lens;
    [Tooltip("Desde donde mira la camara de la mira, apuntando hacia adelante")]
    public Transform scopeCamera;
    [Tooltip("Campo de vision de la mira (60 / zoom)")]
    public float fieldOfView = 8f;
    public int resolution = 512;
    [Tooltip("Distancia del ojo al lente para encender la mira")]
    public float eyeDistance = 0.3f;
    [Tooltip("Material Unlit/Texture para el lente")]
    public Material lensMaterial;
    [Tooltip("Material transparente (Sprites/Default) para la reticula")]
    public Material reticleMaterial;

    Camera cam;
    RenderTexture texture;
    XRGrabInteractable grab;
    Transform head;
    Material lensInstance;
    Renderer reticle;

    void Awake()
    {
        grab = GetComponentInParent<XRGrabInteractable>();
        texture = new RenderTexture(resolution, resolution, 16) { name = "Mira" };

        cam = scopeCamera.gameObject.AddComponent<Camera>();
        cam.fieldOfView = fieldOfView;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 400f;
        cam.targetTexture = texture;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.enabled = false;

        lensInstance = new Material(lensMaterial != null ? lensMaterial : new Material(Shader.Find("Unlit/Texture")));
        lensInstance.SetTexture(MainTex, texture);
        lens.sharedMaterial = lensInstance;
        reticle = CreateReticle();
    }

    void OnDestroy()
    {
        if (texture != null)
            texture.Release();
        Destroy(lensInstance);
    }

    void LateUpdate()
    {
        if (head == null && Camera.main != null)
            head = Camera.main.transform;

        bool held = grab == null || grab.isSelected;
        bool near = head != null && Vector3.Distance(head.position, lens.transform.position) < eyeDistance;
        bool active = held && near;
        if (cam.enabled == active)
            return;
        cam.enabled = active;
        reticle.enabled = active;
        lensInstance.color = active ? Color.white : new Color(0.04f, 0.04f, 0.05f);
    }

    // Cruz fina con marcas y borde oscuro, en una capa delante del lente
    Renderer CreateReticle()
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        var line = new Color32(10, 10, 10, 255);
        int c = size / 2;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                byte edge = (byte)(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1f, r)) * 255);
                bool cross = (Mathf.Abs(x - c) <= 1 || Mathf.Abs(y - c) <= 1) && r > 0.04f;
                bool tick = (Mathf.Abs(x - c) <= 5 && (y - c) % 24 == 0 && y != c) || (Mathf.Abs(y - c) <= 5 && (x - c) % 24 == 0 && x != c);
                pixels[y * size + x] = cross || tick ? line : new Color32(0, 0, 0, edge);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Reticula";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(lens.transform, false);
        // El quad mira por -Z; el lente (cilindro) apunta su eje Y al ojo
        // La tapa del cilindro primitivo esta en y = 1 (local)
        quad.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(1f, 1f, 1f);
        var renderer = quad.GetComponent<Renderer>();
        var material = new Material(reticleMaterial != null ? reticleMaterial : new Material(Shader.Find("Sprites/Default")));
        material.mainTexture = tex;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.enabled = false;
        return renderer;
    }
}
