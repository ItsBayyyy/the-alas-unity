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
    /// Map permainan dari scene buatan Gung Wah dengan susunan persis: terrain, rumah, sumur, peternakan,
    /// kincir angin, pagar, pohon conifer, dan rumputnya.
    ///
    /// Map Gung Wah dibangun sekitar 6× lebih besar dari ukuran nyata (rumah 230 m, sumur 32 m) sehingga
    /// pemain 1,8 m terlihat seperti semut. Seluruh map diperkecil seragam dengan <see cref="MapScale"/>:
    /// terrain, posisi, dan ukuran semua objek ikut, jadi susunannya tetap sama, objeknya berukuran nyata,
    /// dan luas map (±124 × 141 m) dekat dengan area main GDD. Pohon ikut diperkecil (±14 m).
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
        const string ScaledTerrainPath = GreyboxBuilder.SceneDir + "/GungWah/GungWah_Terrain_Scaled.asset";
        /// <summary>Rumah Gung Wah diskalakan 6,3×; faktor ini mengembalikannya ke ukuran asli modelnya.</summary>
        const float MapScale = 1f / 6.3f;
        const float RoundDuration = 450f; // 7:30 GDD
        const int Seed = 1911;

        /// <summary>Mobil di lembah datar sekitar 25 m di utara rumah, dalam koordinat scene asli Gung Wah.</summary>
        static readonly Vector3 SourceCarPosition = new(-40f, 0f, 260f);
        const float CarClearRadius = 12f;

        // Jarak tata letak dalam skala GDD (map greybox: landmark 56–127 m dari mobil, makhluk 140 m).
        static readonly Vector2 GddLandmarkDistance = new(30f, 75f);
        const float LandmarkSpacing = 22f;
        const float CreatureDistance = 75f;
        const float CandidateStep = 3f, EdgeMargin = 6f;

        static Vector3 carPosition;

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
            var terrain = ScaleMap(terrainObject, out var toScaled);
            carPosition = GreyboxBuilder.Flat(toScaled(SourceCarPosition)); // y = 0: tinggi tanah ditambahkan saat Spawn
            var playerSpawn = carPosition + new Vector3(3.5f, 0f, -5f);
            TerrainBuilder.Use(terrain);
            GreyboxBuilder.SetupLighting();

            var obstacles = AddColliders(terrainObject.transform);
            var rng = new Random(Seed);
            var landmarks = new GameObject("Landmarks").transform;
            var reserved = new List<(Vector3 center, float radius)> { (carPosition, CarClearRadius) };

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
            var car = GreyboxBuilder.Spawn(prefabs["Car"], carPosition, Quaternion.Euler(0f, 90f, 0f), gameplay);
            var player = GreyboxBuilder.Spawn(prefabs["Player"], playerSpawn, Quaternion.Euler(0f, 180f, 0f), gameplay);

            var size = terrain.terrainData.size;
            var origin = terrain.transform.position;
            GameplaySetup.PopulateScene(prefabs, gameplay, player, car, new GameplaySetup.MapSpec(
                FindCreatureSpawn(terrain, obstacles, reserved),
                origin + new Vector3(size.x / 2f, size.y / 2f, size.z / 2f), new Vector3(size.x - 3f, size.y + 40f, size.z - 3f),
                RoundDuration, GreyboxBuilder.SceneDir + "/Hutan_NavMesh.asset"));

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[MapGungWah] Scene tersimpan di {ScenePath}");
        }

        /// <summary>
        /// Memperkecil seluruh map dengan <see cref="MapScale"/> dan memusatkannya di (0, 0): terrain disalin
        /// ke ukuran baru (tinggi ikut diperkecil, susunan pohon dan lapisan tanah tetap), lalu setiap objek
        /// Gung Wah dipindah dan diperkecil terhadap titik asal terrain. <paramref name="toScaled"/>
        /// mengubah koordinat scene asli ke koordinat map baru.
        /// </summary>
        static Terrain ScaleMap(GameObject terrainObject, out System.Func<Vector3, Vector3> toScaled)
        {
            var terrain = terrainObject.GetComponent<Terrain>();
            var source = terrain.terrainData;
            Vector3 oldOrigin = terrainObject.transform.position;
            var size = source.size * MapScale;
            var newOrigin = new Vector3(-size.x / 2f, 0f, -size.z / 2f);
            toScaled = p => newOrigin + (p - oldOrigin) * MapScale;
            var convert = toScaled;

            var data = ScaledTerrain(source, size);
            terrain.terrainData = data;
            terrainObject.GetComponent<TerrainCollider>().terrainData = data;

            var children = terrainObject.transform.Cast<Transform>().ToList();
            var targets = children.Select(c => convert(c.position)).ToList();
            terrainObject.transform.position = newOrigin;
            for (int i = 0; i < children.Count; i++)
            {
                children[i].position = targets[i];
                children[i].localScale *= MapScale;
            }
            // Collider terrain baru pindah di dunia fisika setelah sinkron; tanpa ini cek halangan
            // dan bake navmesh masih membaca posisi terrain lama.
            Physics.SyncTransforms();
            return terrain;
        }

        /// <summary>
        /// Salinan terrain Gung Wah dengan ukuran baru. Rumput detail diturunkan resolusinya sebanding,
        /// supaya kerapatan per meter persegi sama dengan aslinya (tidak 40× lebih rapat).
        /// </summary>
        static TerrainData ScaledTerrain(TerrainData source, Vector3 size)
        {
            AssetDatabase.DeleteAsset(ScaledTerrainPath);
            var data = new TerrainData { heightmapResolution = source.heightmapResolution };
            data.size = size;
            // Aset dibuat dulu: peta lapisan disimpan sebagai sub-aset dan hilang kalau dilukis sebelum CreateAsset.
            AssetDatabase.CreateAsset(data, ScaledTerrainPath);

            int h = source.heightmapResolution;
            data.SetHeights(0, 0, source.GetHeights(0, 0, h, h));
            data.alphamapResolution = source.alphamapResolution;
            data.terrainLayers = source.terrainLayers;
            data.SetAlphamaps(0, 0, source.GetAlphamaps(0, 0, source.alphamapWidth, source.alphamapHeight));

            data.SetDetailScatterMode(source.detailScatterMode);
            // Rumput Gung Wah setinggi 2–10 m mengikuti dunia yang diperbesar; diperkecil setara objek lain.
            data.detailPrototypes = source.detailPrototypes.Select(grass =>
            {
                grass.minWidth *= MapScale;
                grass.maxWidth *= MapScale;
                grass.minHeight *= MapScale;
                grass.maxHeight *= MapScale;
                return grass;
            }).ToArray();
            int oldRes = source.detailResolution;
            int newRes = Mathf.Max(32, Mathf.RoundToInt(oldRes * MapScale / 16f) * 16);
            data.SetDetailResolution(newRes, 16);
            for (int layer = 0; layer < source.detailPrototypes.Length; layer++)
            {
                var old = source.GetDetailLayer(0, 0, oldRes, oldRes, layer);
                var scaled = new int[newRes, newRes];
                for (int z = 0; z < newRes; z++)
                for (int x = 0; x < newRes; x++)
                    scaled[z, x] = old[z * oldRes / newRes, x * oldRes / newRes];
                data.SetDetailLayer(0, 0, layer, scaled);
            }
            data.wavingGrassAmount = source.wavingGrassAmount;
            data.wavingGrassSpeed = source.wavingGrassSpeed;
            data.wavingGrassStrength = source.wavingGrassStrength;
            data.wavingGrassTint = source.wavingGrassTint;

            // Pohon di scene Gung Wah juga diperbesar (±3,3×, sekitar 88 m); ikut diperkecil jadi ±14 m.
            data.treePrototypes = source.treePrototypes;
            data.treeInstances = source.treeInstances.Select(tree =>
            {
                tree.widthScale *= MapScale;
                tree.heightScale *= MapScale;
                return tree;
            }).ToArray();
            EditorUtility.SetDirty(data);
            return data;
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
            var edge = new Vector3(Mathf.Clamp(carPosition.x, b.min.x, b.max.x), 0f, Mathf.Clamp(carPosition.z, b.min.z, b.max.z));
            Vector3 toCar = GreyboxBuilder.Flat(carPosition - edge).normalized;
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

        /// <summary>Landmark GDD (prefab) di tanah datar terbuka dalam jarak GDD dari mobil, sejauh mungkin dari landmark lain.</summary>
        static (Vector3, float) PlaceInOpen(GameObject prefab, Terrain terrain, List<Bounds> obstacles,
            List<(Vector3 center, float radius)> reserved, Transform parent)
        {
            Vector3 best = Vector3.zero;
            float bestScore = float.MinValue;
            foreach (var p in Candidates(terrain, 12f))
            {
                float toCar = GreyboxBuilder.Flat(p - carPosition).magnitude;
                if (toCar < GddLandmarkDistance.x || toCar > GddLandmarkDistance.y || Blocked(p, obstacles, 4f)) continue;
                float nearest = reserved.Min(r => GreyboxBuilder.Flat(p - r.center).magnitude);
                if (nearest < LandmarkSpacing) continue;
                if (nearest > bestScore) { bestScore = nearest; best = p; }
            }

            Vector3 toCarDir = GreyboxBuilder.Flat(carPosition - best);
            var landmark = GreyboxBuilder.Spawn(prefab, best, Quaternion.LookRotation(toCarDir), parent);
            SnapToGround(landmark.transform.Find("ItemSpawns"));
            return (best, 13f);
        }

        /// <summary>Makhluk mulai di tanah datar sejauh mungkin dari mobil (sekitar <see cref="CreatureDistance"/>), seperti di GDD.</summary>
        static Vector3 FindCreatureSpawn(Terrain terrain, List<Bounds> obstacles, List<(Vector3 center, float radius)> reserved)
        {
            return Candidates(terrain, 10f)
                .Where(p => !Blocked(p, obstacles, 2f) && reserved.All(r => GreyboxBuilder.Flat(p - r.center).magnitude > r.radius + 3f))
                .OrderBy(p => Mathf.Abs(GreyboxBuilder.Flat(p - carPosition).magnitude - CreatureDistance))
                .First();
        }

        /// <summary>
        /// Titik-titik grid di terrain yang kemiringannya di bawah batas, menjauhi tepi. Y = 0: tinggi tanah
        /// ditambahkan oleh <see cref="GreyboxBuilder.Spawn"/>, sama seperti tata letak map greybox.
        /// </summary>
        static IEnumerable<Vector3> Candidates(Terrain terrain, float maxSlope)
        {
            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            for (float x = EdgeMargin; x <= data.size.x - EdgeMargin; x += CandidateStep)
            for (float z = EdgeMargin; z <= data.size.z - EdgeMargin; z += CandidateStep)
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
        /// dan batang tumbang (harus dilompati atau diputari). Jumlahnya setara map greybox untuk luas yang mirip.
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

            Place("Bushes", 70, 30f, (p, parent) => GreyboxBuilder.SpawnScaled(bushes[rng.Next(bushes.Length)], p, rng, parent, 0.8f, 1.4f));
            Place("Twigs", 90, 30f, (p, parent) => GreyboxBuilder.SpawnScaled(prefabs["Twig_01"], p, rng, parent, 0.8f, 1.2f));
            Place("Logs", 14, 15f, (p, parent) => GreyboxBuilder.Spawn(prefabs["Log_01"], p, Quaternion.Euler(0f, GreyboxBuilder.Range(rng, 0f, 360f), 0f), parent));
        }

        /// <summary>Tembok tak terlihat di tepi terrain, lebih tinggi dari puncak bukit di utara.</summary>
        static void BuildBounds(Terrain terrain, Transform parent)
        {
            var bounds = new GameObject("MapBounds").transform;
            bounds.SetParent(parent, false);
            var size = terrain.terrainData.size;
            var o = terrain.transform.position;
            const float inset = 1f;
            float height = size.y + 40f, cy = o.y + height / 2f - 10f;
            GreyboxBuilder.AddWall(bounds, new Vector3(o.x + size.x / 2f, cy, o.z + inset), new Vector3(size.x, height, 1f));
            GreyboxBuilder.AddWall(bounds, new Vector3(o.x + size.x / 2f, cy, o.z + size.z - inset), new Vector3(size.x, height, 1f));
            GreyboxBuilder.AddWall(bounds, new Vector3(o.x + inset, cy, o.z + size.z / 2f), new Vector3(1f, height, size.z));
            GreyboxBuilder.AddWall(bounds, new Vector3(o.x + size.x - inset, cy, o.z + size.z / 2f), new Vector3(1f, height, size.z));
        }
    }
}
