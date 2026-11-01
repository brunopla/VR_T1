using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

// Pasa los prefabs y escenas al sistema nuevo de armas y zombies.
// Se puede ejecutar varias veces: lo ya convertido se salta.
// Batch: Unity -batchmode -quit -executeMethod VRT1Migration.RunAll
public static class VRT1Migration
{
    const string DataFolder = "Assets/Data";
    const string WeaponFolder = "Assets/Data/Weapons";
    const int HeadLayer = 11;
    static readonly string[] Scenes = { "Assets/Scenes/Prototipable.unity", "Assets/Scenes/level_00.unity" };

    static StringBuilder log;
    static WeaponData uzi, shotgun, rifle;
    static Material tracer;

    [MenuItem("VR_T1/Migrar armas y zombies")]
    public static void RunAll()
    {
        log = new StringBuilder();
        CreateAssets();

        string[] markers =
        {
            GuidOf<Bspawn>(), GuidOf<Bullet_Spawn>(), GuidOf<ZombieController>()
        };

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string text = File.ReadAllText(path);
            if (Array.TrueForAll(markers, m => !text.Contains(m)))
                continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = ConvertWeapons(root, path) | AddHeadHitboxes(root, path);
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        foreach (var scenePath in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                changed |= ConvertWeapons(root, scenePath);
                changed |= CleanSceneWeaponEvents(root, scenePath);
            }
            if (scenePath.EndsWith("Prototipable.unity"))
                changed |= UpdateTutorialTexts(scene.GetRootGameObjects());
            if (changed)
                EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[VRT1Migration] Terminado\n" + log);
    }

    static string GuidOf<T>() where T : MonoBehaviour
    {
        var go = new GameObject();
        try
        {
            var script = MonoScript.FromMonoBehaviour(go.AddComponent<T>());
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    static void CreateAssets()
    {
        EnsureFolder(DataFolder);
        EnsureFolder(WeaponFolder);

        string tracerPath = DataFolder + "/TracerMaterial.mat";
        tracer = AssetDatabase.LoadAssetAtPath<Material>(tracerPath);
        if (tracer == null)
        {
            tracer = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(tracer, tracerPath);
        }

        uzi = EnsureData("Uzi", d =>
        {
            d.automatic = true; d.fireRate = 12f; d.damage = 1f; d.spreadAngle = 1.5f;
            d.magazineSize = 32; d.range = 60f; d.hapticAmplitude = 0.35f; d.hapticDuration = 0.04f;
        });
        shotgun = EnsureData("Escopeta", d =>
        {
            d.pellets = 8; d.spreadAngle = 6f; d.damage = 1f; d.fireRate = 1.2f;
            d.magazineSize = 6; d.range = 30f; d.hapticAmplitude = 0.9f; d.hapticDuration = 0.15f;
        });
        rifle = EnsureData("M4", d =>
        {
            d.automatic = true; d.fireRate = 9f; d.damage = 1.5f; d.spreadAngle = 0.5f;
            d.magazineSize = 30; d.range = 100f; d.hapticAmplitude = 0.5f; d.hapticDuration = 0.05f;
        });
    }

    static WeaponData EnsureData(string name, Action<WeaponData> init)
    {
        string path = WeaponFolder + "/" + name + ".asset";
        var data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
        if (data != null)
            return data;

        data = ScriptableObject.CreateInstance<WeaponData>();
        data.displayName = name;
        init(data);
        AssetDatabase.CreateAsset(data, path);
        log.AppendLine("Creado " + path);
        return data;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    static bool IsRifle(Component c)
    {
        string name = c.transform.root.name.ToLowerInvariant();
        return name.Contains("m4") || name.Contains("rifle");
    }

    static bool ConvertWeapons(GameObject root, string context)
    {
        bool changed = false;
        foreach (var old in root.GetComponentsInChildren<Bspawn>(true))
        {
            if (PrefabUtility.IsPartOfPrefabInstance(old))
                continue;
            var data = IsRifle(old) ? rifle : shotgun;
            changed |= Convert(old, data, old.bulletOrigin, old.muzzlePrefab, old.GunShotClip, context);
        }
        foreach (var old in root.GetComponentsInChildren<Bullet_Spawn>(true))
        {
            if (PrefabUtility.IsPartOfPrefabInstance(old))
                continue;
            changed |= Convert(old, uzi, old.bulletOrigin, old.muzzlePrefab, old.uzi, context);
        }
        return changed;
    }

    static bool Convert(MonoBehaviour old, WeaponData data, GameObject origin, GameObject flash, AudioClip clip, string context)
    {
        var grab = old.GetComponentInParent<XRGrabInteractable>(true);
        if (grab == null)
        {
            log.AppendLine("AVISO sin XRGrabInteractable: " + context + " / " + old.name);
            return false;
        }

        var weapon = grab.GetComponent<Weapon>();
        if (weapon == null)
            weapon = grab.gameObject.AddComponent<Weapon>();

        weapon.data = data;
        weapon.muzzle = origin != null ? origin.transform : old.transform;
        weapon.muzzleFlashPrefab = flash;
        weapon.tracer = tracer;
        // GetComponent devuelve un "null falso" en el editor: no usar ??
        var audio = old.GetComponent<AudioSource>();
        weapon.audioSource = audio != null ? audio : grab.GetComponent<AudioSource>();

        if (data.shotClip == null && clip != null)
        {
            data.shotClip = clip;
            EditorUtility.SetDirty(data);
        }

        RemoveListeners(grab, old);
        Object.DestroyImmediate(old);
        log.AppendLine("Arma " + data.displayName + ": " + context + " / " + grab.name);
        return true;
    }

    // Quita llamadas al componente viejo, a Selectgun/DeselectGun y a objetivos perdidos.
    // El Weapon se conecta solo al XRGrabInteractable en tiempo de ejecucion.
    static bool RemoveListeners(XRGrabInteractable grab, Object old)
    {
        bool changed = false;
        UnityEventBase[] events = { grab.activated, grab.deactivated, grab.selectEntered, grab.selectExited };
        foreach (var e in events)
        {
            for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                var target = e.GetPersistentTarget(i);
                string method = e.GetPersistentMethodName(i);
                bool legacy = target == null || target == old || target is Bspawn || target is Bullet_Spawn
                              || method == "Selectgun" || method == "DeselectGun";
                if (!legacy)
                    continue;
                UnityEventTools.RemovePersistentListener(e, i);
                changed = true;
            }
        }
        return changed;
    }

    static bool CleanSceneWeaponEvents(GameObject root, string context)
    {
        bool changed = false;
        foreach (var weapon in root.GetComponentsInChildren<Weapon>(true))
        {
            var grab = weapon.GetComponent<XRGrabInteractable>();
            if (grab == null || !RemoveListeners(grab, null))
                continue;
            if (PrefabUtility.IsPartOfPrefabInstance(grab))
                PrefabUtility.RecordPrefabInstancePropertyModifications(grab);
            log.AppendLine("Eventos limpiados: " + context + " / " + grab.name);
            changed = true;
        }
        return changed;
    }

    static bool AddHeadHitboxes(GameObject root, string context)
    {
        bool changed = false;
        foreach (var zombie in root.GetComponentsInChildren<ZombieController>(true))
        {
            if (PrefabUtility.IsPartOfPrefabInstance(zombie) || zombie.GetComponentInChildren<Hitbox>(true) != null)
                continue;

            var head = FindHead(zombie);
            if (head == null)
            {
                log.AppendLine("AVISO sin hueso de cabeza: " + context);
                continue;
            }

            var go = new GameObject("HeadHitbox");
            go.layer = HeadLayer;
            go.transform.SetParent(head, false);
            float scale = Mathf.Max(0.0001f, head.lossyScale.x);
            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.13f / scale;
            sphere.center = new Vector3(0f, 0.09f / scale, 0f);
            var hitbox = go.AddComponent<Hitbox>();
            hitbox.isHead = true;
            hitbox.damageMultiplier = 3f;
            log.AppendLine("Cabeza: " + context + " -> " + head.name);
            changed = true;
        }
        return changed;
    }

    static Transform FindHead(ZombieController zombie)
    {
        var animator = zombie.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            var bone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (bone != null)
                return bone;
        }

        foreach (var t in zombie.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n.EndsWith("head") || n.EndsWith(":head"))
                return t;
        }
        return null;
    }

    static bool UpdateTutorialTexts(GameObject[] roots)
    {
        bool changed = false;
        foreach (var root in roots)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                string extra = null;
                if (text.text.Contains("trigger para disparar") && !text.text.Contains("recargar"))
                    extra = "\napunta al suelo para recargar";
                else if (text.text.Contains("stick izquierdo") && !text.text.Contains("menú"))
                    extra = "\nbotón menú izquierdo: opciones de comodidad";
                if (extra == null)
                    continue;

                Undo.RecordObject(text, "Tutorial");
                text.text += extra;
                if (PrefabUtility.IsPartOfPrefabInstance(text))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                log.AppendLine("Tutorial: " + text.text.Replace("\n", " | "));
                changed = true;
            }
        }
        return changed;
    }
}
