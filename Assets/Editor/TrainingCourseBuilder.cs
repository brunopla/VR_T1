using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Arma el entrenamiento guiado en la escena del tutorial (Prototipable):
// estaciones, mesas de armas, galeria de tiro, cuerpo a cuerpo, explosivos,
// gancho, nido de francotirador y portal. Se puede repetir: rehace lo generado.
// Batch: Unity -batchmode -quit -executeMethod TrainingCourseBuilder.Build
public static class TrainingCourseBuilder
{
    const string Scene = "Assets/Scenes/Prototipable.unity";
    const string Root = "Entrenamiento (generado)";
    const string MaterialFolder = "Assets/Data/Materials";
    const string GrapplePrefab = "Assets/Assets_Del_Juego/Prefab/Grappling 1.prefab";
    const string PolePrefab = "Assets/Assets_City Elias/Prefabs/Bass_Grappling.prefab";
    const string BarrelPrefab = "Assets/Prefabs/Gameplay/BarrilExplosivo.prefab";
    public static string PreviewFolder = "C:/Users/bplatoni/AppData/Local/Temp/claude/vrcheck/views";

    static StringBuilder log;
    static Transform root, stations;
    static Material wood, red, orange, tableMaterial;

    // Posiciones en el plano (x, z), todo dentro de la nave donde empieza el
    // jugador (paredes en x -5.9..45.6, z -42.2..-8.7). La altura sale de un raycast.
    static readonly Dictionary<string, Vector2> Layout = new Dictionary<string, Vector2>
    {
        { "Inicio", new Vector2(5f, -19f) },
        { "Armeria", new Vector2(8f, -15.2f) },
        { "Galeria", new Vector2(10f, -24f) },
        { "CuerpoACuerpo", new Vector2(22f, -30f) },
        { "Explosivos", new Vector2(33f, -30f) },
        { "Gancho", new Vector2(41f, -22f) },
        { "Nido", new Vector2(-2f, -12f) },
        { "Portal", new Vector2(-1f, -36f) },
    };

    [MenuItem("VR_T1/Armar entrenamiento guiado")]
    public static void Build()
    {
        log = new StringBuilder();
        var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        foreach (var old in scene.GetRootGameObjects().Where(g => g.name == Root))
            Object.DestroyImmediate(old);

        wood = EnsureMaterial("BlancoMadera", new Color(0.72f, 0.58f, 0.38f));
        red = EnsureMaterial("BlancoRojo", new Color(0.85f, 0.12f, 0.08f));
        orange = EnsureMaterial("BlancoNaranja", new Color(1f, 0.5f, 0.1f));
        tableMaterial = EnsureMaterial("MesaArmas", new Color(0.22f, 0.24f, 0.26f));

        root = new GameObject(Root).transform;
        stations = new GameObject("Estaciones").transform;
        stations.SetParent(root, false);
        foreach (var pair in Layout)
        {
            var s = new GameObject(pair.Key).transform;
            s.SetParent(stations, false);
            s.position = Ground(pair.Value);
            log.AppendLine("Estacion " + pair.Key + " -> " + s.position);
        }

        HideOldInstructions(scene);
        BuildArmory(scene);
        BuildRange();
        BuildMelee();
        BuildExplosives();
        BuildGrapple();
        BuildSniperNest();
        BuildPortal();
        LogWallOpenings();

        var course = root.gameObject.AddComponent<TrainingCourse>();
        course.stations = stations;
        course.steps = Steps();

        EditorSceneManager.SaveScene(scene);
        RenderPreviews();
        Debug.Log("[TrainingCourseBuilder] Entrenamiento armado\n" + log);
    }

    static List<TrainingCourse.Step> Steps()
    {
        TrainingCourse.Step S(string title, string detail, string buttons, string station, string eventId, int count = 1) =>
            new TrainingCourse.Step { title = title, detail = detail, buttons = buttons, station = station, eventId = eventId, count = count };
        return new List<TrainingCourse.Step>
        {
            S("Bienvenido, recluta", "Mira a tu alrededor girando la cabeza o con el stick derecho.", "STICK DER.", "", "mirar"),
            S("Muévete", "Usa el stick izquierdo para llegar al anillo naranja. Sigue la línea del piso.", "STICK IZQ.", "Inicio", "llegar"),
            S("Armería", "Acércate a la mesa y agarra un arma de fuego con el botón grip.", "GRIP", "Armeria", "agarrar"),
            S("Galería de tiro", "Derriba 3 blancos con el gatillo.", "GATILLO", "Galeria", "blanco:galeria", 3),
            S("Tiro a la cabeza", "Los tiros a la cabeza hacen el triple de daño. Dale en la cabeza al blanco rojo del fondo.", "GATILLO", "Galeria", "cabeza:cabeza"),
            S("Recarga", "Apunta el arma al suelo un momento para recargar el cargador.", "APUNTA ABAJO", "", "recarga"),
            S("Fundas", "Guarda el arma: suéltala junto a tu cadera (armas cortas) o sobre tu hombro derecho (largas).", "SOLTAR GRIP", "", "funda"),
            S("HUD de muñeca", "Gira la muñeca izquierda hacia tu cara. Toca A para elegir un arma y mantén A para traerla a tu mano.", "A|MANTENER A", "", "muneca"),
            S("Cuchillo", "Toma un cuchillo de la mesa y lánzalo al blanco. Si va rápido, se clava.", "GRIP|LANZAR", "CuerpoACuerpo", "golpe:cuchillo"),
            S("Martillo de Thor", "Lanza el martillo y luego presiona B o Y para que vuelva a tu mano.", "LANZAR|B / Y", "CuerpoACuerpo", "martillo_vuelve"),
            S("Explosivos", "Dispárale al barril rojo: la explosión derriba todo lo que tiene cerca.", "GATILLO", "Explosivos", "explosion"),
            S("Gancho", "Agarra el gancho, apunta al poste (aparece una mira verde) y mantén el gatillo. Con el stick recoges cuerda.", "GATILLO|STICK", "Gancho", "gancho"),
            S("Francotirador", "Toma el francotirador de la armería, acerca el ojo a la mira y derriba un blanco lejano desde el borde.", "GRIP|GATILLO", "Nido", "blanco:lejos"),
            S("Comodidad", "Botón menú izquierdo: un toque cambia el giro; mantenerlo 1 s activa el modo sentado.", "MENÚ", "", "comodidad"),
            S("¡A la ciudad!", "Cruza el portal. Sobrevive a las oleadas y llega al helicóptero para escapar.", "", "Portal", "llegar"),
        };
    }

    // ---------------------------------------------------------------- piezas

    // El jugador empieza dentro de un edificio (piso ~11 m, techo ~23 m) y la
    // escena tiene un plano de cielo mas arriba: se busca desde 14 m y solo
    // superficies caminables (el piso del edificio o el exterior a ~8 m)
    static Vector3 Ground(Vector2 xz)
    {
        var first = Physics.RaycastAll(new Vector3(xz.x, 14f, xz.y), Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.normal.y > 0.7f && !h.collider.transform.root.name.StartsWith("XR Origin")
                        && !h.collider.transform.root.name.StartsWith(Root))
            .OrderBy(h => h.distance).FirstOrDefault();
        return first.collider != null ? first.point : new Vector3(xz.x, 8.14f, xz.y);
    }

    static Material EnsureMaterial(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;
        material = new Material(Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider)
            Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Group(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        return go;
    }

    // Blanco con forma de zombie: tabla que cae hacia atras, cabeza con hitbox
    static TrainingTarget Target(Transform parent, string group, Vector2 xz, Vector3 faceTowards, Material headMaterial = null)
    {
        var basePoint = Ground(xz);
        var go = new GameObject("Blanco " + group);
        go.transform.SetParent(parent, false);
        go.transform.position = basePoint;
        var face = Vector3.ProjectOnPlane(faceTowards - basePoint, Vector3.up);
        if (face.sqrMagnitude > 0.01f)
            go.transform.rotation = Quaternion.LookRotation(face);

        Box("Base", go.transform, basePoint + Vector3.up * 0.05f, new Vector3(0.8f, 0.1f, 0.5f), tableMaterial);
        var board = new GameObject("Tabla").transform;
        board.SetParent(go.transform, false);
        board.localPosition = new Vector3(0f, 0.1f, 0f);
        var body = Box("Cuerpo", board, Vector3.zero, new Vector3(0.55f, 1.1f, 0.06f), wood);
        body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        body.transform.localRotation = Quaternion.identity;
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Cabeza";
        head.transform.SetParent(board, false);
        head.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        head.transform.localScale = Vector3.one * 0.32f;
        head.GetComponent<Renderer>().sharedMaterial = headMaterial != null ? headMaterial : wood;
        var hitbox = head.AddComponent<Hitbox>();
        hitbox.isHead = true;
        hitbox.damageMultiplier = 3f;

        var target = go.AddComponent<TrainingTarget>();
        target.group = group;
        target.board = board;
        return target;
    }

    static GameObject Table(Transform parent, Vector2 xz, float length)
    {
        var g = Ground(xz);
        return Box("Mesa", parent, g + Vector3.up * 0.45f, new Vector3(length, 0.9f, 0.8f), tableMaterial);
    }

    // Deja objetos de la escena sobre una mesa, acostados y espaciados
    static void LayOnTable(GameObject table, IList<Transform> items)
    {
        var top = table.transform.position + Vector3.up * 0.5f;
        float length = table.transform.localScale.x;
        for (int i = 0; i < items.Count; i++)
        {
            float x = -length * 0.5f + (i + 0.5f) * (length / items.Count);
            // Un hijo dentro de un prefab no puede cambiar de padre: solo se mueve
            var go = items[i].gameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(go) || PrefabUtility.IsOutermostPrefabInstanceRoot(go))
                items[i].SetParent(table.transform.parent, true);
            items[i].position = top + Vector3.right * x + Vector3.up * 0.15f;
            items[i].rotation = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Euler(0f, 0f, 90f);
            log.AppendLine("  sobre la mesa: " + items[i].name);
        }
    }

    static void HideOldInstructions(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            if (text.text.Contains("cruza este portal"))
                continue;
            if (text.text.Contains("presiona") || text.text.Contains("utiliza"))
            {
                text.gameObject.SetActive(false);
                log.AppendLine("Texto antiguo oculto: " + text.text.Replace("\n", " | "));
            }
        }
    }

    static void BuildArmory(UnityEngine.SceneManagement.Scene scene)
    {
        var group = Group("Armeria");
        var table = Table(group.transform, new Vector2(8f, -14f), 4.5f);
        // Armas sueltas de la escena (no las que van en el rig del jugador)
        var items = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None)
            .Where(g => !g.transform.root.name.StartsWith("XR Origin"))
            .Where(g => g.GetComponent<Weapon>() != null || g.GetComponent<ThorHammer>() != null || g.GetComponent<MeleeWeapon>() != null)
            .Select(g => g.transform).Distinct()
            .OrderBy(t => t.GetComponent<Weapon>() != null ? 0 : 1).ThenBy(t => t.name)
            .ToList();
        // El martillo y los cuchillos van a la mesa de cuerpo a cuerpo
        var melee = items.Where(t => t.GetComponent<Weapon>() == null).ToList();
        LayOnTable(table, items.Except(melee).ToList());
        meleeItems = melee;
    }

    static List<Transform> meleeItems = new List<Transform>();

    static void BuildRange()
    {
        var group = Group("Galeria de tiro");
        var shooter = stations.Find("Galeria").position;
        Box("Linea de tiro", group.transform, shooter + new Vector3(0f, 0.01f, -0.6f), new Vector3(8f, 0.02f, 0.1f), orange, false);
        float[] xs = { 4f, 7f, 10f, 13f, 16f };
        float[] zs = { -36f, -38.5f, -37f, -39f, -36.5f };
        for (int i = 0; i < xs.Length; i++)
            Target(group.transform, "galeria", new Vector2(xs[i], zs[i]), shooter);
        Target(group.transform, "cabeza", new Vector2(10f, -41f), shooter, red);
    }

    static void BuildMelee()
    {
        var group = Group("Cuerpo a cuerpo");
        var station = stations.Find("CuerpoACuerpo").position;
        var table = Table(group.transform, new Vector2(22f, -28.6f), 2.4f);
        LayOnTable(table, meleeItems);
        Target(group.transform, "cuchillo", new Vector2(22f, -34.5f), station, orange);
        Target(group.transform, "martillo", new Vector2(25.5f, -36f), station);
    }

    static void BuildExplosives()
    {
        var group = Group("Explosivos");
        var station = stations.Find("Explosivos").position;
        var barrel = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefab);
        if (barrel != null)
        {
            var b = (GameObject)PrefabUtility.InstantiatePrefab(barrel);
            b.transform.SetParent(group.transform, true);
            b.transform.position = Ground(new Vector2(33f, -38f)) + Vector3.up * 0.46f;
        }
        Target(group.transform, "explosivo", new Vector2(31.4f, -39.3f), station);
        Target(group.transform, "explosivo", new Vector2(34.6f, -39.3f), station);
    }

    static void BuildGrapple()
    {
        var group = Group("Gancho");
        var table = Table(group.transform, new Vector2(41f, -20.6f), 1.6f);
        var grapple = AssetDatabase.LoadAssetAtPath<GameObject>(GrapplePrefab);
        if (grapple != null)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(grapple);
            LayOnTable(table, new[] { g.transform });
        }
        var pole = AssetDatabase.LoadAssetAtPath<GameObject>(PolePrefab);
        if (pole != null)
        {
            var p = (GameObject)PrefabUtility.InstantiatePrefab(pole);
            p.transform.SetParent(group.transform, true);
            p.transform.position = Ground(new Vector2(42f, -32f));
        }
    }

    // Nido en el extremo oeste de la nave, disparando a lo largo hacia el este
    static void BuildSniperNest()
    {
        var group = Group("Nido de francotirador");
        var nest = stations.Find("Nido").position;
        Box("Saco", group.transform, nest + new Vector3(0.9f, 0.4f, 0f), new Vector3(0.5f, 0.8f, 2.2f), tableMaterial);
        Vector2[] far = { new Vector2(35f, -11f), new Vector2(39f, -14.5f), new Vector2(43f, -10.5f) };
        foreach (var xz in far)
        {
            var t = Target(group.transform, "lejos", xz, nest, orange);
            t.transform.localScale = Vector3.one * 1.6f;
            log.AppendLine("Blanco lejano a " + Vector3.Distance(nest, t.transform.position).ToString("F0") + " m");
        }
    }

    // Portal dentro de la nave: lleva a la ciudad (el original queda afuera)
    static void BuildPortal()
    {
        var group = Group("Portal a la ciudad");
        var basePoint = stations.Find("Portal").position + new Vector3(-2.5f, 0f, 0f);
        var frame = EnsureMaterial("PortalMarco", new Color(0.15f, 0.15f, 0.18f));
        Box("Columna izquierda", group.transform, basePoint + new Vector3(0f, 1.75f, -1.4f), new Vector3(0.4f, 3.5f, 0.4f), frame);
        Box("Columna derecha", group.transform, basePoint + new Vector3(0f, 1.75f, 1.4f), new Vector3(0.4f, 3.5f, 0.4f), frame);
        Box("Dintel", group.transform, basePoint + new Vector3(0f, 3.6f, 0f), new Vector3(0.4f, 0.4f, 3.2f), frame);

        var glow = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.45f, 0.1f, 0.55f) };
        var glowPath = MaterialFolder + "/PortalLuz.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(glowPath) == null)
            AssetDatabase.CreateAsset(glow, glowPath);
        glow = AssetDatabase.LoadAssetAtPath<Material>(glowPath);
        var surface = Box("Superficie", group.transform, basePoint + new Vector3(0f, 1.75f, 0f), new Vector3(0.1f, 3.4f, 2.4f), glow);
        surface.AddComponent<Port>();
        var label = new GameObject("Cartel").AddComponent<TextMeshPro>();
        label.transform.SetParent(group.transform, false);
        label.transform.position = basePoint + new Vector3(0.3f, 4.3f, 0f);
        label.transform.rotation = Quaternion.LookRotation(Vector3.left);
        label.text = "A LA CIUDAD";
        label.fontSize = 6f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.6f, 0.2f);
    }

    // Diagnostico: aberturas en las paredes de la nave (rayos a 1,5 m de altura)
    static void LogWallOpenings()
    {
        int openings = 0;
        for (float x = -4f; x <= 44f; x += 1f)
        {
            if (!Physics.Raycast(new Vector3(x, 9.6f, -12f), Vector3.forward, 6f)) { log.AppendLine("Abertura norte en x=" + x); openings++; }
            if (!Physics.Raycast(new Vector3(x, 9.6f, -39f), Vector3.back, 6f)) { log.AppendLine("Abertura sur en x=" + x); openings++; }
        }
        for (float z = -41f; z <= -10f; z += 1f)
        {
            if (!Physics.Raycast(new Vector3(-2f, 9.6f, z), Vector3.left, 6f)) { log.AppendLine("Abertura oeste en z=" + z); openings++; }
            if (!Physics.Raycast(new Vector3(42f, 9.6f, z), Vector3.right, 6f)) { log.AppendLine("Abertura este en z=" + z); openings++; }
        }
        log.AppendLine("Aberturas en las paredes: " + openings);
    }

    // ---------------------------------------------------------------- control

    static void RenderPreviews()
    {
        Directory.CreateDirectory(PreviewFolder);
        var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        var camObject = new GameObject("PreviewCam");
        var cam = camObject.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.farClipPlane = 400f;

        // Desde arriba, sin el techo ni el plano del cielo (solo para la foto)
        var hidden = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.bounds.min.y > 18f).ToList();
        hidden.ForEach(r => r.enabled = false);
        cam.orthographic = true;
        cam.orthographicSize = 20f;
        camObject.transform.position = new Vector3(20f, 60f, -25.5f);
        camObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Save(cam, 1000, 800, PreviewFolder + "/curso_top.png");
        hidden.ForEach(r => r.enabled = true);

        // Desde el punto de inicio, a la altura de los ojos, mirando adelante
        cam.orthographic = false;
        cam.fieldOfView = 80f;
        camObject.transform.position = Ground(new Vector2(origin.transform.position.x, origin.transform.position.z)) + Vector3.up * 1.6f;
        camObject.transform.rotation = Quaternion.LookRotation(new Vector3(1f, -0.15f, -0.25f));
        Save(cam, 1200, 700, PreviewFolder + "/curso_inicio.png");

        // Desde la linea de tiro hacia la galeria
        camObject.transform.position = stations.Find("Galeria").position + Vector3.up * 1.6f;
        camObject.transform.rotation = Quaternion.LookRotation(Vector3.back + Vector3.down * 0.05f);
        Save(cam, 1200, 700, PreviewFolder + "/curso_galeria.png");

        // Desde el nido hacia los blancos lejanos (con algo de zoom, como la mira)
        camObject.transform.position = stations.Find("Nido").position + Vector3.up * 1.4f;
        camObject.transform.rotation = Quaternion.LookRotation(new Vector3(1f, 0f, 0.02f));
        cam.fieldOfView = 25f;
        Save(cam, 1200, 700, PreviewFolder + "/curso_nido.png");

        // El portal
        cam.fieldOfView = 70f;
        camObject.transform.position = stations.Find("Portal").position + new Vector3(4f, 1.6f, 0f);
        camObject.transform.rotation = Quaternion.LookRotation(Vector3.left);
        Save(cam, 1200, 700, PreviewFolder + "/curso_portal.png");
        Object.DestroyImmediate(camObject);
    }

    static void Save(Camera cam, int w, int h, string path)
    {
        var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(rt);
    }
}
