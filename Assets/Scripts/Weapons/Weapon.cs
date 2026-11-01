using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Arma de disparo instantaneo (raycast). Reemplaza a Bspawn y Bullet_Spawn.
// Se conecta sola al XRGrabInteractable: no necesita UnityEvents en el prefab.
[RequireComponent(typeof(XRGrabInteractable))]
public class Weapon : MonoBehaviour
{
    const float TracerDuration = 0.05f;
    static readonly RaycastHit[] hits = new RaycastHit[16];
    static ObjectPool<LineRenderer> tracerPool;
    static Material tracerMaterial;

    public WeaponData data;
    [Tooltip("Punto y direccion de salida del disparo")]
    public Transform muzzle;
    public GameObject muzzleFlashPrefab;
    public GameObject impactPrefab;
    [Tooltip("Material de la estela del disparo (Sprites/Default)")]
    public Material tracer;
    [Tooltip("Capas que puede golpear el disparo")]
    public LayerMask hitMask = ~((1 << 2) | (1 << 7) | (1 << 8));
    [Tooltip("Contador en el arma; si esta vacio se crea solo")]
    public TMP_Text ammoText;
    public AudioSource audioSource;

    [Header("Recarga")]
    [Tooltip("Apuntar al suelo durante este tiempo recarga desde la reserva")]
    public float reloadHoldTime = 0.4f;
    [Tooltip("Angulo maximo respecto a la vertical para contar como 'al suelo'")]
    public float reloadAngle = 30f;
    [Tooltip("Opcional: socket donde se inserta un Magazine")]
    public XRSocketInteractor magazineSocket;

    public int Ammo { get; private set; }

    XRGrabInteractable grab;
    IXRSelectInteractor holder;
    bool triggerHeld;
    float nextShotTime;
    float pointingDownTime;
    Coroutine fireRoutine;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (muzzle == null)
            muzzle = transform;
        if (tracerMaterial == null)
            tracerMaterial = tracer;
        Ammo = data != null ? data.magazineSize : 0;
        if (ammoText == null)
            ammoText = CreateAmmoText();
        RefreshAmmo();
    }

    void OnEnable()
    {
        grab.activated.AddListener(OnActivated);
        grab.deactivated.AddListener(OnDeactivated);
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
        if (magazineSocket != null)
            magazineSocket.selectEntered.AddListener(OnMagazineInserted);
    }

    void OnDisable()
    {
        grab.activated.RemoveListener(OnActivated);
        grab.deactivated.RemoveListener(OnDeactivated);
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
        if (magazineSocket != null)
            magazineSocket.selectEntered.RemoveListener(OnMagazineInserted);
        triggerHeld = false;
        fireRoutine = null;
    }

    void Update()
    {
        if (holder == null || data == null || Ammo >= data.magazineSize)
        {
            pointingDownTime = 0;
            return;
        }

        if (Vector3.Angle(muzzle.forward, Vector3.down) < reloadAngle)
        {
            pointingDownTime += Time.deltaTime;
            if (pointingDownTime >= reloadHoldTime)
            {
                pointingDownTime = 0;
                ReloadFromReserve();
            }
        }
        else
        {
            pointingDownTime = 0;
        }
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        holder = args.interactorObject;
        if (GameManager.instance != null)
            GameManager.instance.SelectWeapon(this);
    }

    void OnReleased(SelectExitEventArgs args)
    {
        if (args.interactorObject != holder)
            return;
        holder = null;
        triggerHeld = false;
        if (GameManager.instance != null)
            GameManager.instance.DeselectWeapon(this);
    }

    void OnActivated(ActivateEventArgs args)
    {
        triggerHeld = true;
        if (fireRoutine == null)
            fireRoutine = StartCoroutine(FireLoop());
    }

    void OnDeactivated(DeactivateEventArgs args)
    {
        triggerHeld = false;
    }

    IEnumerator FireLoop()
    {
        bool firedOnce = false;
        while (!firedOnce || (data.automatic && triggerHeld))
        {
            if (Time.time >= nextShotTime)
            {
                firedOnce = true;
                nextShotTime = Time.time + 1f / Mathf.Max(0.1f, data.fireRate);
                if (!TryFire())
                    break;
            }
            yield return null;
        }
        fireRoutine = null;
    }

    bool TryFire()
    {
        if (Ammo <= 0)
        {
            PlayClip(data.emptyClip);
            return false;
        }

        Ammo--;
        RefreshAmmo();

        for (int i = 0; i < Mathf.Max(1, data.pellets); i++)
        {
            Vector3 direction = Spread(muzzle.forward, data.spreadAngle);
            Vector3 end = muzzle.position + direction * data.range;
            if (Raycast(muzzle.position, direction, out RaycastHit hit))
            {
                end = hit.point;
                ApplyHit(hit);
            }
            ShowTracer(muzzle.position, end);
        }

        PlayClip(data.shotClip);
        if (muzzleFlashPrefab != null)
            Destroy(Instantiate(muzzleFlashPrefab, muzzle.position, muzzle.rotation, muzzle), 1f);
        SendHaptics(data.hapticAmplitude, data.hapticDuration);
        return true;
    }

    // El primer impacto que no sea el arma ni el jugador. Los triggers solo
    // cuentan si son Hitbox (la cabeza de los zombies es un trigger)
    bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit closest)
    {
        closest = default;
        int count = Physics.RaycastNonAlloc(origin, direction, hits, data.range, hitMask, QueryTriggerInteraction.Collide);
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var c = hits[i].collider;
            if (hits[i].distance >= best || c.transform.IsChildOf(transform) || c.CompareTag("Player"))
                continue;
            if (c.isTrigger && c.GetComponent<Hitbox>() == null)
                continue;
            best = hits[i].distance;
            closest = hits[i];
        }
        return best < float.MaxValue;
    }

    void ApplyHit(RaycastHit hit)
    {
        var hitbox = hit.collider.GetComponent<Hitbox>();
        if (hitbox != null)
            hitbox.Hit(data.damage, hit.point);
        else
            hit.collider.GetComponentInParent<IDamageable>()?.TakeHit(data.damage, hit.point, false);

        if (hit.rigidbody != null && hitbox == null)
            hit.rigidbody.AddForceAtPosition(-hit.normal * 2f, hit.point, ForceMode.Impulse);

        if (impactPrefab != null)
            Destroy(Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal)), 2f);
    }

    public void ReloadFromReserve()
    {
        var gm = GameManager.instance;
        if (gm == null || data == null)
            return;

        int taken = gm.TakeReserve(data.magazineSize - Ammo);
        if (taken <= 0)
        {
            VRHud.Toast("Sin munición de reserva", 1.5f);
            return;
        }
        Ammo += taken;
        OnReloaded();
    }

    void OnMagazineInserted(SelectEnterEventArgs args)
    {
        var magazine = args.interactableObject.transform.GetComponent<Magazine>();
        if (magazine == null || (magazine.compatibleWith != null && magazine.compatibleWith != data))
            return;

        Ammo = Mathf.Min(data.magazineSize, magazine.rounds);
        Destroy(magazine.gameObject);
        OnReloaded();
    }

    void OnReloaded()
    {
        RefreshAmmo();
        PlayClip(data.reloadClip);
        SendHaptics(0.3f, 0.05f);
    }

    void RefreshAmmo()
    {
        if (ammoText != null)
        {
            ammoText.text = Ammo.ToString();
            ammoText.color = Ammo == 0 ? new Color(1f, 0.3f, 0.3f) : Color.white;
        }
        if (GameManager.instance != null && GameManager.instance.SelectedWeapon == this)
            GameManager.instance.RefreshAmmo();
    }

    void PlayClip(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    void SendHaptics(float amplitude, float duration)
    {
        if (holder is XRBaseInputInteractor input)
            input.SendHapticImpulse(amplitude, duration);
    }

    static Vector3 Spread(Vector3 forward, float angle)
    {
        if (angle <= 0f)
            return forward;
        Vector2 offset = Random.insideUnitCircle * Mathf.Tan(angle * Mathf.Deg2Rad);
        var rotation = Quaternion.LookRotation(forward);
        return (forward + rotation * new Vector3(offset.x, offset.y, 0f)).normalized;
    }

    void ShowTracer(Vector3 from, Vector3 to)
    {
        if (tracerPool == null)
        {
            tracerPool = new ObjectPool<LineRenderer>(CreateTracer,
                lr => lr.gameObject.SetActive(true),
                lr => lr.gameObject.SetActive(false),
                lr => { if (lr != null) Destroy(lr.gameObject); },
                collectionCheck: false, defaultCapacity: 16, maxSize: 64);
        }
        var tracer = tracerPool.Get();
        tracer.SetPosition(0, from);
        tracer.SetPosition(1, to);
        StartCoroutine(ReleaseTracer(tracer));
    }

    static IEnumerator ReleaseTracer(LineRenderer tracer)
    {
        yield return new WaitForSeconds(TracerDuration);
        if (tracer != null)
            tracerPool.Release(tracer);
    }

    static LineRenderer CreateTracer()
    {
        if (tracerMaterial == null)
            tracerMaterial = new Material(Shader.Find("Sprites/Default"));
        var go = new GameObject("Tracer");
        DontDestroyOnLoad(go);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = tracerMaterial;
        lr.positionCount = 2;
        lr.startWidth = 0.012f;
        lr.endWidth = 0.004f;
        lr.startColor = new Color(1f, 0.9f, 0.6f, 0.9f);
        lr.endColor = new Color(1f, 0.8f, 0.4f, 0f);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    // Contador de balas encima del arma, legible desde atras
    TMP_Text CreateAmmoText()
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var local = new Bounds(transform.InverseTransformPoint(r.bounds.center), Vector3.zero);
            local.Encapsulate(transform.InverseTransformPoint(r.bounds.min));
            local.Encapsulate(transform.InverseTransformPoint(r.bounds.max));
            if (any) bounds.Encapsulate(local); else bounds = local;
            any = true;
        }

        var go = new GameObject("AmmoCounter");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(bounds.center.x, bounds.max.y + 0.03f, bounds.center.z);
        go.transform.rotation = Quaternion.LookRotation(muzzle.forward, transform.up);
        var text = go.AddComponent<TextMeshPro>();
        text.fontSize = 0.4f;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(0.2f, 0.06f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
}
