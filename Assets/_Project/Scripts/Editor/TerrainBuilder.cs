using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Random = System.Random;

namespace TheAlas.EditorTools
{
    /// <summary>
    /// Tanah peta dari terrain buatan Gung Wah (780 × 889 m). Area main 160 × 160 m dipotong dari
    /// lereng kaki bukit tengahnya, berpusat di (330, 300) koordinat lokal terrain itu, ke
    /// <c>Source/Terrain/Hutan_Height_129.r16</c> (16-bit, 129 × 129, little-endian, dinormalkan 0–1).
    /// Bagian lain terrainnya terlalu datar atau terlalu curam untuk dijelajah dengan berjalan kaki.
    ///
    /// Tingginya mengikuti aslinya; area landmark, mobil, dan titik muncul diratakan supaya angka
    /// gameplay dari GDD (jarak, lompatan, titik muncul barang) tetap berlaku.
    /// Permukaannya memakai dua lapisan tanah dari map Gung Wah (rumput dan tanah berbatu), ditambah
    /// jalan setapak dan kerikil Kali Kering, dan ditumbuhi rumput detail, kecuali di jalur setapak,
    /// area rata, dan Kali Kering.
    /// </summary>
    static class TerrainBuilder
    {
        const string HeightmapPath = "Source/Terrain/Hutan_Height_129.r16";
        /// <summary>Resolusi file heightmap (potongan 160 m area main).</summary>
        const int SourceResolution = 129;
        /// <summary>Resolusi terrain, yang mencakup area main ditambah hutan pemandangan di luar tembok.</summary>
        const int Resolution = 257;
        const int SplatResolution = 256;
        const int DetailResolution = 352; // kelipatan 16; sekitar 0,6 m per sel di terrain 220 m
        const string TerrainDataPath = GreyboxBuilder.SceneDir + "/Hutan_Terrain.asset";
        const string LayerDir = GreyboxBuilder.Root + "/Art/Materials/Terrain";
        const string GroundTextures = "Assets/ThirdParty/YughuesFreeGroundMaterials/Textures";
        const string GrassTexture = "Assets/ThirdParty/VegetationSpawner/_Demo/Materials/Textures/GrassField.png";

        /// <summary>Beda tinggi titik terendah dan tertinggi potongan, dalam meter (sesuai terrain asli).</summary>
        const float Relief = 12.9f;
        /// <summary>Lebar peralihan dari area rata ke kontur asli, dalam meter.</summary>
        const float PadFalloff = 6f;
        /// <summary>Jarak rumput terlihat; lebih jauh dari ini sudah tertutup kabut.</summary>
        const float GrassDistance = 30f;

        enum Layer { Grass, Stones, Trail, Gravel }

        static Terrain current;

        public static bool HasHeightmap => File.Exists(HeightmapPath);

        /// <summary>Tinggi tanah di posisi dunia; 0 kalau belum ada terrain.</summary>
        public static float HeightAt(Vector3 position) =>
            current ? current.SampleHeight(position) + current.transform.position.y : 0f;

        public readonly struct Pad
        {
            public readonly Vector3 Center;
            public readonly Vector2 HalfSize;
            public readonly bool IsRect;

            Pad(Vector3 center, Vector2 halfSize, bool isRect)
            {
                Center = center;
                HalfSize = halfSize;
                IsRect = isRect;
            }

            public static Pad Circle(Vector3 center, float radius) => new(center, new Vector2(radius, radius), false);
            public static Pad Rect(Vector3 center, Vector2 size) => new(center, size / 2f, true);

            /// <summary>Jarak dari tepi area (0 di dalam).</summary>
            public float Distance(Vector3 p)
            {
                float dx = p.x - Center.x, dz = p.z - Center.z;
                if (!IsRect) return Mathf.Max(0f, Mathf.Sqrt(dx * dx + dz * dz) - HalfSize.x);
                float ox = Mathf.Max(0f, Mathf.Abs(dx) - HalfSize.x), oz = Mathf.Max(0f, Mathf.Abs(dz) - HalfSize.y);
                return Mathf.Sqrt(ox * ox + oz * oz);
            }
        }

        /// <param name="outerMargin">
        /// Lebar hutan pemandangan di luar tembok tak terlihat. Tanpa ini, tepi peta terlihat sebagai
        /// tanah yang terpotong lalu bidang kosong.
        /// </param>
        public static Terrain Build(Transform parent, float mapHalfSize, float outerMargin, IReadOnlyList<Pad> pads,
            Func<Vector3, float> distanceToTrail, Pad riverbed)
        {
            float half = mapHalfSize + outerMargin;
            float size = half * 2f;
            var heights = BuildHeights(mapHalfSize, half);
            FlattenPads(heights, half, size, pads);

            var layers = new[]
            {
                EnsureLayer("TL_Grass", "Grass04", 5f),
                EnsureLayer("TL_Stones", "GroundStones02", 4f),
                EnsureLayer("TL_Trail", "SoilGravel01", 3f),
                EnsureLayer("TL_Riverbed", "Dry02", 4f),
            };

            // Aset dibuat dulu: peta lapisan disimpan sebagai sub-aset, dan hilang kalau dilukis sebelum CreateAsset.
            AssetDatabase.DeleteAsset(TerrainDataPath);
            var data = new TerrainData { heightmapResolution = Resolution };
            data.size = new Vector3(size, Relief, size);
            AssetDatabase.CreateAsset(data, TerrainDataPath);
            data.SetHeights(0, 0, heights);
            data.alphamapResolution = SplatResolution;
            data.terrainLayers = layers;

            Vector3 WorldAt(float u, float v) => new(u * size - half, 0f, v * size - half);
            PaintSurface(data, WorldAt, distanceToTrail, riverbed);
            PlantGrass(data, WorldAt, pads, distanceToTrail, riverbed);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Ground";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-half, 0f, -half);

            current = go.GetComponent<Terrain>();
            current.materialTemplate = GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;
            current.drawInstanced = true;
            current.detailObjectDistance = GrassDistance;
            current.detailObjectDensity = 1f;
            return current;
        }

        public static void Clear() => current = null;

        static float[,] ReadHeightmap()
        {
            var bytes = File.ReadAllBytes(HeightmapPath);
            var heights = new float[SourceResolution, SourceResolution];
            for (int z = 0; z < SourceResolution; z++)
            for (int x = 0; x < SourceResolution; x++)
            {
                int i = (z * SourceResolution + x) * 2;
                heights[z, x] = (bytes[i] | (bytes[i + 1] << 8)) / 65535f;
            }
            return heights;
        }

        /// <summary>
        /// Heightmap area main ditempatkan di tengah terrain yang lebih lebar. Di luar area main,
        /// tinggi tepinya diteruskan keluar (koordinat di-clamp), jadi tanahnya menyambung tanpa tebing.
        /// </summary>
        static float[,] BuildHeights(float mapHalfSize, float half)
        {
            var source = ReadHeightmap();
            float size = half * 2f;
            var heights = new float[Resolution, Resolution];
            for (int z = 0; z < Resolution; z++)
            for (int x = 0; x < Resolution; x++)
            {
                float wx = x / (float)(Resolution - 1) * size - half;
                float wz = z / (float)(Resolution - 1) * size - half;
                float u = Mathf.Clamp01((wx + mapHalfSize) / (2f * mapHalfSize)) * (SourceResolution - 1);
                float v = Mathf.Clamp01((wz + mapHalfSize) / (2f * mapHalfSize)) * (SourceResolution - 1);
                int x0 = Mathf.Min((int)u, SourceResolution - 2), z0 = Mathf.Min((int)v, SourceResolution - 2);
                float fx = u - x0, fz = v - z0;
                heights[z, x] = Mathf.Lerp(
                    Mathf.Lerp(source[z0, x0], source[z0, x0 + 1], fx),
                    Mathf.Lerp(source[z0 + 1, x0], source[z0 + 1, x0 + 1], fx), fz);
            }
            return heights;
        }

        /// <summary>Setiap area diratakan ke tinggi rata-rata kontur di bawahnya, lalu dibaurkan ke sekitarnya.</summary>
        static void FlattenPads(float[,] heights, float half, float size, IReadOnlyList<Pad> pads)
        {
            Vector3 WorldAt(int z, int x) =>
                new(x / (float)(Resolution - 1) * size - half, 0f, z / (float)(Resolution - 1) * size - half);

            foreach (var pad in pads)
            {
                float sum = 0f;
                int count = 0;
                for (int z = 0; z < Resolution; z++)
                for (int x = 0; x < Resolution; x++)
                    if (pad.Distance(WorldAt(z, x)) <= 0f) { sum += heights[z, x]; count++; }
                if (count == 0) continue;
                float target = sum / count;

                for (int z = 0; z < Resolution; z++)
                for (int x = 0; x < Resolution; x++)
                {
                    float d = pad.Distance(WorldAt(z, x));
                    if (d >= PadFalloff) continue;
                    float blend = 1f - Mathf.SmoothStep(0f, 1f, d / PadFalloff);
                    heights[z, x] = Mathf.Lerp(heights[z, x], target, blend);
                }
            }
        }

        /// <summary>Rumput sebagai dasar, bercak tanah berbatu acak, tanah kering di jalur, kerikil di Kali Kering.</summary>
        static void PaintSurface(TerrainData data, Func<float, float, Vector3> worldAt, Func<Vector3, float> distanceToTrail, Pad riverbed)
        {
            int n = data.alphamapResolution;
            var maps = new float[n, n, 4];
            float trailCore = GreyboxBuilder.TrailHalfWidth * 0.8f, trailEdge = GreyboxBuilder.TrailHalfWidth + 0.8f;

            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                Vector3 p = worldAt(x / (float)(n - 1), z / (float)(n - 1));
                float stones = Mathf.Clamp01((Mathf.PerlinNoise(p.x * 0.06f + 13f, p.z * 0.06f + 7f) - 0.45f) * 2f) * 0.8f;
                float trailW = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(trailCore, trailEdge, distanceToTrail(p)));
                float gravel = 1f - Mathf.SmoothStep(0f, 1f, riverbed.Distance(p) / 1.5f);

                float rest = (1f - trailW) * (1f - gravel);
                maps[z, x, (int)Layer.Grass] = rest * (1f - stones);
                maps[z, x, (int)Layer.Stones] = rest * stones;
                maps[z, x, (int)Layer.Trail] = trailW * (1f - gravel);
                maps[z, x, (int)Layer.Gravel] = gravel;
            }
            data.SetAlphamaps(0, 0, maps);
        }

        /// <summary>Rumput rendah bergerombol; tidak tumbuh di jalur, area rata, dan dasar kali.</summary>
        static void PlantGrass(TerrainData data, Func<float, float, Vector3> worldAt, IReadOnlyList<Pad> pads,
            Func<Vector3, float> distanceToTrail, Pad riverbed)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTexture);
            if (!texture) return;

            // Nilai di detail layer = jumlah helai per sel. Mode bawaan Unity 6 (Coverage) membacanya sebagai persen.
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            data.SetDetailResolution(DetailResolution, 16);
            data.detailPrototypes = new[]
            {
                new DetailPrototype
                {
                    prototypeTexture = texture,
                    // Billboard: helai selalu tegak menghadap kamera; mode Grass biasa tampak rebah di URP.
                    renderMode = DetailRenderMode.GrassBillboard,
                    // Tekstur rumputnya putih; warna ini yang memberi hijau tua lantai hutan malam.
                    healthyColor = new Color(0.10f, 0.16f, 0.06f),
                    dryColor = new Color(0.17f, 0.16f, 0.07f),
                    minWidth = 0.4f, maxWidth = 0.8f,
                    minHeight = 0.25f, maxHeight = 0.55f,
                    noiseSpread = 0.4f,
                }
            };

            var density = new int[DetailResolution, DetailResolution];
            var rng = new Random(23);
            for (int z = 0; z < DetailResolution; z++)
            for (int x = 0; x < DetailResolution; x++)
            {
                Vector3 p = worldAt(x / (float)(DetailResolution - 1), z / (float)(DetailResolution - 1));
                float clump = Mathf.PerlinNoise(p.x * 0.12f + 3f, p.z * 0.12f + 11f);
                if (clump < 0.45f) continue;
                if (distanceToTrail(p) < GreyboxBuilder.TrailHalfWidth + 0.4f || riverbed.Distance(p) <= 0.5f) continue;
                bool onPad = false;
                foreach (var pad in pads) if (pad.Distance(p) <= 0f) { onPad = true; break; }
                if (onPad) continue;
                density[z, x] = 1 + rng.Next(3);
            }
            data.SetDetailLayer(0, 0, 0, density);
        }

        static TerrainLayer EnsureLayer(string name, string textureName, float tile)
        {
            string path = $"{LayerDir}/{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer) return layer;

            Directory.CreateDirectory(LayerDir);
            AssetDatabase.Refresh();
            string normalPath = $"{GroundTextures}/T_YFGM_{textureName}_n.tga";
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter importer && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }

            layer = new TerrainLayer
            {
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundTextures}/T_YFGM_{textureName}_d.tga"),
                normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath),
                tileSize = new Vector2(tile, tile),
                smoothness = 0.05f,
            };
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }
    }
}
