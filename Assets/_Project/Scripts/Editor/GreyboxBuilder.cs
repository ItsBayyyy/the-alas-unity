using System;
using System.Collections.Generic;
using System.IO;
using TheAlas.Player;
using TheAlas.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace TheAlas.EditorTools
{
    /// <summary>
    /// Membangun prefab placeholder dan scene greybox Hutan sesuai bagian Level design di GDD.
    ///
    /// Prefab hanya dibuat kalau belum ada, jadi model asli yang sudah dipasang tim
    /// di dalam prefab tidak pernah tertimpa. Scene dibangun ulang dari nol setiap kali
    /// menu dijalankan; pakai hanya selama fase greybox.
    /// </summary>
    public static class GreyboxBuilder
    {
        internal const string Root = "Assets/_Project";
        const string MaterialDir = Root + "/Art/Materials/Greybox";
        internal const string PrefabDir = Root + "/Prefabs";
        internal const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/Hutan.unity";
        const string LightingPath = SceneDir + "/Hutan_Lighting.lighting";

        const float MapHalfSize = 80f;
        const int Seed = 1311;

        static readonly Color FogColor = new Color32(0x0D, 0x10, 0x0B, 0xFF);

        // Utara = +Z. Mobil di tepi utara, makhluk mulai di ujung selatan sekitar 140 m dari pemain.
        static readonly Vector3 CarPosition = new(0f, 0f, 72f);
        static readonly Vector3 PlayerSpawn = new(3.5f, 0f, 67f);
        internal static readonly Vector3 CreatureSpawn = new(0f, 0f, -68f);

        static readonly Vector3 RiverCenter = new(0f, 0f, -52f);
        static readonly Vector2 RiverSize = new(64f, 7f);

        readonly struct LandmarkSpot
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly float ClearRadius;

            public LandmarkSpot(string name, Vector3 position, float clearRadius)
            {
                Name = name;
                Position = position;
                ClearRadius = clearRadius;
            }
        }

        // Jarak dari mobil 56–127 m, dalam rentang 30–130 m di GDD.
        static readonly LandmarkSpot[] Landmarks =
        {
            new("GubukReyot", new Vector3(-45f, 0f, 40f), 9f),
            new("SumurTua", new Vector3(40f, 0f, 30f), 15f),
            new("PohonBeringin", new Vector3(0f, 0f, 0f), 13f),
            new("MakamKeramat", new Vector3(55f, 0f, -35f), 10f),
            new("PosRonda", new Vector3(-55f, 0f, -40f), 8f),
            new("KaliKering", RiverCenter, 0f), // area persegi panjang, dicek terpisah
        };

        internal class Palette
        {
            public Material Ground, Trunk, PineLeaves, TeakLeaves, Bush, Log, Stone, Gravel;
            public Material CarBody, CarGlass, Tire, CabinGlow, Wood, Thatch, StoneWall, WellHole, Grave, Root;
            public Material ItemPart, CreatureBody, CreatureEye;
        }

        [MenuItem("Tools/The Alas/Bangun Greybox Hutan")]
        static void BuildFromMenu()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Bangun ulang greybox?",
                    "Scene Hutan akan dibangun ulang dari nol dan perubahan manual di scene itu hilang. " +
                    "Prefab yang sudah ada tidak disentuh.", "Bangun ulang", "Batal"))
                return;

            Build();
        }

        public static void Build()
        {
            EnsureFolder(MaterialDir);
            foreach (var sub in new[] { "Player", "Vehicle", "Environment", "Landmarks" })
                EnsureFolder($"{PrefabDir}/{sub}");

            GameplaySetup.EnsureLayers();
            var palette = CreatePalette();
            var prefabs = CreatePrefabs(palette);
            GameplaySetup.ConfigurePrefabs(prefabs, palette);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();

            var environment = new GameObject("Environment").transform;
            BuildGround(environment, palette);
            BuildBounds(environment);

            var landmarks = new GameObject("Landmarks").transform;
            foreach (var spot in Landmarks)
                PlaceLandmark(prefabs[$"Landmark_{spot.Name}"], spot, landmarks);

            var rng = new Random(Seed);
            var logSegments = PlaceLogs(prefabs["Log_01"], environment, rng);
            PlaceBushes(prefabs["Bush_01"], environment, rng);
            PlaceTrees(prefabs["Tree_Pinus_01"], prefabs["Tree_Jati_01"], environment, logSegments, rng);
            MarkStatic(environment);
            MarkStatic(landmarks);

            var gameplay = new GameObject("Gameplay").transform;
            var car = Spawn(prefabs["Car"], CarPosition, Quaternion.Euler(0f, 90f, 0f), gameplay);
            var player = Spawn(prefabs["Player"], PlayerSpawn, Quaternion.Euler(0f, 180f, 0f), gameplay);
            GameplaySetup.PopulateScene(prefabs, gameplay, player, car);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[Greybox] Scene tersimpan di {ScenePath}");
        }

        // ---------- Material ----------

        static Palette CreatePalette() => new()
        {
            Ground = Mat("Ground", "#1E2416"),
            Trunk = Mat("Trunk", "#3A2C20"),
            PineLeaves = Mat("PineLeaves", "#1F3321"),
            TeakLeaves = Mat("TeakLeaves", "#34401F"),
            Bush = Mat("Bush", "#2B3D1E"),
            Log = Mat("Log", "#4A3826"),
            Stone = Mat("Stone", "#55554F"),
            Gravel = Mat("Gravel", "#6B675C"),
            CarBody = Mat("CarBody", "#5C6B6E"),
            CarGlass = Mat("CarGlass", "#1A2226"),
            Tire = Mat("Tire", "#151515"),
            CabinGlow = Mat("CabinGlow", "#E0A63C", emission: 2.5f),
            Wood = Mat("Wood", "#5A4632"),
            Thatch = Mat("Thatch", "#6E5C3A"),
            StoneWall = Mat("StoneWall", "#77736A"),
            WellHole = Mat("WellHole", "#050505"),
            Grave = Mat("Grave", "#8A867A"),
            Root = Mat("Root", "#2E241A"),
            // Kuning lentera untuk barang wajib (palet GDD), sedikit menyala supaya terlihat di gelap.
            ItemPart = Mat("ItemPart", "#E0A63C", emission: 0.6f),
            CreatureBody = Mat("CreatureBody", "#121410"),
            CreatureEye = Mat("CreatureEye", "#000000", emission: 1f),
        };

        internal static Material Mat(string name, string hex, float emission = 0f)
        {
            string path = $"{MaterialDir}/M_Greybox_{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;

            ColorUtility.TryParseHtmlString(hex, out var color);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.1f);
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------- Prefab ----------

        static Dictionary<string, GameObject> CreatePrefabs(Palette p) => new()
        {
            ["Player"] = EnsurePlayerPrefab(),
            ["Car"] = EnsurePrefab("Vehicle/PF_Car", (root, model) => BuildCar(root, model, p)),
            ["Tree_Pinus_01"] = EnsurePrefab("Environment/PF_Tree_Pinus_01", (_, m) => BuildPine(m, p)),
            ["Tree_Jati_01"] = EnsurePrefab("Environment/PF_Tree_Jati_01", (_, m) => BuildTeak(m, p)),
            ["Bush_01"] = EnsurePrefab("Environment/PF_Bush_01", (r, m) => BuildBush(r, m, p)),
            ["Log_01"] = EnsurePrefab("Environment/PF_Log_01", (r, m) => BuildLog(r, m, p)),
            ["Landmark_GubukReyot"] = EnsurePrefab("Landmarks/PF_Landmark_GubukReyot", (_, m) => BuildHut(m, p)),
            ["Landmark_SumurTua"] = EnsurePrefab("Landmarks/PF_Landmark_SumurTua", (_, m) => BuildWell(m, p)),
            ["Landmark_PohonBeringin"] = EnsurePrefab("Landmarks/PF_Landmark_PohonBeringin", (_, m) => BuildBanyan(m, p)),
            ["Landmark_MakamKeramat"] = EnsurePrefab("Landmarks/PF_Landmark_MakamKeramat", (_, m) => BuildGraves(m, p)),
            ["Landmark_PosRonda"] = EnsurePrefab("Landmarks/PF_Landmark_PosRonda", (_, m) => BuildGuardPost(m, p)),
            ["Landmark_KaliKering"] = EnsurePrefab("Landmarks/PF_Landmark_KaliKering", (r, m) => BuildRiverbed(r, m, p)),
        };

        /// <summary>
        /// Struktur setiap prefab: root (pivot di tanah, berisi komponen gameplay) → "Model" (visual).
        /// Artis cukup mengganti isi "Model" dengan mesh final.
        /// </summary>
        internal static GameObject EnsurePrefab(string relativePath, Action<GameObject, Transform> build)
        {
            string path = $"{PrefabDir}/{relativePath}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing;

            var root = new GameObject(Path.GetFileName(relativePath));
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            build(root, model);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject EnsurePlayerPrefab()
        {
            const string path = PrefabDir + "/Player/PF_Player.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing;

            var root = new GameObject("PF_Player");
            var controller = root.AddComponent<CharacterController>();
            controller.radius = 0.35f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.04f;
            controller.slopeLimit = 50f;

            var cameraRoot = new GameObject("CameraRoot") { tag = "MainCamera" };
            cameraRoot.transform.SetParent(root.transform, false);
            cameraRoot.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var cam = cameraRoot.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            cameraRoot.AddComponent<UniversalAdditionalCameraData>();
            cameraRoot.AddComponent<AudioListener>();

            // Di tangan kanan, sedikit di bawah mata, supaya bayangan senter terlihat.
            var flashlight = new GameObject("Flashlight");
            flashlight.transform.SetParent(cameraRoot.transform, false);
            flashlight.transform.localPosition = new Vector3(0.22f, -0.18f, 0.25f);
            var spot = flashlight.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 30f;
            spot.spotAngle = 60f;
            spot.innerSpotAngle = 26f;
            spot.intensity = 45f;
            spot.color = new Color(1f, 0.95f, 0.84f);
            spot.shadows = LightShadows.Soft;
            spot.shadowNearPlane = 0.1f;
            flashlight.AddComponent<Flashlight>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void BuildCar(GameObject root, Transform m, Palette p)
        {
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(1.9f, 1.9f, 4.4f);
            box.center = new Vector3(0f, 0.95f, 0f);

            Box(m, "Body", new Vector3(0f, 0.75f, 0f), new Vector3(1.9f, 0.8f, 4.4f), p.CarBody);
            Box(m, "Cabin", new Vector3(0f, 1.5f, -0.3f), new Vector3(1.7f, 0.7f, 2.2f), p.CarGlass);
            Box(m, "HoodOpen", new Vector3(0f, 1.6f, 1.75f), new Vector3(1.8f, 0.06f, 1.2f), p.CarBody, euler: new Vector3(-55f, 0f, 0f));
            foreach (var x in new[] { -0.95f, 0.95f })
            foreach (var z in new[] { -1.4f, 1.4f })
                Cylinder(m, "Wheel", new Vector3(x, 0.33f, z), 0.65f, 0.25f, p.Tire, euler: new Vector3(0f, 0f, 90f));

            Box(m, "CabinLamp", new Vector3(0f, 1.82f, -0.3f), new Vector3(0.3f, 0.04f, 0.15f), p.CabinGlow);
            var lampLight = new GameObject("CabinLight").AddComponent<Light>();
            lampLight.transform.SetParent(root.transform, false);
            lampLight.transform.localPosition = new Vector3(0f, 1.7f, -0.3f);
            lampLight.type = LightType.Point;
            lampLight.range = 9f;
            lampLight.intensity = 4f;
            lampLight.color = new Color32(0xE0, 0xA6, 0x3C, 0xFF);
            lampLight.shadows = LightShadows.None;
        }

        static void BuildPine(Transform m, Palette p)
        {
            Cylinder(m, "Trunk", new Vector3(0f, 4.5f, 0f), 0.35f, 9f, p.Trunk);
            Primitive(m, PrimitiveType.Capsule, "Leaves", new Vector3(0f, 8f, 0f), new Vector3(2.6f, 3.5f, 2.6f), p.PineLeaves, collider: false);
        }

        static void BuildTeak(Transform m, Palette p)
        {
            Cylinder(m, "Trunk", new Vector3(0f, 3f, 0f), 0.5f, 6f, p.Trunk);
            Primitive(m, PrimitiveType.Sphere, "Leaves", new Vector3(0f, 7f, 0f), new Vector3(5f, 3.5f, 5f), p.TeakLeaves, collider: false);
        }

        static void BuildBush(GameObject root, Transform m, Palette p)
        {
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.2f, 1.2f, 2.2f);
            trigger.center = new Vector3(0f, 0.6f, 0f);
            SetSurface(root.AddComponent<GroundSurface>(), SurfaceType.Bush);

            Primitive(m, PrimitiveType.Sphere, "Leaves", new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 1.1f, 2.2f), p.Bush, collider: false);
        }

        static void BuildLog(GameObject root, Transform m, Palette p)
        {
            // Tinggi 0,45 m adalah angka desain (batas lompat sekitar 3 unit beban), jadi collider-nya di root.
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(5f, 0.45f, 0.45f);
            box.center = new Vector3(0f, 0.225f, 0f);

            Cylinder(m, "Trunk", new Vector3(0f, 0.225f, 0f), 0.45f, 5f, p.Log, collider: false, euler: new Vector3(0f, 0f, 90f));
        }

        static void BuildHut(Transform m, Palette p)
        {
            const float size = 4.5f, height = 2.6f, wall = 0.15f, door = 1.2f;
            float half = size * 0.5f;
            float side = (size - door) * 0.5f;

            Box(m, "Floor", new Vector3(0f, 0.05f, 0f), new Vector3(size, 0.1f, size), p.Wood);
            Box(m, "WallBack", new Vector3(0f, height / 2f, -half), new Vector3(size, height, wall), p.Wood);
            Box(m, "WallLeft", new Vector3(-half, height / 2f, 0f), new Vector3(wall, height, size), p.Wood);
            Box(m, "WallRight", new Vector3(half, height / 2f, 0f), new Vector3(wall, height, size), p.Wood);
            Box(m, "WallFrontL", new Vector3(-(door + side) / 2f, height / 2f, half), new Vector3(side, height, wall), p.Wood);
            Box(m, "WallFrontR", new Vector3((door + side) / 2f, height / 2f, half), new Vector3(side, height, wall), p.Wood);
            Box(m, "DoorLintel", new Vector3(0f, height - 0.3f, half), new Vector3(door, 0.6f, wall), p.Wood);
            Box(m, "Roof", new Vector3(0f, height + 0.25f, 0f), new Vector3(size + 0.8f, 0.15f, size + 0.8f), p.Thatch, euler: new Vector3(8f, 0f, 0f));
        }

        static void BuildWell(Transform m, Palette p)
        {
            Cylinder(m, "Ring", new Vector3(0f, 0.45f, 0f), 1.8f, 0.9f, p.StoneWall);
            Cylinder(m, "Hole", new Vector3(0f, 0.91f, 0f), 1.4f, 0.02f, p.WellHole, collider: false);
            Box(m, "PostL", new Vector3(-0.85f, 1.3f, 0f), new Vector3(0.15f, 1.8f, 0.15f), p.Wood);
            Box(m, "PostR", new Vector3(0.85f, 1.3f, 0f), new Vector3(0.15f, 1.8f, 0.15f), p.Wood);
            Cylinder(m, "Crossbar", new Vector3(0f, 2.1f, 0f), 0.12f, 1.9f, p.Wood, collider: false, euler: new Vector3(0f, 0f, 90f));
        }

        static void BuildBanyan(Transform m, Palette p)
        {
            Cylinder(m, "Trunk", new Vector3(0f, 7f, 0f), 3.2f, 14f, p.Trunk);
            Primitive(m, PrimitiveType.Sphere, "Canopy", new Vector3(0f, 15f, 0f), new Vector3(18f, 8f, 18f), p.TeakLeaves, collider: false);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Box(m, "Buttress", dir * 2.2f + Vector3.up * 0.25f, new Vector3(0.4f, 0.5f, 2.2f), p.Root, euler: new Vector3(0f, angle, 0f));
            }

            // Akar gantung: visual saja, tidak menghalangi gerak.
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f + (i % 2) * 11f;
                float radius = 3.2f + (i % 3) * 1.6f;
                float length = 7f + (i % 4) * 1.2f;
                Vector3 pos = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                pos.y = 12f - length / 2f;
                Cylinder(m, "HangingRoot", pos, 0.12f, length, p.Root, collider: false);
            }
        }

        static void BuildGraves(Transform m, Palette p)
        {
            // Nisan polos tanpa simbol agama apa pun (aturan SARA).
            const float size = 9f, wallH = 0.5f, gap = 2f;
            float half = size / 2f;
            float sideLen = (size - gap) / 2f;

            Box(m, "WallBack", new Vector3(0f, wallH / 2f, -half), new Vector3(size, wallH, 0.3f), p.StoneWall);
            Box(m, "WallLeft", new Vector3(-half, wallH / 2f, 0f), new Vector3(0.3f, wallH, size), p.StoneWall);
            Box(m, "WallRight", new Vector3(half, wallH / 2f, 0f), new Vector3(0.3f, wallH, size), p.StoneWall);
            Box(m, "WallFrontL", new Vector3(-(gap + sideLen) / 2f, wallH / 2f, half), new Vector3(sideLen, wallH, 0.3f), p.StoneWall);
            Box(m, "WallFrontR", new Vector3((gap + sideLen) / 2f, wallH / 2f, half), new Vector3(sideLen, wallH, 0.3f), p.StoneWall);

            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++)
            {
                var pos = new Vector3(-2.6f + col * 2.6f, 0f, -2.2f + row * 3.4f);
                Box(m, "Mound", pos + new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.25f, 1.8f), p.Ground);
                Box(m, "Headstone", pos + new Vector3(0f, 0.35f, -0.95f), new Vector3(0.35f, 0.7f, 0.12f), p.Grave,
                    euler: new Vector3((col - 1) * 6f, 0f, (row - 0.5f) * 8f));
            }
        }

        static void BuildGuardPost(Transform m, Palette p)
        {
            Box(m, "Platform", new Vector3(0f, 0.25f, 0f), new Vector3(2.6f, 0.5f, 2.6f), p.Wood);
            foreach (var x in new[] { -1.2f, 1.2f })
            foreach (var z in new[] { -1.2f, 1.2f })
                Box(m, "Post", new Vector3(x, 1.4f, z), new Vector3(0.15f, 2.8f, 0.15f), p.Wood);
            Box(m, "Roof", new Vector3(0f, 2.9f, 0f), new Vector3(3.2f, 0.12f, 3.2f), p.Thatch, euler: new Vector3(0f, 0f, 7f));
            Cylinder(m, "Kentongan", new Vector3(1.45f, 1.7f, 1.45f), 0.25f, 0.8f, p.Log, collider: false);
        }

        static void BuildRiverbed(GameObject root, Transform m, Palette p)
        {
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(RiverSize.x, 1.2f, RiverSize.y);
            trigger.center = new Vector3(0f, 0.6f, 0f);
            SetSurface(root.AddComponent<GroundSurface>(), SurfaceType.Gravel);

            Box(m, "Gravel", new Vector3(0f, 0.02f, 0f), new Vector3(RiverSize.x, 0.04f, RiverSize.y), p.Gravel, collider: false);
            // Tebing di bawah step offset (0,3 m) supaya bisa dilewati tanpa lompat.
            float bankZ = RiverSize.y / 2f + 0.5f;
            Box(m, "BankNorth", new Vector3(0f, 0.12f, bankZ), new Vector3(RiverSize.x, 0.25f, 1f), p.Ground);
            Box(m, "BankSouth", new Vector3(0f, 0.12f, -bankZ), new Vector3(RiverSize.x, 0.25f, 1f), p.Ground);

            var rng = new Random(Seed + 7);
            for (int i = 0; i < 18; i++)
            {
                float s = Range(rng, 0.3f, 0.7f);
                var pos = new Vector3(Range(rng, -RiverSize.x / 2f + 1f, RiverSize.x / 2f - 1f), s * 0.3f,
                    Range(rng, -RiverSize.y / 2f + 0.5f, RiverSize.y / 2f - 0.5f));
                Box(m, "Stone", pos, new Vector3(s, s * 0.6f, s * 0.8f), p.Stone, euler: new Vector3(0f, Range(rng, 0f, 360f), 0f));
            }
        }

        // ---------- Scene ----------

        static void SetupLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.04f, 0.05f);
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.09f; // pandang efektif sekitar 15–25 m

            var lighting = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (!lighting)
            {
                lighting = new LightingSettings { bakedGI = false, realtimeGI = false };
                AssetDatabase.CreateAsset(lighting, LightingPath);
            }
            Lightmapping.lightingSettings = lighting;

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.06f;
            moon.color = new Color(0.55f, 0.62f, 0.8f);
            moon.shadows = LightShadows.None;
            moon.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        }

        static void BuildGround(Transform parent, Palette p)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(MapHalfSize / 5f, 1f, MapHalfSize / 5f);
            ground.GetComponent<Renderer>().sharedMaterial = p.Ground;
        }

        static void BuildBounds(Transform parent)
        {
            var bounds = new GameObject("MapBounds").transform;
            bounds.SetParent(parent, false);
            float size = MapHalfSize * 2f;
            AddWall(bounds, new Vector3(0f, 3f, MapHalfSize + 0.5f), new Vector3(size + 2f, 6f, 1f));
            AddWall(bounds, new Vector3(0f, 3f, -MapHalfSize - 0.5f), new Vector3(size + 2f, 6f, 1f));
            AddWall(bounds, new Vector3(MapHalfSize + 0.5f, 3f, 0f), new Vector3(1f, 6f, size + 2f));
            AddWall(bounds, new Vector3(-MapHalfSize - 0.5f, 3f, 0f), new Vector3(1f, 6f, size + 2f));
        }

        static void AddWall(Transform parent, Vector3 center, Vector3 size)
        {
            var wall = new GameObject("Wall").AddComponent<BoxCollider>();
            wall.transform.SetParent(parent, false);
            wall.center = center;
            wall.size = size;
        }

        static void PlaceLandmark(GameObject prefab, LandmarkSpot spot, Transform parent)
        {
            // Pintu dan bukaan menghadap mobil supaya pemain langsung melihat sisi yang bisa dimasuki.
            Vector3 toCar = CarPosition - spot.Position;
            toCar.y = 0f;
            var rotation = spot.Name == "KaliKering" ? Quaternion.identity : Quaternion.LookRotation(toCar);
            Spawn(prefab, spot.Position, rotation, parent);
        }

        /// <summary>
        /// Batang tumbang dipasang melintang di garis lurus mobil → landmark, supaya jalan pintas
        /// selalu menuntut pilihan: memutar senyap atau melompat berisik.
        /// </summary>
        static List<(Vector3 a, Vector3 b)> PlaceLogs(GameObject prefab, Transform environment, Random rng)
        {
            var parent = new GameObject("Logs").transform;
            parent.SetParent(environment, false);
            var segments = new List<(Vector3, Vector3)>();

            void Place(Vector3 pos, float yaw)
            {
                var log = Spawn(prefab, pos, Quaternion.Euler(0f, yaw, 0f), parent);
                Vector3 half = log.transform.right * 2.5f;
                segments.Add((pos - half, pos + half));
            }

            foreach (var spot in Landmarks)
            {
                Vector3 dir = spot.Position - CarPosition;
                float yaw = Quaternion.LookRotation(dir).eulerAngles.y; // sumbu panjang log = right, jadi melintang
                foreach (float t in new[] { 0.35f, 0.7f })
                {
                    Vector3 pos = CarPosition + dir * t + new Vector3(Range(rng, -1f, 1f), 0f, Range(rng, -1f, 1f));
                    if (!IsReserved(pos, 3f)) Place(pos, yaw + Range(rng, -15f, 15f));
                }
            }

            for (int placed = 0, tries = 0; placed < 16 && tries < 200; tries++)
            {
                var pos = RandomMapPoint(rng, 4f);
                if (IsReserved(pos, 4f)) continue;
                Place(pos, Range(rng, 0f, 360f));
                placed++;
            }
            return segments;
        }

        static void PlaceBushes(GameObject prefab, Transform environment, Random rng)
        {
            var parent = new GameObject("Bushes").transform;
            parent.SetParent(environment, false);

            foreach (var spot in Landmarks)
            {
                Vector3 pos = Vector3.Lerp(CarPosition, spot.Position, 0.55f);
                if (!IsReserved(pos, 2f))
                    SpawnScaled(prefab, pos, rng, parent, 1.2f, 1.8f);
            }

            for (int placed = 0, tries = 0; placed < 60 && tries < 400; tries++)
            {
                var pos = RandomMapPoint(rng, 2f);
                if (IsReserved(pos, 2f)) continue;
                SpawnScaled(prefab, pos, rng, parent, 0.8f, 1.6f);
                placed++;
            }
        }

        static void PlaceTrees(GameObject pine, GameObject teak, Transform environment,
            List<(Vector3 a, Vector3 b)> logs, Random rng)
        {
            var parent = new GameObject("Trees").transform;
            parent.SetParent(environment, false);

            const float step = 5.5f, jitter = 2f, density = 0.7f;
            int count = 0;
            for (float x = -MapHalfSize + 2f; x <= MapHalfSize - 2f; x += step)
            for (float z = -MapHalfSize + 2f; z <= MapHalfSize - 2f; z += step)
            {
                if (rng.NextDouble() > density) continue;
                var pos = new Vector3(x + Range(rng, -jitter, jitter), 0f, z + Range(rng, -jitter, jitter));
                if (IsReserved(pos, 1.5f) || NearAnyLog(pos, logs, 1.5f)) continue;

                SpawnScaled(rng.NextDouble() < 0.6 ? pine : teak, pos, rng, parent, 0.85f, 1.2f);
                count++;
            }
            Debug.Log($"[Greybox] {count} pohon ditempatkan");
        }

        static bool IsReserved(Vector3 pos, float margin)
        {
            if (Flat(pos - CarPosition).magnitude < 14f + margin) return true;
            if (Flat(pos - CreatureSpawn).magnitude < 4f + margin) return true;

            foreach (var spot in Landmarks)
                if (spot.ClearRadius > 0f && Flat(pos - spot.Position).magnitude < spot.ClearRadius + margin)
                    return true;

            Vector3 r = pos - RiverCenter;
            return Mathf.Abs(r.x) < RiverSize.x / 2f + margin && Mathf.Abs(r.z) < RiverSize.y / 2f + 1f + margin;
        }

        static bool NearAnyLog(Vector3 pos, List<(Vector3 a, Vector3 b)> logs, float distance)
        {
            foreach (var (a, b) in logs)
            {
                Vector3 ab = Flat(b - a);
                float t = Mathf.Clamp01(Vector3.Dot(Flat(pos - a), ab) / ab.sqrMagnitude);
                if (Flat(pos - (a + ab * t)).magnitude < distance) return true;
            }
            return false;
        }

        static void MarkStatic(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        // ---------- Helper ----------

        internal static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            return go;
        }

        static void SpawnScaled(GameObject prefab, Vector3 pos, Random rng, Transform parent, float min, float max)
        {
            var go = Spawn(prefab, pos, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), parent);
            go.transform.localScale = Vector3.one * Range(rng, min, max);
        }

        internal static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat,
            bool collider = true, Vector3 euler = default) =>
            Primitive(parent, PrimitiveType.Cube, name, pos, size, mat, collider, euler);

        /// <summary>Silinder dengan ukuran nyata (diameter, tinggi), bukan skala primitive Unity.</summary>
        internal static GameObject Cylinder(Transform parent, string name, Vector3 pos, float diameter, float height, Material mat,
            bool collider = true, Vector3 euler = default) =>
            Primitive(parent, PrimitiveType.Cylinder, name, pos, new Vector3(diameter, height / 2f, diameter), mat, collider, euler);

        internal static GameObject Primitive(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale,
            Material mat, bool collider = true, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            var col = go.GetComponent<Collider>();
            // Primitive silinder memakai CapsuleCollider yang membulat; ganti dengan bentuk aslinya.
            if (!collider || type == PrimitiveType.Cylinder) Object.DestroyImmediate(col);
            if (collider && type == PrimitiveType.Cylinder) go.AddComponent<MeshCollider>();
            return go;
        }

        internal static void SetReference(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSurface(GroundSurface surface, SurfaceType type)
        {
            var so = new SerializedObject(surface);
            so.FindProperty("type").enumValueIndex = (int)type;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Vector3 RandomMapPoint(Random rng, float edgeMargin) =>
            new(Range(rng, -MapHalfSize + edgeMargin, MapHalfSize - edgeMargin), 0f,
                Range(rng, -MapHalfSize + edgeMargin, MapHalfSize - edgeMargin));

        static float Range(Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
