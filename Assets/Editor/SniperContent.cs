using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Rifle de francotirador: modelo M107 con la mira SR_Scope_00, lente con camara
// de zoom, agarre a dos manos y balas que atraviesan enemigos. Crea el prefab y
// lo coloca junto a las demas armas. Se puede repetir sin duplicar.
// Batch: Unity -batchmode -quit -executeMethod SniperContent.Run
public static class SniperContent
{
    const string ModelPath = "Assets/Low Poly Weapons VOL.1/Prefabs/M107.prefab";
    const string ScopePath = "Assets/Low Poly Weapons VOL.1/Prefabs/SR_Scope_00.prefab";
    const string DataPath = "Assets/Data/Weapons/Francotirador.asset";
    const string PrefabPath = "Assets/Prefabs/Gameplay/Francotirador.prefab";
    const string TracerPath = "Assets/Data/TracerMaterial.mat";
    const string LensMaterialPath = "Assets/Data/Materials/MiraLente.mat";
    const string ReticleMaterialPath = "Assets/Data/Materials/MiraReticula.mat";
    const float RifleLength = 1.35f;
    const float ScopeLength = 0.4f;

    [MenuItem("VR_T1/Crear francotirador")]
    public static void Run()
    {
        var data = EnsureData();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            prefab = Build(data);
        Place("Assets/Scenes/Prototipable.unity", prefab);
        Place("Assets/Scenes/level_00.unity", prefab);
        AssetDatabase.SaveAssets();
        Debug.Log("[SniperContent] Francotirador listo: " + PrefabPath);
    }

    static WeaponData EnsureData()
    {
        var data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataPath);
        if (data != null)
            return data;

        data = ScriptableObject.CreateInstance<WeaponData>();
        data.displayName = "Francotirador";
        data.damage = 6f;          // un zombie normal (5 de vida) cae de un tiro; en la cabeza x3
        data.penetration = 3;
        data.range = 300f;
        data.fireRate = 0.7f;
        data.magazineSize = 5;
        data.hapticAmplitude = 1f;
        data.hapticDuration = 0.2f;
        var shot = AssetDatabase.FindAssets("SFX_Assault Single Shot t:AudioClip").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
        if (shot != null)
            data.shotClip = AssetDatabase.LoadAssetAtPath<AudioClip>(shot);
        AssetDatabase.CreateAsset(data, DataPath);
        return data;
    }

    static Material EnsureMaterial(string path, string shader)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;
        material = new Material(Shader.Find(shader));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static GameObject Build(WeaponData data)
    {
        var root = new GameObject("Francotirador");
        var model = AddVisual(ModelPath, root.transform, RifleLength);
        var gun = LocalBounds(root.transform, model);

        // Mira encima del cajon, un poco hacia atras
        var scope = AddVisual(ScopePath, root.transform, ScopeLength);
        var scopeBounds = LocalBounds(root.transform, scope);
        scope.localPosition += new Vector3(gun.center.x - scopeBounds.center.x,
                                           gun.max.y - scopeBounds.min.y - 0.01f,
                                           gun.center.z - RifleLength * 0.12f - scopeBounds.center.z);
        scopeBounds = LocalBounds(root.transform, scope);

        // Lente en la parte de atras de la mira (eje Y del disco hacia el ojo)
        var lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lens.name = "Lente";
        Object.DestroyImmediate(lens.GetComponent<Collider>());
        lens.transform.SetParent(root.transform, false);
        float diameter = Mathf.Min(scopeBounds.size.x, scopeBounds.size.y) * 0.85f;
        lens.transform.localScale = new Vector3(diameter, 0.001f, diameter);
        lens.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
        lens.transform.localPosition = new Vector3(scopeBounds.center.x, scopeBounds.center.y, scopeBounds.min.z - 0.002f);

        var scopeCamera = new GameObject("CamaraMira").transform;
        scopeCamera.SetParent(root.transform, false);
        scopeCamera.localPosition = new Vector3(scopeBounds.center.x, scopeBounds.center.y, scopeBounds.max.z + 0.05f);

        var muzzle = new GameObject("Boca").transform;
        muzzle.SetParent(root.transform, false);
        muzzle.localPosition = new Vector3(gun.center.x, gun.center.y + gun.size.y * 0.15f, gun.max.z);

        // Agarre: la empunadura suele estar a un tercio desde la culata
        var grip = new GameObject("Agarre").transform;
        grip.SetParent(root.transform, false);
        grip.localPosition = new Vector3(gun.center.x, gun.center.y, gun.min.z + RifleLength * 0.35f);

        var box = root.AddComponent<BoxCollider>();
        box.center = gun.center;
        box.size = gun.size;

        var body = root.AddComponent<Rigidbody>();
        body.mass = 6f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var grab = root.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = true;
        grab.attachTransform = grip;
        // La otra mano puede sujetar el canon para estabilizar
        grab.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple;

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;

        var weapon = root.AddComponent<Weapon>();
        weapon.data = data;
        weapon.muzzle = muzzle;
        weapon.tracer = AssetDatabase.LoadAssetAtPath<Material>(TracerPath);
        weapon.audioSource = audio;

        var sniperScope = root.AddComponent<SniperScope>();
        sniperScope.lens = lens.GetComponent<Renderer>();
        sniperScope.scopeCamera = scopeCamera;
        sniperScope.lensMaterial = EnsureMaterial(LensMaterialPath, "Unlit/Texture");
        sniperScope.reticleMaterial = EnsureMaterial(ReticleMaterialPath, "Sprites/Default");

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Copia del modelo sin colliders ni scripts, con el eje largo hacia +Z
    // (la boca es el extremo mas lejos del pivote) y escalado a 'length'
    static Transform AddVisual(string path, Transform parent, float length)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var visual = (GameObject)Object.Instantiate(source, parent);
        visual.name = source.name;
        foreach (var c in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var r in visual.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
        foreach (var m in visual.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(m);
        visual.transform.localPosition = Vector3.zero;

        var b = LocalBounds(parent, visual.transform);
        int axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : (b.size.z >= b.size.y ? 2 : 1);
        float sign = Mathf.Abs(b.max[axis]) >= Mathf.Abs(b.min[axis]) ? 1f : -1f;
        Quaternion turn = Quaternion.identity;
        if (axis == 0)
            turn = Quaternion.Euler(0f, sign > 0 ? -90f : 90f, 0f);
        else if (axis == 2 && sign < 0)
            turn = Quaternion.Euler(0f, 180f, 0f);
        else if (axis == 1)
            turn = Quaternion.Euler(sign > 0 ? 90f : -90f, 0f, 0f);
        visual.transform.localRotation = turn * visual.transform.localRotation;

        b = LocalBounds(parent, visual.transform);
        visual.transform.localScale *= length / Mathf.Max(0.0001f, b.size.z);
        b = LocalBounds(parent, visual.transform);
        visual.transform.localPosition -= new Vector3(b.center.x, b.center.y, b.center.z);
        return visual.transform;
    }

    static Bounds LocalBounds(Transform space, Transform target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        var bounds = new Bounds(space.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
        foreach (var r in renderers)
        {
            var wb = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? wb.min.x : wb.max.x, (i / 2) % 2 == 0 ? wb.min.y : wb.max.y, i / 4 == 0 ? wb.min.z : wb.max.z);
                bounds.Encapsulate(space.InverseTransformPoint(corner));
            }
        }
        return bounds;
    }

    static void Place(string scenePath, GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var container = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Gameplay (generado)");
        if (container == null || container.transform.Cast<Transform>().Any(t => t.name.StartsWith("Francotirador")))
            return;

        var weapon = Object.FindObjectsByType<Weapon>(FindObjectsSortMode.None).FirstOrDefault(w => w.GetComponent<SniperScope>() == null);
        if (weapon == null)
            return;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.SetParent(container.transform, true);
        instance.transform.position = weapon.transform.position + weapon.transform.right * 1.6f + Vector3.up * 0.1f;
        instance.transform.rotation = Quaternion.LookRotation(weapon.transform.forward, Vector3.up);
        EditorSceneManager.SaveScene(scene);
    }
}
