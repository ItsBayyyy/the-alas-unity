using System;
using System.Collections.Generic;
using System.Linq;
using TheAlas.Core;
using TheAlas.Creature;
using TheAlas.Items;
using TheAlas.Player;
using TheAlas.UI;
using TheAlas.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace TheAlas.EditorTools
{
    /// <summary>
    /// Bagian gameplay dari <see cref="GreyboxBuilder"/>: layer, data barang, prefab barang dan makhluk,
    /// komponen di prefab pemain, mobil, dan landmark, lalu sistem ronde dan navmesh di scene.
    ///
    /// Semua langkah idempoten: komponen hanya ditambah kalau belum ada, dan isi "Model"
    /// tidak pernah disentuh, jadi aman dijalankan setelah artis memasang model final.
    /// </summary>
    static class GameplaySetup
    {
        const string ItemDataDir = GreyboxBuilder.Root + "/Data/Items";
        const string NavMeshPath = GreyboxBuilder.SceneDir + "/Hutan_NavMesh.asset";

        // Layer 8–10 dipakai supaya navmesh dan interaksi bisa mengabaikan pemain, barang, dan makhluk.
        static readonly (int index, string name)[] Layers = { (8, "Player"), (9, "Item"), (10, "Creature") };

        /// <summary>Penanjakan agen 0,5 m supaya makhluk setinggi 3 m bisa melangkahi batang tumbang 0,45 m.</summary>
        const float AgentClimb = 0.5f;

        readonly struct PartSpec
        {
            public readonly string Id, DisplayName;
            public readonly float Weight;
            public readonly Vector3 Size;

            public PartSpec(string id, string displayName, float weight, Vector3 size)
            {
                Id = id;
                DisplayName = displayName;
                Weight = weight;
                Size = size;
            }
        }

        // Bobot dari GDD; ukuran dibesarkan sedikit dari aslinya supaya mudah dibidik di greybox.
        static readonly PartSpec[] Parts =
        {
            new("KunciMobil", "Kunci Mobil", 0.5f, new Vector3(0.26f, 0.06f, 0.12f)),
            new("Busi", "Busi", 0.5f, new Vector3(0.1f, 0.1f, 0.3f)),
            new("Aki", "Aki", 3f, new Vector3(0.34f, 0.22f, 0.2f)),
            new("JerikenBensin", "Jeriken Bensin", 3.5f, new Vector3(0.3f, 0.42f, 0.17f)),
        };

        // Titik muncul suku cadang, dalam ruang lokal landmark (+Z menghadap mobil).
        static readonly Dictionary<string, (string displayName, Vector3[] spawns)> LandmarkInfo = new()
        {
            ["GubukReyot"] = ("Gubuk Reyot", new[] { new Vector3(0f, 0.1f, -1.3f), new Vector3(1.4f, 0.1f, -1.4f), new Vector3(-1.5f, 0.1f, 0.6f) }),
            ["SumurTua"] = ("Sumur Tua", new[] { new Vector3(1.5f, 0f, 0.4f), new Vector3(-1.3f, 0f, -1.1f), new Vector3(0.2f, 0f, 1.7f) }),
            ["PohonBeringin"] = ("Pohon Beringin", new[] { new Vector3(2.9f, 0f, 0.9f), new Vector3(-2.7f, 0f, -1.5f), new Vector3(0.8f, 0f, -3.1f) }),
            ["MakamKeramat"] = ("Makam Keramat", new[] { new Vector3(-1.3f, 0f, -0.5f), new Vector3(1.3f, 0f, 2.9f), new Vector3(3.6f, 0f, -3.4f) }),
            ["PosRonda"] = ("Pos Ronda", new[] { new Vector3(0f, 0.5f, 0f), new Vector3(1.9f, 0f, -0.5f), new Vector3(-1.9f, 0f, 1f) }),
            ["KaliKering"] = ("Kali Kering", new[] { new Vector3(-20f, 0.05f, 1f), new Vector3(5f, 0.05f, -2f), new Vector3(24f, 0.05f, 0.5f) }),
        };

        public static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            foreach (var (index, name) in Layers)
            {
                var slot = layers.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(slot.stringValue)) slot.stringValue = name;
                else if (slot.stringValue != name)
                    Debug.LogWarning($"[Gameplay] Layer {index} sudah dipakai '{slot.stringValue}', bukan '{name}'.");
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ConfigurePrefabs(Dictionary<string, GameObject> prefabs, GreyboxBuilder.Palette palette)
        {
            GreyboxBuilder.EnsureFolder(ItemDataDir);
            GreyboxBuilder.EnsureFolder(GreyboxBuilder.PrefabDir + "/Items");
            GreyboxBuilder.EnsureFolder(GreyboxBuilder.PrefabDir + "/Creature");

            var partData = Parts.Select(spec => EnsurePart(spec, palette)).ToArray();

            Configure(prefabs["Player"], ConfigurePlayer);
            Configure(prefabs["Car"], root =>
            {
                var car = GetOrAdd<Car>(root);
                SetReferences(car, "requiredParts", partData);
            });

            foreach (var (id, info) in LandmarkInfo)
                Configure(prefabs[$"Landmark_{id}"], root => ConfigureLandmark(root, info.displayName, info.spawns));

            prefabs["Creature"] = GreyboxBuilder.EnsurePrefab("Creature/PF_Creature",
                (root, model) => BuildCreature(root, model, palette));
            Configure(prefabs["Creature"], ConfigureCreature);
        }

        public static void PopulateScene(Dictionary<string, GameObject> prefabs, Transform gameplay, GameObject player, GameObject carObject)
        {
            var creatureObject = GreyboxBuilder.Spawn(prefabs["Creature"], GreyboxBuilder.CreatureSpawn, Quaternion.identity, gameplay);

            var systems = new GameObject("Systems");
            var round = systems.AddComponent<RoundManager>();
            var spawner = systems.AddComponent<ItemSpawner>();
            var hud = systems.AddComponent<TemporaryHud>();

            var input = player.GetComponent<PlayerInputReader>();
            var motor = player.GetComponent<PlayerMotor>();
            var noise = player.GetComponent<PlayerNoise>();
            var inventory = player.GetComponent<PlayerInventory>();
            var interactor = player.GetComponent<PlayerInteractor>();
            var viewPoint = player.transform.Find("CameraRoot");
            var car = carObject.GetComponent<Car>();
            var creature = creatureObject.GetComponent<CreatureAI>();

            Wire(round, ("input", input), ("motor", motor), ("look", player.GetComponent<PlayerLook>()),
                ("inventory", inventory), ("interactor", interactor));
            Wire(spawner, ("car", car));
            Wire(creature, ("player", noise));
            Wire(hud, ("round", round), ("motor", motor), ("noise", noise), ("inventory", inventory),
                ("interactor", interactor), ("car", car), ("spawner", spawner), ("viewPoint", viewPoint), ("creature", creature));

            BakeNavMesh();
            BuildIntro(systems, player, carObject.transform,
                new Behaviour[] { motor, player.GetComponent<PlayerLook>(), inventory, interactor, round, creature, hud });
        }

        // ---------- Cutscene pembuka ----------

        /// <summary>
        /// Cutscene tanpa model karakter: kamera turun ke mobil, mendekati mobil, lalu menatap hutan
        /// tempat sepasang mata menyala sebentar. Posisi shot dihitung dari mobil dan titik muncul pemain.
        /// </summary>
        static void BuildIntro(GameObject systems, GameObject player, Transform car, Behaviour[] paused)
        {
            var camObject = new GameObject("CutsceneCamera");
            camObject.transform.SetParent(systems.transform, false);
            var cam = camObject.AddComponent<Camera>();
            var playerCam = player.GetComponentInChildren<Camera>(true);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = playerCam.backgroundColor;
            camObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camObject.AddComponent<AudioListener>();

            Vector3 carPos = car.position;
            Vector3 eye = player.transform.position + Vector3.up * 1.65f;
            Vector3 south = Vector3.back;

            var eyesRoot = BuildIntroEyes(systems.transform, eye, south);
            var eyeRenderers = eyesRoot.GetComponentsInChildren<Renderer>(true).Cast<Object>().ToArray();

            var intro = systems.AddComponent<IntroCutscene>();
            var so = new SerializedObject(intro);
            so.FindProperty("cutsceneCamera").objectReferenceValue = cam;
            so.FindProperty("playerCamera").objectReferenceValue = playerCam.gameObject;
            so.FindProperty("moonlight").objectReferenceValue = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Directional);
            so.ApplyModifiedPropertiesWithoutUndo();
            SetReferences(intro, "pausedDuringIntro", paused);
            SetReferences(intro, "eyes", eyeRenderers);

            var establishing = new[]
            {
                // Berakhir di dalam area kosong sekitar mobil (radius 14 m) supaya kamera tidak menembus tajuk pohon.
                new ShotSpec("Malam itu, mobilmu mogok di tepi alas.", 6.5f,
                    carPos + new Vector3(-14f, 18f, -32f), carPos + new Vector3(-5f, 5f, -11f), carPos + Vector3.up),
                new ShotSpec("Kunci, busi, aki, bensin… semuanya tertinggal di dalam hutan.", 6f,
                    carPos + new Vector3(-5.5f, 1.3f, -4.5f), carPos + new Vector3(-3.6f, 1.0f, -3.0f), carPos + new Vector3(0.5f, 0.8f, 0f)),
                new ShotSpec("Ada yang menunggu di antara pepohonan.", 7f,
                    eye, eye + south * 4f + Vector3.down * 0.05f, eyesRoot.position) { Eyes = true },
            };

            // Sudut pandang pemain: terduduk di samping mobil, berdiri, lalu berbalik ke hutan dan
            // berhenti tepat di pose kamera pemain, sehingga kontrol diserahkan tanpa potongan.
            float ground = carPos.y;
            Vector3 playerEye = playerCam.transform.position;
            Vector3 sitting = new(carPos.x + 5.2f, ground + 0.9f, carPos.z - 2.8f);
            Vector3 standing = playerEye + new Vector3(0.1f, 0f, 0.4f);
            Vector3 carSide = carPos + new Vector3(-2f, 1.1f, -0.4f);
            var pov = new[]
            {
                new ShotSpec("Mesinnya mati total.", 5.5f, sitting, sitting + Vector3.up * 0.05f,
                    sitting + new Vector3(-0.7f, -0.9f, -1.2f), carPos + new Vector3(2f, 1f, -0.4f)) { FadeOut = false, Flashlight = true },
                new ShotSpec("Satu-satunya jalan pulang: hidupkan lagi mobil ini.", 5f, sitting + Vector3.up * 0.05f, standing,
                    carPos + new Vector3(2f, 1f, -0.4f), carSide) { FadeIn = false, FadeOut = false, Flashlight = true },
                new ShotSpec("Dengarkan langkahmu sendiri.", 5f, standing, playerEye,
                    carSide, playerEye + playerCam.transform.forward * 10f) { FadeIn = false, FadeOut = false, Flashlight = true },
            };

            so = new SerializedObject(intro);
            WriteShots(so.FindProperty("shots"), establishing);
            WriteShots(so.FindProperty("povShots"), pov);
            so.FindProperty("povFlashlight").objectReferenceValue = BuildCutsceneFlashlight(camObject.transform, playerCam);
            var titles = so.FindProperty("titleLines");
            titles.arraySize = 2;
            titles.GetArrayElementAtIndex(0).stringValue = "Ia tidak melihat. Ia mendengar.";
            titles.GetArrayElementAtIndex(1).stringValue = "Sedikit itu cukup. Banyak itu petaka."; // tagline GDD
            so.ApplyModifiedPropertiesWithoutUndo();

            camObject.SetActive(false); // cutscene sendiri yang menyalakannya
        }

        class ShotSpec
        {
            public readonly string Caption;
            public readonly float Duration;
            public readonly Vector3 From, To, LookAt, LookAtEnd;
            public bool Eyes, Flashlight, FadeIn = true, FadeOut = true;

            public ShotSpec(string caption, float duration, Vector3 from, Vector3 to, Vector3 lookAt, Vector3 lookAtEnd = default)
            {
                Caption = caption;
                Duration = duration;
                From = from;
                To = to;
                LookAt = lookAt;
                LookAtEnd = lookAtEnd;
            }
        }

        static void WriteShots(SerializedProperty list, ShotSpec[] specs)
        {
            list.arraySize = specs.Length;
            for (int i = 0; i < specs.Length; i++)
            {
                var s = list.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("caption").stringValue = specs[i].Caption;
                s.FindPropertyRelative("duration").floatValue = specs[i].Duration;
                s.FindPropertyRelative("fromPosition").vector3Value = specs[i].From;
                s.FindPropertyRelative("toPosition").vector3Value = specs[i].To;
                s.FindPropertyRelative("lookAt").vector3Value = specs[i].LookAt;
                s.FindPropertyRelative("lookAtEnd").vector3Value = specs[i].LookAtEnd;
                s.FindPropertyRelative("showEyes").boolValue = specs[i].Eyes;
                s.FindPropertyRelative("fadeIn").boolValue = specs[i].FadeIn;
                s.FindPropertyRelative("fadeOut").boolValue = specs[i].FadeOut;
                s.FindPropertyRelative("flashlight").boolValue = specs[i].Flashlight;
            }
        }

        /// <summary>Salinan senter pemain di kamera cutscene, supaya shot sudut pandang pemain terlihat sama dengan saat bermain.</summary>
        static Light BuildCutsceneFlashlight(Transform cutsceneCamera, Camera playerCam)
        {
            var source = playerCam.GetComponentInChildren<Light>(true);
            var light = new GameObject("Flashlight").AddComponent<Light>();
            light.transform.SetParent(cutsceneCamera, false);
            light.transform.localPosition = source.transform.localPosition;
            light.type = source.type;
            light.range = source.range;
            light.spotAngle = source.spotAngle;
            light.innerSpotAngle = source.innerSpotAngle;
            light.intensity = source.intensity;
            light.color = source.color;
            light.shadows = source.shadows;
            light.shadowNearPlane = source.shadowNearPlane;
            light.enabled = false;
            return light;
        }

        /// <summary>Hanya sepasang mata, setinggi makhluk, di titik hutan yang tidak terhalang batang pohon.</summary>
        static Transform BuildIntroEyes(Transform parent, Vector3 viewer, Vector3 direction)
        {
            // Dekat dan agak besar dari mata makhluk sebenarnya, supaya tetap terbaca di layar kecil tanpa bloom.
            const float distance = 14f, eyeHeight = 2.8f;
            Physics.SyncTransforms(); // pohon baru saja ditanam; collider-nya harus terdaftar sebelum Linecast
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            Vector3 best = viewer + direction * distance;
            foreach (float offset in new[] { 0f, 2f, -2f, 4f, -4f, 6f, -6f, 8f, -8f })
            {
                Vector3 p = viewer + direction * distance + side * offset;
                p.y = TerrainBuilder.HeightAt(p) + eyeHeight;
                if (!Physics.Linecast(viewer, p, ~0, QueryTriggerInteraction.Ignore)) { best = p; break; }
            }

            var root = new GameObject("IntroEyes").transform;
            root.SetParent(parent, false);
            root.position = best;
            root.rotation = Quaternion.LookRotation(viewer - best);

            var material = AssetDatabase.LoadAssetAtPath<Material>(GreyboxBuilder.Root + "/Art/Materials/Greybox/M_Greybox_CreatureEye.mat");
            foreach (float x in new[] { -0.13f, 0.13f })
            {
                var e = GreyboxBuilder.Primitive(root, PrimitiveType.Sphere, "Eye", new Vector3(x, 0f, 0f), Vector3.one * 0.11f, material, collider: false);
                e.GetComponent<Renderer>().enabled = false;
            }
            return root;
        }

        // ---------- Prefab gameplay ----------

        static void ConfigurePlayer(GameObject root)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            root.layer = LayerMask.NameToLayer("Player");

            var viewPoint = root.transform.Find("CameraRoot");
            GetOrAdd<PlayerInputReader>(root);
            var motor = GetOrAdd<PlayerMotor>(root);
            var look = GetOrAdd<PlayerLook>(root);
            GetOrAdd<PlayerNoise>(root);
            var inventory = GetOrAdd<PlayerInventory>(root);
            var interactor = GetOrAdd<PlayerInteractor>(root);

            Wire(motor, ("cameraRoot", viewPoint));
            Wire(look, ("cameraRoot", viewPoint));
            Wire(inventory, ("viewPoint", viewPoint));
            Wire(interactor, ("viewPoint", viewPoint));

            var so = new SerializedObject(interactor);
            so.FindProperty("probeMask").intValue = ~LayerMask.GetMask("Player");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureLandmark(GameObject root, string displayName, Vector3[] spawnPositions)
        {
            var landmark = GetOrAdd<Landmark>(root);

            var spawnRoot = root.transform.Find("ItemSpawns");
            if (!spawnRoot)
            {
                spawnRoot = new GameObject("ItemSpawns").transform;
                spawnRoot.SetParent(root.transform, false);
                for (int i = 0; i < spawnPositions.Length; i++)
                {
                    var point = new GameObject($"ItemSpawn_{i + 1:00}").transform;
                    point.SetParent(spawnRoot, false);
                    point.localPosition = spawnPositions[i];
                }
            }

            var so = new SerializedObject(landmark);
            so.FindProperty("displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetReferences(landmark, "itemSpawnPoints", spawnRoot.Cast<Transform>().ToArray());
        }

        static ItemData EnsurePart(PartSpec spec, GreyboxBuilder.Palette palette)
        {
            string dataPath = $"{ItemDataDir}/ITEM_{spec.Id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<ItemData>(dataPath);
            if (!data)
            {
                data = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(data, dataPath);
                var so = new SerializedObject(data);
                so.FindProperty("displayName").stringValue = spec.DisplayName;
                so.FindProperty("weight").floatValue = spec.Weight;
                so.FindProperty("isCarPart").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var prefab = GreyboxBuilder.EnsurePrefab($"Items/PF_Item_{spec.Id}",
                (root, model) => GreyboxBuilder.Box(model, "Body", Vector3.up * (spec.Size.y / 2f), spec.Size, palette.ItemPart, collider: false));

            Configure(prefab, root => ConfigureItem(root, data, spec));
            Wire(data, ("worldPrefab", prefab));
            EditorUtility.SetDirty(data);
            return data;
        }

        static void ConfigureItem(GameObject root, ItemData data, PartSpec spec)
        {
            int itemLayer = LayerMask.NameToLayer("Item");
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = itemLayer;

            var body = GetOrAdd<Rigidbody>(root);
            body.mass = Mathf.Max(1f, data.Weight * 4f);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (!root.TryGetComponent<BoxCollider>(out _))
            {
                var box = root.AddComponent<BoxCollider>();
                box.size = spec.Size;
                box.center = Vector3.up * (spec.Size.y / 2f);
            }

            // Area pungut lebih besar dari bendanya, supaya barang kecil di tanah gelap mudah dibidik.
            if (!root.transform.Find("PickupArea"))
            {
                var area = new GameObject("PickupArea") { layer = itemLayer };
                area.transform.SetParent(root.transform, false);
                var sphere = area.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = 0.4f;
                sphere.center = Vector3.up * 0.15f;
            }

            Wire(GetOrAdd<Item>(root), ("data", data));
        }

        static void BuildCreature(GameObject root, Transform m, GreyboxBuilder.Palette p)
        {
            // Sosok tinggi kurus sekitar 3 m, lengan menjuntai hampir ke tanah (GDD).
            GreyboxBuilder.Primitive(m, PrimitiveType.Capsule, "Body", new Vector3(0f, 1.55f, 0f), new Vector3(0.55f, 1.05f, 0.4f), p.CreatureBody, collider: false);
            GreyboxBuilder.Primitive(m, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.8f, 0.05f), new Vector3(0.32f, 0.38f, 0.32f), p.CreatureBody, collider: false);
            GreyboxBuilder.Box(m, "ArmL", new Vector3(-0.36f, 1.45f, 0.05f), new Vector3(0.09f, 1.9f, 0.09f), p.CreatureBody, collider: false, euler: new Vector3(0f, 0f, -4f));
            GreyboxBuilder.Box(m, "ArmR", new Vector3(0.36f, 1.45f, 0.05f), new Vector3(0.09f, 1.9f, 0.09f), p.CreatureBody, collider: false, euler: new Vector3(0f, 0f, 4f));
            GreyboxBuilder.Primitive(m, PrimitiveType.Sphere, "EyeL", new Vector3(-0.08f, 2.84f, 0.19f), Vector3.one * 0.06f, p.CreatureEye, collider: false);
            GreyboxBuilder.Primitive(m, PrimitiveType.Sphere, "EyeR", new Vector3(0.08f, 2.84f, 0.19f), Vector3.one * 0.06f, p.CreatureEye, collider: false);
        }

        static void ConfigureCreature(GameObject root)
        {
            root.layer = LayerMask.NameToLayer("Creature");

            if (!root.TryGetComponent<NavMeshAgent>(out var agent))
            {
                agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.45f;
                agent.height = 3f;
                agent.angularSpeed = 300f;
                agent.acceleration = 10f;
                agent.stoppingDistance = 0.3f;
            }

            if (!root.TryGetComponent<CapsuleCollider>(out _))
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = 0.4f;
                capsule.height = 3f;
                capsule.center = Vector3.up * 1.5f;
            }

            var ai = GetOrAdd<CreatureAI>(root);
            var eyes = root.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Eye")).Cast<Object>().ToArray();
            SetReferences(ai, "eyes", eyes);
        }

        // ---------- Navmesh ----------

        static void BakeNavMesh()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            settings.FindProperty("m_Settings").GetArrayElementAtIndex(0).FindPropertyRelative("agentClimb").floatValue = AgentClimb;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var surface = new GameObject("NavMesh").AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default");
            surface.BuildNavMesh();

            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }

        // ---------- Helper ----------

        static void Configure(GameObject prefab, Action<GameObject> configure)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            configure(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();

        static void Wire(Object target, params (string field, Object value)[] references)
        {
            foreach (var (field, value) in references)
                GreyboxBuilder.SetReference(target, field, value);
        }

        static void SetReferences(Object target, string field, IReadOnlyList<Object> values)
        {
            var so = new SerializedObject(target);
            var array = so.FindProperty(field);
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
