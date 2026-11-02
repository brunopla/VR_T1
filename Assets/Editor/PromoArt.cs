using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.TextCore.LowLevel;

// Arte promocional renderizado desde level_00: portadas para la tienda de Meta,
// icono e imagen de carga en VR. Nada se guarda en la escena: solo se abre,
// se monta la toma (atardecer, niebla, zombies, explosion) y se renderiza.
// Batch: Unity -batchmode -quit -executeMethod PromoArt.Candidates | PromoArt.Final
public static class PromoArt
{
    const string Level = "Assets/Scenes/level_00.unity";
    const string OutFolder = "Assets/Art/Promo";
    const string WorkFolder = "Temp/Promo";
    const string ChoiceFile = "Temp/Promo/choice.txt";
    // Fuente del titulo: se copia aqui solo para renderizar y luego se borra
    const string TitleFontPath = "Assets/Art/Promo/_TitleFont.ttf";
    const float Spacing = 37.96f;

    static readonly Color Sky = new Color(0.42f, 0.13f, 0.07f);
    static readonly Color Fog = new Color(0.36f, 0.12f, 0.07f);

    struct Shot
    {
        public Vector3 position;
        public Vector3 target;
        public Vector3 street;
    }

    static readonly List<Object> temporary = new List<Object>();
    static Light sun;
    static GameObject helicopter;
    static List<GameObject> zombiePrefabs;
    static AnimationClip[] poses;
    static GameObject explosionPrefab;
    static GameObject barrelPrefab;

    // ---------------------------------------------------------------- entradas

    // Varias tomas pequenas para elegir la mejor
    public static void Candidates()
    {
        var shots = Prepare();
        Directory.CreateDirectory(WorkFolder);
        var lines = new List<string>();
        for (int k = 0; k < shots.Count; k++)
        {
            var cam = SetupShot(shots[k], k);
            Save(Render(cam, 800, 450), WorkFolder + "/cand_" + k + ".png");
            lines.Add(k + ": " + shots[k].position + " -> " + shots[k].target);
        }
        Debug.Log("[PromoArt] Candidatas\n" + string.Join("\n", lines));
    }

    // Portadas, icono y splash con la toma elegida (indice en Temp/Promo/choice.txt)
    public static void Final()
    {
        var shots = Prepare();
        int choice = File.Exists(ChoiceFile) ? int.Parse(File.ReadAllText(ChoiceFile).Trim()) : 0;
        var shot = shots[Mathf.Clamp(choice, 0, shots.Count - 1)];
        if (!AssetDatabase.IsValidFolder("Assets/Art"))
            AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder(OutFolder))
            AssetDatabase.CreateFolder("Assets/Art", "Promo");

        var font = CreateTitleFont();

        var cam = SetupShot(shot, choice);
        RenderWithTitle(cam, font, 2560, 1440, OutFolder + "/Portada_2560x1440.png", 0.78f, 0.16f);
        RenderWithTitle(cam, font, 3000, 900, OutFolder + "/Hero_3000x900.png", 0.72f, 0.2f);
        RenderWithTitle(cam, font, 1440, 1440, OutFolder + "/Portada_1440x1440.png", 0.8f, 0.12f);

        var iconCam = SetupIconShot(shot);
        RenderIcon(iconCam, font, 1024, OutFolder + "/Icono_1024.png");
        RenderIcon(iconCam, font, 512, OutFolder + "/Icono_512.png");

        AssetDatabase.Refresh();
        ApplyToPlayerSettings();
        Debug.Log("[PromoArt] Arte final en " + OutFolder);
    }

    // ---------------------------------------------------------------- escena

    static List<Shot> Prepare()
    {
        var scene = EditorSceneManager.OpenScene(Level, OpenSceneMode.Single);

        // Fuera lo que no debe salir en la foto
        foreach (var name in new[] { "XR Origin", "EnemyPool", "Gameplay (generado)" })
            scene.GetRootGameObjects().FirstOrDefault(g => g.name == name)?.SetActive(false);
        helicopter = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Helicoptero");

        // Atardecer apocaliptico: sin lightmaps de dia, niebla roja y sol bajo
        LightmapSettings.lightmaps = new LightmapData[0];
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Fog;
        RenderSettings.fogStartDistance = 6f;
        RenderSettings.fogEndDistance = 130f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.32f, 0.14f, 0.12f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.09f, 0.07f);
        RenderSettings.ambientGroundColor = new Color(0.06f, 0.04f, 0.04f);
        sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
        sun.color = new Color(1f, 0.5f, 0.25f);
        sun.intensity = 1.6f;
        sun.shadows = LightShadows.Soft;

        zombiePrefabs = new[] { "Z_Girl", "Z_Soldier", "Z_prisioner", "z_scoutGirl" }
            .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets Bruno/Zombi/ZombiePrefabs/" + n + ".prefab"))
            .Concat(new[] { "Z_Clown", "z_Boss" }.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_LVL_design/asts/Zombi/ZombiePrefabs/" + n + ".prefab")))
            .Where(p => p != null).ToList();
        poses = new[] { "zombie walk", "zombie scream", "zombie run", "zombie walk" }
            .Select(n => AssetDatabase.LoadAllAssetsAtPath("Assets/Assets Bruno/Zombi/WomanZ/" + n + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview")))
            .Where(c => c != null).ToArray();
        explosionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EffectTexturesAndPrefabs/Prefabs/Explosion_A.prefab");
        barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/BarrilExplosivo.prefab");

        return FindShots(scene);
    }

    // Calles rectas de la ciudad original con edificios a ambos lados
    static List<Shot> FindShots(UnityEngine.SceneManagement.Scene scene)
    {
        var city = scene.GetRootGameObjects().First(g => g.name == "City_Prefab");
        var crossings = city.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.name.StartsWith("general_street_crossing")).Select(r => r.bounds.center).ToList();
        var dirs = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        var scored = new List<(Shot shot, float score)>();

        foreach (var a in crossings)
        {
            foreach (var dir in dirs)
            {
                Vector3 b = a + dir * Spacing * 2f;
                if (!crossings.Any(c => (c - b).sqrMagnitude < 9f))
                    continue;
                var shot = new Shot
                {
                    position = a - dir * 5f + Vector3.up * 1.1f,
                    target = b + Vector3.up * 7f,
                    street = dir
                };
                // Calle despejada al frente y edificios a los lados
                if (Physics.Raycast(shot.position, dir, 25f))
                    continue;
                var side = Vector3.Cross(Vector3.up, dir);
                float score = 0f;
                for (int k = 1; k <= 6; k++)
                {
                    var p = a + dir * (k * 10f) + Vector3.up * 6f;
                    if (Physics.Raycast(p, side, 16f)) score += 1f;
                    if (Physics.Raycast(p, -side, 16f)) score += 1f;
                    if (Physics.Raycast(p + Vector3.up * 14f, side, 16f)) score += 0.5f;
                    if (Physics.Raycast(p + Vector3.up * 14f, -side, 16f)) score += 0.5f;
                }
                scored.Add((shot, score));
            }
        }

        // Las mejores, sin repetir el mismo cruce
        var result = new List<Shot>();
        foreach (var (shot, _) in scored.OrderByDescending(s => s.score))
        {
            if (result.Any(r => (r.position - shot.position).sqrMagnitude < 100f))
                continue;
            result.Add(shot);
            if (result.Count == 6)
                break;
        }
        return result;
    }

    static void ClearTemporary()
    {
        foreach (var o in temporary)
            if (o != null)
                Object.DestroyImmediate(o);
        temporary.Clear();
    }

    static T Temp<T>(T o) where T : Object
    {
        temporary.Add(o);
        return o;
    }

    static Camera SetupShot(Shot shot, int seed)
    {
        ClearTemporary();
        var random = new System.Random(seed * 7919 + 13);
        float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        var cam = CreateCamera(shot.position, shot.target, 52f);

        // Contraluz: el sol se pone al fondo de la calle
        sun.transform.rotation = Quaternion.LookRotation(-shot.street + Vector3.down * 0.16f);

        // Zombies avanzando hacia la camara
        var side = Vector3.Cross(Vector3.up, shot.street);
        float[] distances = { 5.5f, 7.5f, 9f, 11f, 13.5f, 16f, 19f, 23f };
        for (int i = 0; i < distances.Length; i++)
        {
            var p = shot.position + shot.street * distances[i] + side * Range(-3.8f, 3.8f);
            PlaceZombie(zombiePrefabs[i % zombiePrefabs.Count], Ground(p), shot.position, poses[i % poses.Length], Range(0f, 1f), Range(-25f, 25f));
        }

        // Explosion al fondo y barriles a los lados en primer plano
        if (explosionPrefab != null)
        {
            var boom = Temp((GameObject)Object.Instantiate(explosionPrefab, Ground(shot.position + shot.street * 30f + side * 4f), Quaternion.identity));
            boom.transform.localScale *= 3f;
            foreach (var ps in boom.GetComponentsInChildren<ParticleSystem>())
                ps.Simulate(0.35f, true, true);
        }
        if (barrelPrefab != null)
        {
            Temp(Object.Instantiate(barrelPrefab, Ground(shot.position + shot.street * 3.5f + side * 3.2f) + Vector3.up * 0.46f, Quaternion.Euler(0, 20, 0)));
            Temp(Object.Instantiate(barrelPrefab, Ground(shot.position + shot.street * 4.2f - side * 3.6f) + Vector3.up * 0.46f, Quaternion.Euler(0, -35, 0)));
        }

        // El helicoptero de escape en el cielo
        if (helicopter != null)
        {
            helicopter.transform.position = shot.position + shot.street * 45f + Vector3.up * 26f - side * 10f;
            helicopter.transform.rotation = Quaternion.LookRotation(side, Vector3.up);
        }
        return cam;
    }

    static Camera SetupIconShot(Shot shot)
    {
        ClearTemporary();
        var cam = CreateCamera(shot.position, shot.position + shot.street * 10f + Vector3.up * 0.4f, 34f);
        sun.transform.rotation = Quaternion.LookRotation(-shot.street + Vector3.down * 0.25f);
        var head = shot.position + shot.street * 1.9f;
        PlaceZombie(zombiePrefabs[0], Ground(head), shot.position, poses.Length > 1 ? poses[1] : poses[0], 0.45f, 0f);
        var side = Vector3.Cross(Vector3.up, shot.street);
        PlaceZombie(zombiePrefabs[1 % zombiePrefabs.Count], Ground(head + shot.street * 4f + side * 1.6f), shot.position, poses[0], 0.2f, 10f);
        PlaceZombie(zombiePrefabs[2 % zombiePrefabs.Count], Ground(head + shot.street * 6f - side * 2f), shot.position, poses[0], 0.7f, -15f);
        if (helicopter != null)
            helicopter.transform.position = shot.position + Vector3.down * 200f;
        return cam;
    }

    static Camera CreateCamera(Vector3 position, Vector3 target, float fov)
    {
        var go = Temp(new GameObject("PromoCam"));
        go.transform.position = position;
        go.transform.LookAt(target);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 400f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Sky;
        cam.allowMSAA = true;
        cam.allowHDR = true;
        AddPostProcessing(go);
        return cam;
    }

    static void AddPostProcessing(GameObject cameraObject)
    {
        var resources = AssetDatabase.FindAssets("t:PostProcessResources")
            .Select(g => AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        if (resources == null)
            return;

        var layer = cameraObject.AddComponent<PostProcessLayer>();
        layer.Init(resources);
        layer.volumeLayer = 1 << 31;
        layer.volumeTrigger = cameraObject.transform;
        layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;

        var profile = Temp(ScriptableObject.CreateInstance<PostProcessProfile>());
        var bloom = profile.AddSettings<Bloom>();
        bloom.intensity.Override(6f);
        bloom.threshold.Override(0.9f);
        bloom.softKnee.Override(0.6f);
        bloom.color.Override(new Color(1f, 0.6f, 0.35f));
        var vignette = profile.AddSettings<Vignette>();
        vignette.intensity.Override(0.48f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0.08f, 0f, 0f));
        var grading = profile.AddSettings<ColorGrading>();
        grading.tonemapper.Override(Tonemapper.ACES);
        grading.contrast.Override(28f);
        grading.saturation.Override(12f);
        grading.temperature.Override(18f);
        grading.postExposure.Override(0.45f);
        var grain = profile.AddSettings<Grain>();
        grain.intensity.Override(0.25f);
        grain.size.Override(0.8f);
        var aberration = profile.AddSettings<ChromaticAberration>();
        aberration.intensity.Override(0.12f);

        var volumeObject = Temp(new GameObject("PromoVolume") { layer = 31 });
        var volume = volumeObject.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = profile;
    }

    static void PlaceZombie(GameObject prefab, Vector3 position, Vector3 lookAt, AnimationClip clip, float phase, float yawNoise)
    {
        var zombie = Temp((GameObject)PrefabUtility.InstantiatePrefab(prefab));
        zombie.transform.position = position;
        var toCamera = Vector3.ProjectOnPlane(lookAt - position, Vector3.up);
        zombie.transform.rotation = Quaternion.LookRotation(toCamera) * Quaternion.Euler(0f, yawNoise, 0f);
        foreach (var behaviour in zombie.GetComponentsInChildren<MonoBehaviour>())
            behaviour.enabled = false;

        // Pose de la animacion (sin esto quedan en pose de T)
        var animator = zombie.GetComponentInChildren<Animator>();
        if (animator == null || clip == null)
            return;
        animator.applyRootMotion = false;
        var graph = PlayableGraph.Create("PromoPose");
        var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
        var playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(true);
        output.SetSourcePlayable(playable);
        playable.SetTime(phase * clip.length);
        graph.Evaluate();
        graph.Destroy();
        zombie.transform.position = position;
    }

    static Vector3 Ground(Vector3 p)
    {
        return Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point : new Vector3(p.x, 0f, p.z);
    }

    // ---------------------------------------------------------------- render

    static Texture2D Render(Camera cam, int width, int height)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        cam.targetTexture = rt;
        // El lienzo del titulo toma el tamano de la textura recien asignada
        Canvas.ForceUpdateCanvases();
        cam.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        return tex;
    }

    static void Save(Texture2D tex, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static TMP_FontAsset CreateTitleFont()
    {
        if (!File.Exists(TitleFontPath))
        {
            File.Copy(@"C:\Windows\Fonts\impact.ttf", TitleFontPath);
            AssetDatabase.ImportAsset(TitleFontPath);
        }
        var font = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
        var asset = font != null
            ? TMP_FontAsset.CreateFontAsset(font, 120, 12, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic)
            : TMP_Settings.defaultFontAsset;
        return Temp(asset);
    }

    static Canvas CreateOverlay(Camera cam)
    {
        var go = Temp(new GameObject("PromoOverlay", typeof(RectTransform)));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 0.3f;
        return canvas;
    }

    static TextMeshProUGUI AddText(Canvas canvas, TMP_FontAsset font, string text, float size, float anchorY, float height)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, anchorY - height * 0.5f);
        rect.anchorMax = new Vector2(1f, anchorY + height * 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    // Brillo detras y borde oscuro para que el texto destaque sobre la escena
    static void StyleTitle(TextMeshProUGUI tmp, float outline, Color glow)
    {
        var material = Temp(new Material(tmp.fontSharedMaterial));
        material.EnableKeyword("OUTLINE_ON");
        material.EnableKeyword("UNDERLAY_ON");
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.12f, 0f, 0f));
        material.SetColor(ShaderUtilities.ID_UnderlayColor, glow);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.9f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.6f);
        tmp.fontSharedMaterial = material;
    }

    static void RenderWithTitle(Camera cam, TMP_FontAsset font, int width, int height, string path, float titleY, float titleSize)
    {
        cam.aspect = (float)width / height;
        var canvas = CreateOverlay(cam);
        var title = AddText(canvas, font, "DEVA<color=#FF2A12>Z</color>TATION", height * titleSize, titleY, titleSize * 1.4f);
        title.characterSpacing = 6f;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(new Color(1f, 0.96f, 0.88f), new Color(1f, 0.96f, 0.88f), new Color(1f, 0.6f, 0.25f), new Color(1f, 0.6f, 0.25f));
        StyleTitle(title, 0.18f, new Color(0f, 0f, 0f, 0.85f));
        var subtitle = AddText(canvas, font, "SOBREVIVE  ·  ESCAPA", height * titleSize * 0.24f, titleY - titleSize * 0.78f, titleSize * 0.4f);
        subtitle.characterSpacing = 40f;
        subtitle.color = new Color(1f, 0.82f, 0.62f);
        StyleTitle(subtitle, 0.1f, new Color(0f, 0f, 0f, 0.8f));
        Canvas.ForceUpdateCanvases();
        Save(Render(cam, width, height), path);
        Object.DestroyImmediate(canvas.gameObject);
    }

    static void RenderIcon(Camera cam, TMP_FontAsset font, int size, string path)
    {
        cam.aspect = 1f;
        var canvas = CreateOverlay(cam);
        var mark = AddText(canvas, font, "Z", size * 0.55f, 0.24f, 0.5f);
        mark.alignment = TextAlignmentOptions.Right;
        mark.margin = new Vector4(0f, 0f, size * 0.07f, 0f);
        mark.color = new Color(1f, 0.16f, 0.07f);
        StyleTitle(mark, 0.22f, new Color(0f, 0f, 0f, 0.9f));
        Canvas.ForceUpdateCanvases();
        Save(Render(cam, size, size), path);
        Object.DestroyImmediate(canvas.gameObject);
    }

    // ---------------------------------------------------------------- ajustes

    static void ApplyToPlayerSettings()
    {
        foreach (var path in Directory.GetFiles(OutFolder, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(OutFolder + "/Icono_1024.png");
        if (icon != null)
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

        // Imagen que se ve en el visor mientras carga el juego
        var splash = AssetDatabase.LoadAssetAtPath<Texture2D>(OutFolder + "/Portada_1440x1440.png");
        if (splash != null)
            PlayerSettings.virtualRealitySplashScreen = splash;

        AssetDatabase.DeleteAsset(TitleFontPath);
        AssetDatabase.SaveAssets();
    }
}
