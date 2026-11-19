using System;
using System.Collections.Generic;
using System.Linq;
using TheAlas.Audio;
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

        // Layer 8–10 dipakai supaya navmesh dan interaksi bisa mengabaikan pemain, barang, dan makhluk.
        static readonly (int index, string name)[] Layers = { (8, "Player"), (9, "Item"), (10, "Creature") };

        /// <summary>Penanjakan agen 0,5 m supaya makhluk setinggi 3 m bisa melangkahi batang tumbang 0,45 m.</summary>
        const float AgentClimb = 0.5f;

        readonly struct PartSpec
        {
            public readonly string Id, DisplayName;
            public readonly float Weight;
            public readonly Vector3 Size;
            /// <summary>Placeholder silinder rebah dengan sumbu X (roda atau pipa), bukan kotak.</summary>
            public readonly bool IsCylinder;

            public PartSpec(string id, string displayName, float weight, Vector3 size, bool isCylinder = false)
            {
                Id = id;
                DisplayName = displayName;
                Weight = weight;
                Size = size;
                IsCylinder = isCylinder;
            }
        }

        // Daftar dan urutannya mengikuti kertas tugas: Filter Oli, Stir Kemudi, Kunci Mobil, Knalpot, Aki Mobil.
        // Knalpot belum punya model, jadi placeholder-nya pipa (silinder rebah).
        static readonly PartSpec[] Parts =
        {
            new("FilterOli", "Filter Oli", 0.5f, new Vector3(0.12f, 0.16f, 0.12f)),
            new("Stir", "Stir Kemudi", 1.5f, new Vector3(0.4f, 0.4f, 0.1f)),
            new("KunciMobil", "Kunci Mobil", 0.5f, new Vector3(0.26f, 0.06f, 0.12f)),
            new("Knalpot", "Knalpot", 2.5f, new Vector3(0.8f, 0.14f, 0.14f), isCylinder: true),
            new("Aki", "Aki Mobil", 3f, new Vector3(0.34f, 0.22f, 0.2f)),
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

            prefabs["Creature"] = GreyboxBuilder.RequirePrefab("Creature/PF_Creature");
            Configure(prefabs["Creature"], ConfigureCreature);

            ConfigureAudio(prefabs);
        }

        /// <summary>Area navmesh dan lamanya ronde, yang berbeda antara map greybox dan map Gung Wah.</summary>
        public readonly struct MapSpec
        {
            public readonly Vector3 CreatureSpawn, NavCenter, NavSize;
            public readonly float RoundDuration;
            public readonly string NavMeshPath;

            public MapSpec(Vector3 creatureSpawn, Vector3 navCenter, Vector3 navSize, float roundDuration, string navMeshPath)
            {
                CreatureSpawn = creatureSpawn;
                NavCenter = navCenter;
                NavSize = navSize;
                RoundDuration = roundDuration;
                NavMeshPath = navMeshPath;
            }
        }

        public static void PopulateScene(Dictionary<string, GameObject> prefabs, Transform gameplay, GameObject player, GameObject carObject, MapSpec map)
        {
            var creatureObject = GreyboxBuilder.Spawn(prefabs["Creature"], map.CreatureSpawn, Quaternion.identity, gameplay);

            var systems = new GameObject("Systems");
            var round = systems.AddComponent<RoundManager>();
            var spawner = systems.AddComponent<ItemSpawner>();
            var hud = systems.AddComponent<GameHud>();
            var skin = EnsureUiSkin();
            var look = player.GetComponent<PlayerLook>();

            var input = player.GetComponent<PlayerInputReader>();
            var motor = player.GetComponent<PlayerMotor>();
            var noise = player.GetComponent<PlayerNoise>();
            var inventory = player.GetComponent<PlayerInventory>();
            var interactor = player.GetComponent<PlayerInteractor>();
            var viewPoint = player.transform.Find("CameraRoot");
            var car = carObject.GetComponent<Car>();
            var creature = creatureObject.GetComponent<CreatureAI>();

            Wire(round, ("input", input), ("motor", motor), ("look", look),
                ("inventory", inventory), ("interactor", interactor));
            var roundSettings = new SerializedObject(round);
            roundSettings.FindProperty("duration").floatValue = map.RoundDuration;
            roundSettings.ApplyModifiedPropertiesWithoutUndo();
            Wire(spawner, ("car", car));
            Wire(creature, ("player", noise), ("inventory", inventory));
            Wire(hud, ("skin", skin), ("round", round), ("motor", motor), ("noise", noise), ("inventory", inventory),
                ("interactor", interactor), ("look", look), ("car", car), ("spawner", spawner), ("viewPoint", viewPoint), ("creature", creature));

            Wire(systems.AddComponent<MicrophoneInput>(), ("noise", noise));
            var settings = systems.AddComponent<SettingsApplier>();
            Wire(settings, ("look", look), ("playerCamera", viewPoint.GetComponent<Camera>()),
                ("flashlight", player.GetComponentInChildren<Flashlight>().GetComponent<Light>()), ("ground", Object.FindFirstObjectByType<Terrain>()));

            BuildAtmosphere(systems, creature);
            var scareAudio = new GameObject("JumpscareAudio");
            scareAudio.transform.SetParent(systems.transform, false);
            Wire(systems.AddComponent<Jumpscare>(), ("creature", creature), ("viewPoint", viewPoint),
                ("source", EnsureSource(scareAudio, spatial: 0f)), ("scream", Clip("SFX/SFX_Creature_Growl_01")));
            BakeNavMesh(map);
            var intro = BuildIntro(systems, player, carObject.transform,
                new Behaviour[] { motor, look, inventory, interactor, round, creature,
                    creatureObject.GetComponent<CreatureAudio>(), hud });
            Wire(intro, ("skin", skin));
            var menuMusic = new GameObject("MenuMusic");
            menuMusic.transform.SetParent(systems.transform, false);
            var menuSource = EnsureSource(menuMusic, spatial: 0f);
            menuSource.clip = Clip("Music/MUS_MainMenu_Loop");
            menuSource.loop = true;
            Wire(systems.AddComponent<MainMenu>(), ("skin", skin), ("intro", intro), ("music", menuSource));
        }

        // ---------- UI (desain Citari) ----------

        const string UiSpriteDir = GreyboxBuilder.Root + "/UI/Sprites";

        /// <summary>
        /// Aset UiSkin berisi semua gambar UI. Teksturnya diimpor tanpa mipmap dan tanpa pembulatan
        /// ukuran supaya tetap tajam dan koordinat mockup cocok satu banding satu.
        /// </summary>
        static UiSkin EnsureUiSkin()
        {
            const string path = GreyboxBuilder.Root + "/UI/UI_Skin.asset";
            var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(path);
            if (!skin)
            {
                skin = ScriptableObject.CreateInstance<UiSkin>();
                AssetDatabase.CreateAsset(skin, path);
            }

            Texture2D T(string name)
            {
                string texPath = $"{UiSpriteDir}/UI_{name}.png";
                if (AssetImporter.GetAtPath(texPath) is TextureImporter importer && importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.alphaIsTransparency = true;
                    importer.maxTextureSize = 2048;
                    importer.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            }

            skin.menu = T("Menu");
            skin.menuPlayHover = T("Menu_Play_Hover");
            skin.menuSettingHover = T("Menu_Setting_Hover");
            skin.menuQuitHover = T("Menu_Quit_Hover");
            skin.story = T("Story");
            skin.storySkipHover = T("Story_Skip_Hover");
            skin.gameOver = T("GameOver");
            skin.escape = T("Escape");
            skin.endUlangHover = T("End_Ulang_Hover");
            skin.endMenuHover = T("End_Menu_Hover");
            skin.settings = T("Settings");
            skin.settingsCheck = T("Settings_Check");
            skin.hudSlot = T("Hud_Slot");
            skin.hudSuara = T("Hud_Suara");
            skin.hudNafas = T("Hud_Nafas");
            skin.hudHints = T("Hud_Hints");
            skin.taskPaper = T("Task_Paper");
            skin.taskBox = T("Task_Box");
            skin.taskChecks = Enumerable.Range(1, 5).Select(i => T($"Task_Check_{i:00}")).ToArray();
            // Tulisan di kertas tugas, urut sama dengan Parts.
            skin.taskLabels = new[] { "Oli", "Stir", "Kunci", "Knalpot", "Aki" }.Select(n => T($"Task_{n}")).ToArray();
            EditorUtility.SetDirty(skin);
            return skin;
        }

        // ---------- Audio (rekaman Jaya) ----------

        const string AudioDir = GreyboxBuilder.Root + "/Audio";

        static AudioClip[] Sfx(params string[] names) =>
            names.Select(name => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/SFX/SFX_{name}.wav")).Where(clip => clip).ToArray();

        static AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{path}.wav");

        /// <summary>Klip barang menurut bahannya.</summary>
        static readonly Dictionary<string, string> ItemMaterial = new()
        {
            ["Aki"] = "Metal", ["FilterOli"] = "Metal", ["JerikenBensin"] = "Metal", ["KunciMobil"] = "Metal",
            // Belum ada rekaman karet; ban memakai bunyi kayu yang paling mirip dentum tumpul.
            ["Ban"] = "Wood", ["Stir"] = "Wood",
        };

        static void ConfigureAudio(Dictionary<string, GameObject> prefabs)
        {
            ConfigureAudioImport();

            Configure(prefabs["Player"], root =>
            {
                var audio = GetOrAdd<PlayerAudio>(root);
                Wire(audio, ("source", EnsureSource(root, spatial: 0f)));
                SetReferences(audio, "groundSteps", Sfx("Step_Leaves_01", "Step_Ground_01"));
                SetReferences(audio, "gravelSteps", Sfx("Step_Gravel_01"));
                SetReferences(audio, "bushSteps", Sfx("Bush_Rustle_01", "Bush_Rustle_02", "Bush_Rustle_03"));
                SetReferences(audio, "heavySteps", Sfx("Step_Heavy_01"));
                SetReferences(audio, "breathClips", Sfx("Player_Breath_01"));
                SetReferences(audio, "recoverClips", Sfx("Player_BreathCalm_01"));
            });

            Configure(prefabs["Creature"], root =>
            {
                var audio = GetOrAdd<CreatureAudio>(root);
                Wire(audio, ("source", EnsureSource(root, spatial: 1f, minDistance: 2f, maxDistance: 45f)));
                SetReferences(audio, "growlClips", Sfx("Creature_Growl_01", "Creature_Growl_02"));
                SetReferences(audio, "sniffClips", Sfx("Creature_Sniff_01"));
            });

            Configure(prefabs["Car"], root =>
            {
                var car = root.GetComponent<Car>();
                Wire(car, ("audioSource", EnsureSource(root, spatial: 1f, minDistance: 3f, maxDistance: 60f)));
                SetReferences(car, "trunkClips", Sfx("Car_Trunk_01"));
                SetReferences(car, "doorClips", Sfx("Car_Door_01"));
                SetReferences(car, "engineClips", Sfx("Car_EngineStart_01"));
                SetReferences(car, "starterFailClips", Sfx("Car_StarterFail_01"));
            });

            // Belum ada rekaman ranting patah; sementara bunyi kayu jatuh, dinaikkan nadanya oleh variasi acak.
            Configure(prefabs["Twig_01"], root => SetReferences(root.GetComponent<Twig>(), "snapClips", Sfx("Item_Wood_Drop_01")));

            foreach (var (id, material) in ItemMaterial)
            {
                var data = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemDataDir}/ITEM_{id}.asset");
                if (!data) continue;
                bool wood = material == "Wood";
                SetReferences(data, "pickupClips", Sfx(wood ? "Item_Wood_Pickup_01" : "Item_Metal_Pickup_01"));
                SetReferences(data, "impactClips", Sfx(wood ? "Item_Wood_Drop_01" : "Item_Metal_Drop_01"));
                EditorUtility.SetDirty(data);
            }
        }

        /// <summary>
        /// Efek suara mono (diposisikan 3D, ukuran setengah), ambience dan musik stereo dengan kompresi
        /// lebih kuat supaya unduhan WebGL tetap kecil.
        /// </summary>
        static void ConfigureAudioImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;

                bool isSfx = path.Contains("/SFX/");
                var settings = importer.defaultSampleSettings;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = isSfx ? 0.6f : 0.4f;
                settings.loadType = isSfx ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
                if (importer.forceToMono == isSfx && importer.defaultSampleSettings.quality == settings.quality) continue;

                importer.forceToMono = isSfx;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        static AudioSource EnsureSource(GameObject go, float spatial, float minDistance = 1f, float maxDistance = 30f)
        {
            var source = GetOrAdd<AudioSource>(go);
            source.playOnAwake = false;
            source.spatialBlend = spatial;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            return source;
        }

        /// <summary>Ambience berulang, detak jantung, musik bahaya, dan bunyi akhir ronde (2D) di objek Systems.</summary>
        static void BuildAtmosphere(GameObject systems, CreatureAI creature)
        {
            var root = new GameObject("Audio");
            root.transform.SetParent(systems.transform, false);

            AudioSource Source(string name, AudioClip clip, float volume, bool loop)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                var source = EnsureSource(go, spatial: 0f);
                source.clip = clip;
                source.volume = volume;
                source.loop = loop;
                return source;
            }

            var atmosphere = systems.AddComponent<AtmosphereAudio>();
            Wire(atmosphere, ("creature", creature),
                ("heartbeatSource", Source("Heartbeat", null, 1f, false)),
                ("heartbeatClip", Clip("SFX/SFX_Player_Heartbeat_01")),
                ("dangerMusic", Source("DangerMusic", Clip("Music/MUS_Danger_Loop"), 0f, true)),
                ("stingerSource", Source("Stinger", null, 1f, false)),
                ("winClip", Clip("Music/MUS_Win")),
                ("caughtClip", Clip("SFX/SFX_Creature_Devour_01")));
            SetReferences(atmosphere, "ambienceLoops", new Object[]
            {
                Source("AmbienceForest", Clip("Ambience/AMB_Forest_Night_Loop"), 0.6f, true),
                Source("AmbienceCrickets", Clip("Ambience/AMB_Crickets_Loop"), 0.2f, true),
                Source("AmbienceCicada", Clip("Ambience/AMB_Cicada_Loop"), 0.12f, true),
            });
            SetReferences(atmosphere, "scareClips", ScareClips());
        }

        internal static Object[] ScareClips() => new Object[]
        {
            Clip("SFX/SFX_Creature_Growl_01"), Clip("SFX/SFX_Creature_Growl_02"), Clip("SFX/SFX_Creature_Sniff_01"),
            Clip("SFX/SFX_Bush_Rustle_01"), Clip("SFX/SFX_Bush_Rustle_02"), Clip("SFX/SFX_Bush_Rustle_03"),
        };

        // ---------- Cutscene pembuka ----------

        /// <summary>
        /// Cutscene tanpa model karakter: kamera turun ke mobil, mendekati mobil, lalu menatap hutan
        /// tempat sepasang mata menyala sebentar. Posisi shot dihitung dari mobil dan titik muncul pemain.
        /// </summary>
        static IntroCutscene BuildIntro(GameObject systems, GameObject player, Transform car, Behaviour[] paused)
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
                new ShotSpec("Filter oli, stir, kunci, knalpot, aki… semuanya tercecer di dalam hutan.", 6f,
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
            return intro;
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

        internal static void ConfigureLandmark(GameObject root, string displayName, Vector3[] spawnPositions)
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
            }
            var settings = new SerializedObject(data);
            settings.FindProperty("displayName").stringValue = spec.DisplayName;
            settings.FindProperty("weight").floatValue = spec.Weight;
            settings.FindProperty("isCarPart").boolValue = true;
            settings.FindProperty("icon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{UiSpriteDir}/Items/UI_Icon_{spec.Id}.png");
            settings.ApplyModifiedPropertiesWithoutUndo();

            var prefab = GreyboxBuilder.EnsurePrefab($"Items/PF_Item_{spec.Id}", (root, model) =>
            {
                if (spec.IsCylinder)
                    GreyboxBuilder.Cylinder(model, "Body", Vector3.up * (spec.Size.y / 2f), spec.Size.y, spec.Size.x, palette.ItemPart,
                        collider: false, euler: new Vector3(0f, 0f, 90f));
                else
                    GreyboxBuilder.Box(model, "Body", Vector3.up * (spec.Size.y / 2f), spec.Size, palette.ItemPart, collider: false);
            });

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

            if (!root.transform.Find("Sparkle")) AddSparkle(root.transform, itemLayer);
            Wire(GetOrAdd<Item>(root), ("data", data));
        }

        const string SparkleMaterialPath = GreyboxBuilder.Root + "/Art/Materials/M_Item_Sparkle.mat";

        /// <summary>
        /// Kilau kecil kekuningan di sekitar barang supaya pemain tahu benda itu bisa dipungut.
        /// Ikut hilang saat barang dipungut karena objeknya dinonaktifkan.
        /// </summary>
        static void AddSparkle(Transform root, int layer)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SparkleMaterialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.SetFloat("_Surface", 1f); // transparan
                material.SetFloat("_Blend", 2f);   // additive
                material.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
                material.SetColor("_BaseColor", Color.white);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_ZWrite", 0);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                AssetDatabase.CreateAsset(material, SparkleMaterialPath);
            }

            var go = new GameObject("Sparkle") { layer = layer };
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.up * 0.2f;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.97f, 0.8f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            main.gravityModifier = -0.02f;

            var emission = particles.emission;
            emission.rateOverTime = 9f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void ConfigureCreature(GameObject root)
        {
            root.layer = LayerMask.NameToLayer("Creature");

            if (!root.TryGetComponent<NavMeshAgent>(out var agent))
            {
                agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.45f;
                agent.height = 3f;
                agent.angularSpeed = 120f; // diatur ulang CreatureAI menurut keadaan
                agent.acceleration = 20f;  // cukup tinggi supaya tidak mengitari pemain karena kebablasan
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
            GetOrAdd<CreatureAnimator>(root);
        }

        // ---------- Navmesh ----------

        static void BakeNavMesh(MapSpec map)
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            settings.FindProperty("m_Settings").GetArrayElementAtIndex(0).FindPropertyRelative("agentClimb").floatValue = AgentClimb;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var surface = new GameObject("NavMesh").AddComponent<NavMeshSurface>();
            // Hanya area main di dalam tembok; hutan pemandangan di luar tembok tidak boleh dijelajahi makhluk.
            surface.collectObjects = CollectObjects.Volume;
            surface.center = map.NavCenter;
            surface.size = map.NavSize;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default");
            surface.BuildNavMesh();

            AssetDatabase.DeleteAsset(map.NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, map.NavMeshPath);
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
