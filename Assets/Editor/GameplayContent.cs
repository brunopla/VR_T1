using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Crea los prefabs de martillo de Thor, cuchillo lanzable y barril explosivo
// (con formas simples hasta tener modelos propios) y los coloca en las escenas.
// Se puede repetir: no duplica lo que ya existe.
// Batch: Unity -batchmode -quit -executeMethod GameplayContent.Run
public static class GameplayContent
{
    const string PrefabFolder = "Assets/Prefabs/Gameplay";
    const string MaterialFolder = "Assets/Data/Materials";
    const string KnifeModel = "Assets/Combat/Prefab/Mobile/Combat_LODA_Mobile.prefab";
    const string ExplosionEffect = "Assets/EffectTexturesAndPrefabs/Prefabs/Explosion_A.prefab";
    const string Tutorial = "Assets/Scenes/Prototipable.unity";
    const string Level = "Assets/Scenes/level_00.unity";
    const string PlacedRoot = "Gameplay (generado)";

    static StringBuilder log;

    [MenuItem("VR_T1/Crear martillo, cuchillos y barriles")]
    public static void Run()
    {
        log = new StringBuilder();
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        var explosion = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionEffect);
        var hammer = CreateHammer(explosion);
        var knife = CreateKnife();
        var barrel = CreateBarrel(explosion);

        PlaceInScene(Tutorial, hammer, knife, barrel, barrelsNearSpawns: false);
        PlaceInScene(Level, hammer, knife, barrel, barrelsNearSpawns: true);

        AssetDatabase.SaveAssets();
        Debug.Log("[GameplayContent] Terminado\n" + log);
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    static Material EnsureMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        material = new Material(Shader.Find("Standard")) { color = color };
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    static XRGrabInteractable SetupGrab(GameObject root, float mass, Transform attach)
    {
        var body = root.AddComponent<Rigidbody>();
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var grab = root.AddComponent<XRGrabInteractable>();
        // Seguir por velocidad da choques fisicos reales al golpear
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = true;
        grab.attachTransform = attach;
        return grab;
    }

    static GameObject SavePrefab(GameObject root, string name)
    {
        string path = PrefabFolder + "/" + name + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        log.AppendLine("Prefab: " + path);
        return prefab;
    }

    static GameObject CreateHammer(GameObject explosion)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/MartilloThor.prefab");
        if (existing != null)
            return existing;

        var metal = EnsureMaterial("MartilloMetal", new Color(0.55f, 0.57f, 0.6f), 0.85f, 0.6f);
        var leather = EnsureMaterial("MartilloMango", new Color(0.3f, 0.18f, 0.1f), 0f, 0.3f);

        var root = new GameObject("MartilloThor");
        Part(PrimitiveType.Cylinder, "Mango", root.transform, new Vector3(0, 0, 0), new Vector3(0.045f, 0.2f, 0.045f), leather);
        var head = Part(PrimitiveType.Cube, "Cabeza", root.transform, new Vector3(0, 0.25f, 0), new Vector3(0.3f, 0.16f, 0.16f), metal);
        var attach = new GameObject("Agarre").transform;
        attach.SetParent(root.transform, false);
        attach.localPosition = new Vector3(0, -0.08f, 0);

        SetupGrab(root, 3f, attach);
        var hammer = root.AddComponent<ThorHammer>();
        hammer.shockwaveEffect = explosion;
        var melee = root.AddComponent<MeleeWeapon>();
        melee.damage = 4f;
        melee.tip = head.transform;
        melee.hitRadius = 0.2f;
        return SavePrefab(root, "MartilloThor");
    }

    const float KnifeLength = 0.3f;

    static GameObject CreateKnife()
    {
        string path = PrefabFolder + "/CuchilloLanzable.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            // Corrige prefabs creados antes de ajustar la escala del modelo
            var contents = PrefabUtility.LoadPrefabContents(path);
            if (FitKnife(contents))
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                log.AppendLine("Escala del cuchillo corregida");
            }
            PrefabUtility.UnloadPrefabContents(contents);
            return existing;
        }

        var root = new GameObject("CuchilloLanzable");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(KnifeModel);
        if (model != null)
        {
            var visual = (GameObject)Object.Instantiate(model, root.transform);
            visual.name = "Modelo";
            foreach (var c in visual.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
        }
        else
        {
            var steel = EnsureMaterial("CuchilloHoja", new Color(0.8f, 0.82f, 0.85f), 0.9f, 0.8f);
            Part(PrimitiveType.Cube, "Modelo", root.transform, new Vector3(0, 0.1f, 0), new Vector3(0.03f, 0.18f, 0.005f), steel);
            Object.DestroyImmediate(root.transform.Find("Modelo").GetComponent<Collider>());
        }

        root.AddComponent<BoxCollider>();
        new GameObject("ZonaDeGolpe").transform.SetParent(root.transform, false);
        SetupGrab(root, 0.4f, null);
        var melee = root.AddComponent<MeleeWeapon>();
        melee.damage = 3f;
        melee.stickOnThrow = true;
        FitKnife(root);
        return SavePrefab(root, "CuchilloLanzable");
    }

    // Escala el modelo a ~30 cm y ajusta el collider y la zona de golpe.
    // Devuelve false si ya estaba bien.
    static bool FitKnife(GameObject root)
    {
        var visual = root.transform.Find("Modelo");
        var box = root.GetComponent<BoxCollider>();
        var melee = root.GetComponent<MeleeWeapon>();
        var tip = root.transform.Find("ZonaDeGolpe");
        if (visual == null || box == null || melee == null || tip == null)
            return false;

        var bounds = LocalBounds(root.transform);
        float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (Mathf.Abs(longest - KnifeLength) < 0.01f)
            return false;

        visual.localScale *= KnifeLength / longest;
        bounds = LocalBounds(root.transform);
        box.center = bounds.center;
        box.size = bounds.size;
        // La zona de golpe cubre el objeto entero: no se sabe hacia donde apunta la hoja
        tip.localPosition = bounds.center;
        melee.tip = tip;
        melee.hitRadius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 0.8f;
        return true;
    }

    static GameObject CreateBarrel(GameObject explosion)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/BarrilExplosivo.prefab");
        if (existing != null)
            return existing;

        var red = EnsureMaterial("BarrilRojo", new Color(0.75f, 0.1f, 0.05f), 0.4f, 0.45f);
        var band = EnsureMaterial("BarrilFranja", new Color(0.95f, 0.8f, 0.1f), 0.2f, 0.4f);

        var root = Part(PrimitiveType.Cylinder, "BarrilExplosivo", null, Vector3.zero, new Vector3(0.6f, 0.45f, 0.6f), red);
        // Franja amarilla de aviso
        var stripe = Part(PrimitiveType.Cylinder, "Franja", root.transform, new Vector3(0, 0.3f, 0), new Vector3(1.02f, 0.12f, 1.02f), band);
        Object.DestroyImmediate(stripe.GetComponent<Collider>());

        var body = root.AddComponent<Rigidbody>();
        body.mass = 30f;
        var explosive = root.AddComponent<ExplosiveBarrel>();
        explosive.explosionEffect = explosion;
        return SavePrefab(root, "BarrilExplosivo");
    }

    static Bounds LocalBounds(Transform root)
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            var b = r.bounds;
            var local = new Bounds(root.InverseTransformPoint(b.center), Vector3.zero);
            local.Encapsulate(root.InverseTransformPoint(b.min));
            local.Encapsulate(root.InverseTransformPoint(b.max));
            if (any) bounds.Encapsulate(local); else bounds = local;
            any = true;
        }
        return bounds;
    }

    static void PlaceInScene(string scenePath, GameObject hammer, GameObject knife, GameObject barrel, bool barrelsNearSpawns)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == PlacedRoot)
            {
                log.AppendLine("Ya colocado en " + scenePath);
                return;
            }
        }

        var container = new GameObject(PlacedRoot);
        SceneManager_MoveToScene(container, scene);

        // Junto a las armas que ya hay en la escena
        var weapon = Object.FindFirstObjectByType<Weapon>();
        if (weapon != null)
        {
            Vector3 basePosition = weapon.transform.position + Vector3.up * 0.1f;
            Vector3 side = weapon.transform.right;
            Instantiate(hammer, container, basePosition + side * 0.5f);
            Instantiate(knife, container, basePosition + side * 0.8f);
            Instantiate(knife, container, basePosition + side * 1.0f);
            Instantiate(knife, container, basePosition + side * 1.2f);

            if (!barrelsNearSpawns)
                Instantiate(barrel, container, OnGround(basePosition + weapon.transform.forward * 5f));
        }
        else
        {
            log.AppendLine("AVISO: no hay armas en " + scenePath + " para colocar el martillo y los cuchillos");
        }

        int barrels = 0;
        if (barrelsNearSpawns)
        {
            // Dos barriles junto a cada punto de aparicion: los zombies pasan al lado
            foreach (var point in Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None))
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector3 offset = Quaternion.Euler(0, 60f + i * 50f, 0) * Vector3.forward * 2.5f;
                    if (NavMesh.SamplePosition(point.transform.position + offset, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    {
                        Instantiate(barrel, container, hit.position + Vector3.up * 0.46f);
                        barrels++;
                    }
                }
            }
        }

        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Colocado en " + scenePath + ": martillo, 3 cuchillos y " + (barrelsNearSpawns ? barrels : 1) + " barriles");
    }

    static Vector3 OnGround(Vector3 position)
    {
        if (Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.46f;
        return position;
    }

    static void Instantiate(GameObject prefab, GameObject parent, Vector3 position)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.scene);
        instance.transform.SetParent(parent.transform, true);
        instance.transform.position = position;
    }

    static void SceneManager_MoveToScene(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }
}
