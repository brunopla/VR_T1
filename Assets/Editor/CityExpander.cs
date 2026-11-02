using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Amplia level_00: la ciudad original queda al centro y se generan barrios
// alrededor (3 x 3 veces el area original) con las mismas piezas: cruces y
// calles cada 38 m, edificios, faroles, autos, barriles, postes para el gancho
// y puntos de aparicion. Los barrios son CityDistrict: se apagan de lejos.
// Batch: Unity -batchmode -quit -executeMethod CityExpander.Run
public static class CityExpander
{
    const string Level = "Assets/Scenes/level_00.unity";
    const string Root = "Ciudad ampliada (generado)";
    const string SpawnRoot = "Puntos de aparicion (ampliacion)";
    const string NavMeshRoot = "NavMesh Ciudad";
    const string NavMeshAsset = "Assets/Scenes/level_00/NavMesh-Ciudad.asset";
    const string PiecesFolder = "Assets/Prefabs/Ciudad";
    const string CityPrefabs = "Assets/Assets_City Elias/Prefabs/";
    const string CarPrefabs = "Assets/Assets_City Elias/Damaged Cars Pack/PREFABS/";
    const string BarrelPrefab = "Assets/Prefabs/Gameplay/BarrilExplosivo.prefab";

    // Medidas de las piezas de calle de la ciudad original (prefab Cuadra)
    const float Spacing = 37.96f;
    const float RoadWidth = 8.5f;
    const float CrossingHalf = 7f;
    const float StraightLength = 12f;
    const float GrowFactor = 3f;
    const int BlocksPerDistrict = 3;
    const int Seed = 2026;

    static StringBuilder log;
    static System.Random rng;
    static UnityEngine.SceneManagement.Scene scene;
    static readonly Dictionary<Vector2Int, Transform> districts = new Dictionary<Vector2Int, Transform>();
    static Vector2 regionMin;

    [MenuItem("VR_T1/Ampliar ciudad (level_00)")]
    public static void Run()
    {
        Generate();
        BakeNavMesh();
    }

    public static void Generate()
    {
        log = new StringBuilder();
        rng = new System.Random(Seed);
        districts.Clear();
        scene = EditorSceneManager.OpenScene(Level, OpenSceneMode.Single);
        // Regenerar: se borra lo generado antes (los muros ya quedaron apagados)
        foreach (var old in scene.GetRootGameObjects().Where(g => g.name == Root || g.name == SpawnRoot))
        {
            log.AppendLine("Borrado lo generado antes: " + old.name);
            Object.DestroyImmediate(old);
        }

        var city = scene.GetRootGameObjects().First(g => g.name == "City_Prefab");
        Bounds walls = BoundsOf(city.transform.Find("Walls").gameObject);
        Bounds streets = BoundsOf(city.transform.Find("street_01 (1)").gameObject);

        // Fase de la cuadricula: alinear las calles nuevas con las originales
        var reference = city.GetComponentsInChildren<Renderer>(true)
            .First(r => r.name.StartsWith("general_street_crossing")).bounds.center;
        float x0 = Mod(reference.x, Spacing);
        float z0 = Mod(reference.z, Spacing);
        float groundY = 0f;

        // Abrir la ciudad: los muros fortificados y las 4 paredes invisibles de
        // 'colisiones'. Ojo: 'colisiones/Cube' es el piso y debe quedar activo
        SetActive(city.transform.Find("Walls"), false);
        var bounds = city.transform.Find("colisiones");
        SetActive(bounds, true);
        foreach (var wall in new[] { "Cube (1)", "Cube (2)", "Cube (3)", "Cube (4)" })
            SetActive(bounds != null ? bounds.Find(wall) : null, false);

        var pieces = EnsureStreetPieces();
        var region = new Bounds(walls.center, new Vector3(walls.size.x * GrowFactor, 1f, walls.size.z * GrowFactor));
        regionMin = new Vector2(region.min.x, region.min.z);
        var root = new GameObject(Root);
        var spawnRoot = new GameObject(SpawnRoot);
        var groundMaterial = EnsureGroundMaterial(city);
        var enemies = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None)
            .Select(p => p.theEnemy).Where(e => e != null).Distinct().ToArray();
        var catalog = new Catalog();

        int iMin = Mathf.FloorToInt((region.min.x - x0) / Spacing);
        int iMax = Mathf.CeilToInt((region.max.x - x0) / Spacing);
        int jMin = Mathf.FloorToInt((region.min.z - z0) / Spacing);
        int jMax = Mathf.CeilToInt((region.max.z - z0) / Spacing);
        Vector3 Node(int i, int j) => new Vector3(x0 + i * Spacing, groundY, z0 + j * Spacing);
        bool Original(int i, int j) => InsideXZ(streets, Node(i, j), 3f);

        int roads = 0, blocks = 0;
        for (int i = iMin; i <= iMax; i++)
        {
            for (int j = jMin; j <= jMax; j++)
            {
                Vector3 node = Node(i, j);
                bool original = Original(i, j);
                if (!original)
                {
                    Place(pieces.crossing, node, 0f, District(root.transform, node), alignToGround: false);
                    roads++;
                }

                // Calles hacia +X y +Z, salvo las que ya existen en la ciudad original
                if (i < iMax && !(original && Original(i + 1, j)))
                {
                    for (int k = 0; k < 2; k++)
                    {
                        var p = node + Vector3.right * (CrossingHalf + StraightLength * (k + 0.5f));
                        Place(pieces.straightX, p, 0f, District(root.transform, p), alignToGround: false);
                    }
                }
                if (j < jMax && !(original && Original(i, j + 1)))
                {
                    for (int k = 0; k < 2; k++)
                    {
                        var p = node + Vector3.forward * (CrossingHalf + StraightLength * (k + 0.5f));
                        Place(pieces.straightZ, p, 0f, District(root.transform, p), alignToGround: false);
                    }
                }

                // Manzana entre este cruce y el siguiente en diagonal
                if (i < iMax && j < jMax)
                {
                    Vector3 center = node + new Vector3(Spacing * 0.5f, 0f, Spacing * 0.5f);
                    if (InsideXZ(streets, center, 0f) || !InsideXZ(region, center, 0f))
                        continue;
                    var district = District(root.transform, center);
                    BuildGround(center, district, groundMaterial);
                    BuildBlock(center, district, catalog);
                    blocks++;

                    // Punto de aparicion en la calle al costado de la manzana
                    Vector3 spawnPosition = center + new Vector3(Spacing * 0.5f, 0f, 0f);
                    if (enemies.Length > 0 && !InsideXZ(streets, spawnPosition, 2f) && i + 1 <= iMax)
                    {
                        var spawn = new GameObject("Aparicion " + i + "_" + j);
                        spawn.transform.SetParent(spawnRoot.transform, false);
                        spawn.transform.position = spawnPosition;
                        spawn.AddComponent<EnemySpawnPoint>().theEnemy = enemies[rng.Next(enemies.Length)];
                    }
                }
            }
        }

        // Suelo y limites hasta el borde exterior de las calles (no solo de la region)
        var paved = new Bounds();
        paved.SetMinMax(new Vector3(Node(iMin, jMin).x - CrossingHalf, 0f, Node(iMin, jMin).z - CrossingHalf),
                        new Vector3(Node(iMax, jMax).x + CrossingHalf, 1f, Node(iMax, jMax).z + CrossingHalf));
        BuildBounds(root.transform, paved, streets, groundY);

        foreach (var district in districts.Values)
        {
            var component = district.GetComponent<CityDistrict>();
            component.area = BoundsOf(district.gameObject);
        }

        var cam = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>()?.Camera;
        if (cam != null && cam.farClipPlane > 200f)
        {
            // La niebla corta a ~75 m: no hace falta dibujar mas alla de 200
            Undo.RecordObject(cam, "Far clip");
            cam.farClipPlane = 200f;
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[CityExpander] Ciudad ampliada\n" +
                  "Region: " + region.size.x.ToString("F0") + " x " + region.size.z.ToString("F0") + " m (original " +
                  walls.size.x.ToString("F0") + " x " + walls.size.z.ToString("F0") + ")\n" +
                  "Barrios: " + districts.Count + ", manzanas: " + blocks + ", cruces: " + roads + "\n" + log);
    }

    [MenuItem("VR_T1/Calcular NavMesh de la ciudad")]
    public static void BakeNavMesh()
    {
        scene = EditorSceneManager.OpenScene(Level, OpenSceneMode.Single);

        // Lo que se mueve o no es suelo no entra en la NavMesh
        foreach (var name in new[] { "XR Origin", "EnemyPool", "Helicoptero", "Gameplay (generado)", "Vehiculos (generado)" })
        {
            var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (go == null || go.GetComponent<NavMeshModifier>() != null)
                continue;
            var modifier = go.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = true;
        }

        var surfaceObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name == NavMeshRoot) ?? new GameObject(NavMeshRoot);
        var surface = surfaceObject.GetComponent<NavMeshSurface>();
        if (surface == null)
            surface = surfaceObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~((1 << 2) | (1 << 5) | (1 << 7) | (1 << 8) | (1 << 10) | (1 << 11));

        // La NavMesh antigua (ventana Navigation) solo cubria la ciudad original
#pragma warning disable 618
        UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
#pragma warning restore 618

        surface.BuildNavMesh();
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAsset) != null)
            AssetDatabase.DeleteAsset(NavMeshAsset);
        AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAsset);
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[CityExpander] NavMesh de la ciudad calculada: " + NavMeshAsset);
    }

    // ---------------------------------------------------------------- manzanas

    class Catalog
    {
        public readonly List<(GameObject prefab, int weight)> buildings = new List<(GameObject, int)>();
        public GameObject wide, platform, pole, ruins, rubble, rubbleSmall, barrier, lamp, garage, garageTall, barrel;
        public readonly List<GameObject> signs = new List<GameObject>();
        public readonly List<GameObject> cars = new List<GameObject>();

        public Catalog()
        {
            void AddBuilding(string name, int weight)
            {
                var prefab = Load(CityPrefabs + name + ".prefab");
                if (prefab != null)
                    buildings.Add((prefab, weight));
            }
            // Pesos pensados para Quest: los edificios modulares grandes tienen cientos
            // de renderers (el rascacielos 411), asi que en las afueras dominan las
            // casas y galpones; los edificios altos quedan en la ciudad original
            AddBuilding("house1", 3);
            AddBuilding("hall1", 2);
            AddBuilding("Garage_3", 2);
            AddBuilding("Garage", 1);
            AddBuilding("Edif_concret (8)", 2);
            AddBuilding("Edif_concret", 2);
            AddBuilding("FOB_LOD", 1);
            AddBuilding("Edif_concret (1)", 1);

            wide = Load(CityPrefabs + "2-Edif_concret (1).prefab");
            platform = Load(CityPrefabs + "Plataforma_Edif_Metal.prefab");
            pole = Load(CityPrefabs + "Bass_Grappling.prefab");
            ruins = Load(CityPrefabs + "Ruins.prefab");
            rubble = Load(CityPrefabs + "Rubble.prefab");
            rubbleSmall = Load(CityPrefabs + "Rubble_Big_LOD2.prefab");
            barrier = Load(CityPrefabs + "concrete_barrier (1).prefab");
            lamp = Load(CityPrefabs + "StreetPropss/LampPost_A.prefab");
            garage = Load(CityPrefabs + "Garage.prefab");
            garageTall = Load(CityPrefabs + "Garage_3.prefab");
            barrel = Load(BarrelPrefab);
            foreach (var s in new[] { "StreetSign_A", "StreetSign_C", "StreetSign_F" })
                if (Load(CityPrefabs + "StreetPropss/" + s + ".prefab") is GameObject sign)
                    signs.Add(sign);
            foreach (var c in new[] { "Car 1-1", "Car 1-2", "Car 2", "Car 3-1", "Car 3-2" })
                if (Load(CarPrefabs + c + ".prefab") is GameObject car)
                    cars.Add(car);
        }

        public GameObject RandomBuilding()
        {
            int total = buildings.Sum(b => b.weight);
            int roll = rng.Next(total);
            foreach (var (prefab, weight) in buildings)
            {
                if (roll < weight)
                    return prefab;
                roll -= weight;
            }
            return buildings[0].prefab;
        }
    }

    static void BuildBlock(Vector3 center, Transform parent, Catalog catalog)
    {
        float half = (Spacing - RoadWidth) * 0.5f;     // mitad del interior de la manzana
        float quadrant = half * 0.5f;
        double roll = rng.NextDouble();

        if (roll < 0.7)
        {
            // Manzana de edificios: uno por cuadrante, a veces uno ancho en dos
            var free = new List<Vector2Int> { new Vector2Int(-1, -1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(1, 1) };
            if (catalog.wide != null && rng.NextDouble() < 0.08)
            {
                int side = rng.Next(2) == 0 ? -1 : 1;
                var p = center + new Vector3(0f, 0f, side * quadrant);
                if (TryPlace(catalog.wide, p, side > 0 ? 0f : 180f, parent, 2f * half - 1f, half - 0.5f, true) != null)
                    free.RemoveAll(q => q.y == side);
            }
            foreach (var q in free)
            {
                var p = center + new Vector3(q.x * quadrant, 0f, q.y * quadrant);
                if (rng.NextDouble() < 0.2)
                {
                    TryPlace(rng.Next(2) == 0 ? catalog.rubble : catalog.ruins, p, rng.Next(4) * 90f, parent, half, half, true);
                    continue;
                }
                // Mirar hacia una de las dos calles del cuadrante
                float yaw = rng.Next(2) == 0 ? (q.x > 0 ? 90f : 270f) : (q.y > 0 ? 0f : 180f);
                for (int tries = 0; tries < 3; tries++)
                    if (TryPlace(catalog.RandomBuilding(), p, yaw, parent, half - 0.5f, half - 0.5f, true) != null)
                        break;
            }
        }
        else if (roll < 0.82)
        {
            // Plaza con plataforma y postes para el gancho
            TryPlace(catalog.platform, center, rng.Next(4) * 90f, parent, 2f * half, 2f * half, true);
            for (int k = 0; k < 2; k++)
                TryPlace(catalog.pole, center + new Vector3((k == 0 ? -1 : 1) * (half - 3f), 0f, (rng.Next(2) == 0 ? -1 : 1) * (half - 3f)), 0f, parent, 8f, 8f, true);
            for (int k = 0; k < 3; k++)
                TryPlace(catalog.barrier, center + RandomInside(half - 2f), rng.Next(360), parent, 4f, 4f, true);
        }
        else if (roll < 0.92)
        {
            // Manzana en ruinas
            TryPlace(catalog.ruins, center + RandomInside(half * 0.5f), rng.Next(4) * 90f, parent, half, half, true);
            TryPlace(catalog.ruins, center + RandomInside(half * 0.5f), rng.Next(4) * 90f, parent, half, half, true);
            for (int k = 0; k < 3; k++)
                TryPlace(rng.Next(2) == 0 ? catalog.rubble : catalog.rubbleSmall, center + RandomInside(half - 4f), rng.Next(360), parent, half, half, true);
        }
        else
        {
            // Estacionamiento
            TryPlace(catalog.garageTall, center + new Vector3(-quadrant, 0f, 0f), 90f, parent, half, 2f * half, true);
            TryPlace(catalog.garage, center + new Vector3(quadrant, 0f, -quadrant), 270f, parent, half, half, true);
            PlaceCar(catalog, center + new Vector3(quadrant, 0f, quadrant), rng.Next(360), parent);
        }

        // Faroles en las esquinas, sobre la vereda
        foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            if (rng.NextDouble() < 0.5)
                TryPlace(catalog.lamp, center + new Vector3(corner.x, 0f, corner.y) * (half + 1.2f), rng.Next(4) * 90f, parent, 4f, 4f, true);

        if (catalog.signs.Count > 0 && rng.NextDouble() < 0.3)
            TryPlace(catalog.signs[rng.Next(catalog.signs.Count)], center + new Vector3(half + 1.5f, 0f, -half - 1.5f), 0f, parent, 3f, 3f, true);

        if (catalog.pole != null && roll < 0.7 && rng.NextDouble() < 0.15)
            TryPlace(catalog.pole, center + new Vector3(-half - 2.5f, 0f, half + 2.5f), 0f, parent, 8f, 8f, true);

        // Autos abandonados en la calle
        if (rng.NextDouble() < 0.3)
        {
            var p = center + new Vector3(Spacing * 0.5f, 0f, (float)(rng.NextDouble() * 18.0 - 9.0));
            PlaceCar(catalog, p, (rng.Next(2) == 0 ? 0f : 180f) + (float)(rng.NextDouble() * 30.0 - 15.0), parent);
        }

        // Barriles explosivos cerca de una esquina
        if (catalog.barrel != null && rng.NextDouble() < 0.22)
        {
            var corner = new Vector3(rng.Next(2) == 0 ? -1 : 1, 0f, rng.Next(2) == 0 ? -1 : 1) * (half + 2f);
            int count = 1 + rng.Next(2);
            for (int k = 0; k < count; k++)
                TryPlace(catalog.barrel, center + corner + new Vector3(k * 0.9f, 0f, 0f), 0f, parent, 2f, 2f, false);
        }
    }

    static void PlaceCar(Catalog catalog, Vector3 position, float yaw, Transform parent)
    {
        if (catalog.cars.Count > 0)
            TryPlace(catalog.cars[rng.Next(catalog.cars.Count)], position, yaw, parent, 7f, 7f, false);
    }

    static Vector3 RandomInside(float extent)
    {
        return new Vector3((float)(rng.NextDouble() * 2.0 - 1.0) * extent, 0f, (float)(rng.NextDouble() * 2.0 - 1.0) * extent);
    }

    // Instancia el prefab apoyado en el suelo y centrado en 'position'.
    // Si no cabe en maxX x maxZ lo descarta. Devuelve null si no se coloco.
    static GameObject TryPlace(GameObject prefab, Vector3 position, float yaw, Transform parent, float maxX, float maxZ, bool scenery)
    {
        if (prefab == null)
            return null;

        var instance = Place(prefab, position, yaw, parent, alignToGround: true);
        var bounds = BoundsOf(instance);
        if (bounds.size.x > maxX + 0.01f || bounds.size.z > maxZ + 0.01f)
        {
            Object.DestroyImmediate(instance);
            return null;
        }
        if (scenery)
            MarkStatic(instance);
        return instance;
    }

    static GameObject Place(GameObject prefab, Vector3 position, float yaw, Transform parent, bool alignToGround)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.SetParent(parent, false);
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation;
        instance.transform.position = position;

        if (alignToGround)
        {
            var bounds = BoundsOf(instance);
            if (bounds.size != Vector3.zero)
                instance.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
        }
        else
        {
            MarkStatic(instance);
        }
        return instance;
    }

    static Transform District(Transform root, Vector3 position)
    {
        float size = Spacing * BlocksPerDistrict;
        var key = new Vector2Int(Mathf.FloorToInt((position.x - regionMin.x) / size), Mathf.FloorToInt((position.z - regionMin.y) / size));
        if (districts.TryGetValue(key, out var district))
            return district;

        var go = new GameObject("Barrio " + key.x + "_" + key.y);
        go.transform.SetParent(root, false);
        go.AddComponent<CityDistrict>();
        districts[key] = go.transform;
        return go.transform;
    }

    // ---------------------------------------------------------------- suelo y limites

    static void BuildGround(Vector3 center, Transform parent, Material material)
    {
        var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name = "Suelo";
        Object.DestroyImmediate(tile.GetComponent<Collider>());
        tile.transform.SetParent(parent, false);
        tile.transform.position = center + Vector3.down * 0.03f;
        tile.transform.localScale = new Vector3(Spacing, 0.02f, Spacing);
        tile.GetComponent<Renderer>().sharedMaterial = material;
        MarkStatic(tile);
    }

    // Colliders de suelo alrededor de la ciudad original y muros invisibles en el borde
    static void BuildBounds(Transform root, Bounds region, Bounds original, float groundY)
    {
        var limits = new GameObject("Suelo y limites").transform;
        limits.SetParent(root, false);

        void Box(string name, Vector3 min, Vector3 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(limits, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = (min + max) * 0.5f;
            box.size = max - min;
        }

        float bottom = groundY - 2f;
        Box("Suelo oeste", new Vector3(region.min.x, bottom, region.min.z), new Vector3(original.min.x, groundY, region.max.z));
        Box("Suelo este", new Vector3(original.max.x, bottom, region.min.z), new Vector3(region.max.x, groundY, region.max.z));
        Box("Suelo sur", new Vector3(original.min.x, bottom, region.min.z), new Vector3(original.max.x, groundY, original.min.z));
        Box("Suelo norte", new Vector3(original.min.x, bottom, original.max.z), new Vector3(original.max.x, groundY, region.max.z));

        const float height = 15f, thickness = 2f;
        Box("Limite oeste", new Vector3(region.min.x - thickness, groundY, region.min.z), new Vector3(region.min.x, groundY + height, region.max.z));
        Box("Limite este", new Vector3(region.max.x, groundY, region.min.z), new Vector3(region.max.x + thickness, groundY + height, region.max.z));
        Box("Limite sur", new Vector3(region.min.x, groundY, region.min.z - thickness), new Vector3(region.max.x, groundY + height, region.min.z));
        Box("Limite norte", new Vector3(region.min.x, groundY, region.max.z), new Vector3(region.max.x, groundY + height, region.max.z + thickness));
    }

    static Material EnsureGroundMaterial(GameObject city)
    {
        const string path = "Assets/Data/Materials/SueloCiudad.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        // Copia del material de la vereda de la ciudad original, repetido por manzana
        var sidewalk = city.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.name.StartsWith("general_street_crossing"))
            .SelectMany(r => r.sharedMaterials)
            .FirstOrDefault(m => m != null && m.name.Contains("sidewalk"));
        material = sidewalk != null ? new Material(sidewalk) : new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.35f, 0.33f) };
        material.mainTextureScale = new Vector2(8f, 8f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // ---------------------------------------------------------------- piezas de calle

    struct StreetPieces { public GameObject crossing, straightX, straightZ; }

    // Prefabs pequenos con un cruce y una recta, sacados del prefab Cuadra original
    static StreetPieces EnsureStreetPieces()
    {
        if (!AssetDatabase.IsValidFolder(PiecesFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Ciudad");

        var cuadra = Load(CityPrefabs + "Street/Cuadra.prefab");
        return new StreetPieces
        {
            crossing = EnsurePiece(cuadra, "general_street_crossing_w_sidewalk", "Cruce"),
            straightX = EnsurePiece(cuadra, "general_street_straight_w_sidewalk_long (2)", "CalleX"),
            straightZ = EnsurePiece(cuadra, "general_street_straight_w_sidewalk_long (5)", "CalleZ"),
        };
    }

    static GameObject EnsurePiece(GameObject cuadra, string childName, string pieceName)
    {
        string path = PiecesFolder + "/" + pieceName + ".prefab";
        var existing = Load(path);
        if (existing != null)
            return existing;

        var copy = (GameObject)Object.Instantiate(cuadra);
        var child = copy.GetComponentsInChildren<Transform>(true).First(t => t.name == childName);
        child.SetParent(null, true);
        // Centrar la pieza en su origen manteniendo la rotacion
        var bounds = BoundsOf(child.gameObject);
        child.position -= new Vector3(bounds.center.x, 0f, bounds.center.z);
        var holder = new GameObject(pieceName);
        child.SetParent(holder.transform, true);
        var prefab = PrefabUtility.SaveAsPrefabAsset(holder, path);
        Object.DestroyImmediate(holder);
        Object.DestroyImmediate(copy);
        log.AppendLine("Pieza de calle: " + path);
        return prefab;
    }

    // ---------------------------------------------------------------- utilidades

    static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    static void SetActive(Transform t, bool active)
    {
        if (t == null || t.gameObject.activeSelf == active)
            return;
        t.gameObject.SetActive(active);
        PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        log.AppendLine((active ? "Activado: " : "Desactivado: ") + t.name);
    }

    static float Mod(float value, float m) => ((value % m) + m) % m;

    static bool InsideXZ(Bounds b, Vector3 p, float margin)
    {
        return p.x > b.min.x - margin && p.x < b.max.x + margin && p.z > b.min.z - margin && p.z < b.max.z + margin;
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);
        var bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);
        return bounds;
    }

    // Estatico solo lo que no se mueve (sin Rigidbody ni scripts en la jerarquia)
    static void MarkStatic(GameObject root)
    {
        if (root.GetComponentInChildren<Rigidbody>(true) != null || root.GetComponentInChildren<MonoBehaviour>(true) != null)
            return;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            var renderer = t.GetComponent<Renderer>();
            if (renderer != null && renderer.bounds.size.magnitude > 3f)
                flags |= StaticEditorFlags.OccluderStatic;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, GameObjectUtility.GetStaticEditorFlags(t.gameObject) | flags);
        }
    }
}
