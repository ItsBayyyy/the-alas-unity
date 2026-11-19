using System.Collections.Generic;
using System.Linq;
using TheAlas.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace TheAlas.EditorTools
{
    /// <summary>
    /// Map permainan dari scene buatan Gung Wah, persis ukuran dan posisinya: terrain 780 × 889 m,
    /// rumah, sumur, peternakan, kincir angin, pagar, pohon conifer, dan rumputnya.
    ///
    /// Scene sumber (<c>Scenes/GungWah/GungWah_Source.unity</c>) dibuka, kamera, cahaya, volume, dan alat
    /// editor Gung Wah dibuang, objeknya diberi collider, lalu gameplay ditata di atasnya: mobil, tujuh
    /// landmark (empat objek Gung Wah dan tiga dari GDD), semak dan ranting, tembok batas, navmesh, dan
    /// sistem ronde. Hasilnya disimpan sebagai <c>Hutan.unity</c>; scene sumber tidak diubah.
    /// </summary>
    public static class GungWahMapBuilder
    {
        const string SourceScene = GreyboxBuilder.SceneDir + "/GungWah/GungWah_Source.unity";
        const string ScenePath = GreyboxBuilder.SceneDir + "/Hutan.unity";
        /// <summary>Jarak antarobjek di map ini ratusan meter, jadi ronde dua kali GDD.</summary>
        const float RoundDuration = 900f;
        const int Seed = 1911;

        // Mobil di lembah datar di utara rumah; landmark terjauh (sumur) sekitar 450 m.
        static readonly Vector3 CarPosition = new(-60f, 0f, 120f);
        static readonly Vector3 PlayerSpawn = CarPosition + new Vector3(3.5f, 0f, -5f);
        const float CarClearRadius = 15f;

        /// <summary>Objek di scene Gung Wah yang menjadi landmark, dengan nama tampilannya.</summary>
        static readonly (string objectName, string displayName)[] GungWahLandmarks =
        {
            ("Abandoned_House", "Rumah Kosong"),
            ("well (1)", "Sumur Tua"),
            ("OldShed", "Peternakan"),
            ("WindMill", "Kincir Angin"),
        };

        static readonly string[] GddLandmarks = { "PohonBeringin", "MakamKeramat", "PosRonda" };

        // Titik muncul barang di sisi landmark yang menghadap mobil (lokal: +Z ke arah mobil).
        static readonly Vector3[] SpawnOffsets = { new(-3f, 0f, 0f), new(3f, 0f, 0.5f), new(0f, 0f, 2.5f) };

        [MenuItem("Tools/The Alas/Bangun Map Gung Wah")]
        static void BuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Bangun map Gung Wah?",
                    "Hutan.unity dibangun ulang dari scene Gung Wah; perubahan manual di Hutan.unity hilang.", "Bangun", "Batal"))
                return;
            Build();
        }

        public static void Build()
        {
            GameplaySetup.EnsureLayers();
            var palette = new GreyboxBuilder.Palette();
            var prefabs = GreyboxBuilder.CreatePrefabs(palette);
            GameplaySetup.ConfigurePrefabs(prefabs, palette);

            var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name is "Main Camera" or "Directional Light" or "Global Volume" or "Spawner")
                    Object.DestroyImmediate(root);

            var terrainObject = GameObject.Find("Terrain");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(terrainObject);
            var terrain = terrainObject.GetComponent<Terrain>();
            TerrainBuilder.Use(terrain);
            GreyboxBuilder.SetupLighting();

            var obstacles = AddColliders(terrainObject.transform);
            var rng = new Random(Seed);
            var landmarks = new GameObject("Landmarks").transform;
            var reserved = new List<(Vector3 center, float radius)> { (CarPosition, CarClearRadius) };

            foreach (var (objectName, displayName) in GungWahLandmarks)
                reserved.Add(PlaceAtObject(GameObject.Find(objectName), displayName, landmarks));
            foreach (var id in GddLandmarks)
                reserved.Add(PlaceInOpen(prefabs[$"Landmark_{id}"], terrain, obstacles, reserved, landmarks));

            ClearTrees(terrain, reserved);
            var environment = new GameObject("Environment").transform;
            Scatter(prefabs, terrain, obstacles, reserved, environment, rng);
            BuildBounds(terrain, environment);
            GreyboxBuilder.MarkStatic(environment);
            GreyboxBuilder.MarkStatic(landmarks);

            var gameplay = new GameObject("Gameplay").transform;
            var car = GreyboxBuilder.Spawn(prefabs["Car"], CarPosition, Quaternion.Euler(0f, 90f, 0f), gameplay);
            var player = GreyboxBuilder.Spawn(prefabs["Player"], PlayerSpawn, Quaternion.Euler(0f, 180f, 0f), gameplay);

            var size = terrain.terrainData.size;
            var origin = terrain.transform.position;
            GameplaySetup.PopulateScene(prefabs, gameplay, player, car, new GameplaySetup.MapSpec(
                FindCreatureSpawn(terrain, obstacles, reserved),
                origin + new Vector3(size.x / 2f, 150f, size.z / 2f), new Vector3(size.x - 4f, 320f, size.z - 4f),
                RoundDuration, GreyboxBuilder.SceneDir + "/Hutan_NavMesh.asset"));

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[MapGungWah] Scene tersimpan di {ScenePath}");
        }

        /// <summary>
        /// Objek Gung Wah tanpa collider (bisa ditembus) diberi MeshCollider per mesh. Mengembalikan
        /// batas tiap objek, dipakai supaya landmark, semak, dan ranting tidak muncul di dalamnya.
        /// </summary>
        static List<Bounds> AddColliders(Transform terrainObject)
        {
            var bounds = new List<Bounds>();
            foreach (Transform placed in terrainObject)
            {
                foreach (var filter in placed.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh && !filter.GetComponent<Collider>())
                        filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;

                if (MainBounds(placed.gameObject) is { } b) bounds.Add(b);
            }
            return bounds;
        }

        /// <summary>
        /// Batas bagian utama objek: mesh terbesar ditambah mesh lain di dekatnya. Model rumah membawa
        /// potongan kecil yang tercecer jauh (radio di z 1002, di luar map); potongan seperti itu
        /// dibiarkan di scene, tapi tidak dihitung, supaya batasnya tidak melebar sampai 1 km.
        /// </summary>
        static Bounds? MainBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>().Where(r => r.bounds.size.sqrMagnitude > 0.01f).ToArray();
            if (renderers.Length == 0) return null;
            var main = renderers.OrderByDescending(r => r.bounds.size.x * r.bounds.size.z).First().bounds;
            float reach = main.extents.magnitude * 1.5f;
            var b = main;
            foreach (var r in renderers)
                if (Vector3.Distance(r.bounds.center, main.center) < reach) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Landmark di sisi objek Gung Wah yang menghadap mobil, 6 m di luar batas objeknya.</summary>
        static (Vector3, float) PlaceAtObject(GameObject target, string displayName, Transform parent)
        {
            var b = MainBounds(target).Value;

            // Titik tepi objek yang paling dekat ke mobil, lalu 6 m keluar ke arah mobil.
            var edge = new Vector3(Mathf.Clamp(CarPosition.x, b.min.x, b.max.x), 0f, Mathf.Clamp(CarPosition.z, b.min.z, b.max.z));
            Vector3 toCar = GreyboxBuilder.Flat(CarPosition - edge).normalized;
            return CreateLandmark(displayName, edge + toCar * 6f, Quaternion.LookRotation(toCar), parent);
        }

        static (Vector3, float) CreateLandmark(string displayName, Vector3 position, Quaternion rotation, Transform parent)
        {
            var go = new GameObject($"Landmark {displayName}");
            go.transform.SetParent(parent, false);
            position.y = TerrainBuilder.HeightAt(position);
            go.transform.SetPositionAndRotation(position, rotation);
            GameplaySetup.ConfigureLandmark(go, displayName, SpawnOffsets);
            SnapToGround(go.transform.Find("ItemSpawns"));
            return (position, 8f);
        }

        /// <summary>Landmark GDD (prefab) di tanah datar terbuka, 90–260 m dari mobil, sejauh mungkin dari landmark lain.</summary>
        static (Vector3, float) PlaceInOpen(GameObject prefab, Terrain terrain, List<Bounds> obstacles,
            List<(Vector3 center, float radius)> reserved, Transform parent)
        {
            Vector3 best = Vector3.zero;
            float bestScore = float.MinValue;
            foreach (var p in Candidates(terrain, 12f, 10f))
            {
                float toCar = GreyboxBuilder.Flat(p - CarPosition).magnitude;
                if (toCar < 90f || toCar > 260f || Blocked(p, obstacles, 15f)) continue;
                float nearest = reserved.Min(r => GreyboxBuilder.Flat(p - r.center).magnitude);
                if (nearest < 70f) continue;
                if (nearest > bestScore) { bestScore = nearest; best = p; }
            }

            Vector3 toCarDir = GreyboxBuilder.Flat(CarPosition - best);
            var landmark = GreyboxBuilder.Spawn(prefab, best, Quaternion.LookRotation(toCarDir), parent);
            SnapToGround(landmark.transform.Find("ItemSpawns"));
            return (best, 13f);
        }

        /// <summary>Makhluk mulai di tanah datar sekitar 250 m dari mobil, jauh dari pemain seperti di GDD.</summary>
        static Vector3 FindCreatureSpawn(Terrain terrain, List<Bounds> obstacles, List<(Vector3 center, float radius)> reserved)
        {
            return Candidates(terrain, 10f, 10f)
                .Where(p => !Blocked(p, obstacles, 5f) && reserved.All(r => GreyboxBuilder.Flat(p - r.center).magnitude > r.radius + 5f))
                .OrderBy(p => Mathf.Abs(GreyboxBuilder.Flat(p - CarPosition).magnitude - 250f))
                .First();
        }

        /// <summary>
        /// Titik-titik grid di terrain yang kemiringannya di bawah batas, 20 m dari tepi. Y = 0: tinggi tanah
        /// ditambahkan oleh <see cref="GreyboxBuilder.Spawn"/>, sama seperti tata letak map greybox.
        /// </summary>
        static IEnumerable<Vector3> Candidates(Terrain terrain, float maxSlope, float step)
        {
            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            for (float x = 20f; x <= data.size.x - 20f; x += step)
            for (float z = 20f; z <= data.size.z - 20f; z += step)
            {
                if (data.GetSteepness(x / data.size.x, z / data.size.z) > maxSlope) continue;
                yield return new Vector3(origin.x + x, 0f, origin.z + z);
            }
        }

        static bool Blocked(Vector3 p, List<Bounds> obstacles, float margin) =>
            obstacles.Any(b => p.x > b.min.x - margin && p.x < b.max.x + margin && p.z > b.min.z - margin && p.z < b.max.z + margin);

        /// <summary>
        /// Titik muncul yang tertanam di lereng dinaikkan ke permukaan tanah, supaya barang tidak jatuh
        /// menembus terrain. Titik yang memang di atas tanah (misalnya di lantai pos ronda) dibiarkan.
        /// </summary>
        static void SnapToGround(Transform spawnRoot)
        {
            foreach (Transform point in spawnRoot)
            {
                var p = point.position;
                p.y = Mathf.Max(p.y, TerrainBuilder.HeightAt(p));
                point.position = p;
            }
        }

        /// <summary>Pohon conifer yang menimpa mobil, titik muncul pemain, atau landmark dipindah keluar (dihapus).</summary>
        static void ClearTrees(Terrain terrain, List<(Vector3 center, float radius)> reserved)
        {
            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            data.treeInstances = data.treeInstances.Where(tree =>
            {
                var world = origin + Vector3.Scale(tree.position, data.size);
                return reserved.All(r => GreyboxBuilder.Flat(world - r.center).magnitude > r.radius);
            }).ToArray();
            EditorUtility.SetDirty(data);
        }

        /// <summary>
        /// Mekanik suara GDD di seluruh area main: semak (berisik dilewati), ranting (patah saat diinjak),
        /// dan batang tumbang (harus dilompati atau diputari). Kerapatannya mengikuti map greybox.
        /// </summary>
        static void Scatter(Dictionary<string, GameObject> prefabs, Terrain terrain, List<Bounds> obstacles,
            List<(Vector3 center, float radius)> reserved, Transform environment, Random rng)
        {
            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            var bushes = GreyboxBuilder.Variants("PF_Bush_", prefabs["Bush_01"]);

            void Place(string group, int count, float maxSlope, System.Action<Vector3, Transform> spawn)
            {
                var parent = new GameObject(group).transform;
                parent.SetParent(environment, false);
                for (int placed = 0, tries = 0; placed < count && tries < count * 20; tries++)
                {
                    float u = GreyboxBuilder.Range(rng, 0.03f, 0.97f), v = GreyboxBuilder.Range(rng, 0.03f, 0.97f);
                    if (data.GetSteepness(u, v) > maxSlope) continue;
                    var p = origin + new Vector3(u * data.size.x, 0f, v * data.size.z);
                    if (Blocked(p, obstacles, 2f) || reserved.Any(r => GreyboxBuilder.Flat(p - r.center).magnitude < r.radius)) continue;
                    spawn(p, parent);
                    placed++;
                }
            }

            Place("Bushes", 900, 30f, (p, parent) => GreyboxBuilder.SpawnScaled(bushes[rng.Next(bushes.Length)], p, rng, parent, 0.8f, 1.4f));
            Place("Twigs", 1400, 30f, (p, parent) => GreyboxBuilder.SpawnScaled(prefabs["Twig_01"], p, rng, parent, 0.8f, 1.2f));
            Place("Logs", 180, 15f, (p, parent) => GreyboxBuilder.Spawn(prefabs["Log_01"], p, Quaternion.Euler(0f, GreyboxBuilder.Range(rng, 0f, 360f), 0f), parent));
        }

        /// <summary>Tembok tak terlihat di tepi terrain, cukup tinggi untuk menutup gunung di utara.</summary>
        static void BuildBounds(Terrain terrain, Transform parent)
        {
            var bounds = new GameObject("MapBounds").transform;
            bounds.SetParent(parent, false);
            var size = terrain.terrainData.size;
            var o = terrain.transform.position;
            const float inset = 1.5f, height = 400f;
            float cy = o.y + height / 2f - 20f;
            GreyboxBuilder.AddWall(bounds, new Vector3(o.x + size.x / 2f, cy, o.z + inset), new Vector3(size.x, height, 1f));
            GreyboxBuilder.AddWall(bounds, new Vector3(o.x + size.x / 2f, cy, o.z + size.z - inset), new Vector3(size.x, height, 1f));
            GreyboxBuilder.AddWall(bounds, new Vector3(o.x + inset, cy, o.z + size.z / 2f), new Vector3(1f, height, size.z));
            GreyboxBuilder.AddWall(bounds, new Vector3(o.x + size.x - inset, cy, o.z + size.z / 2f), new Vector3(1f, height, size.z));
        }
    }
}
