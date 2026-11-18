using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace TheAlas.EditorTools
{
    /// <summary>
    /// Tanah peta dari heightmap buatan Gung Wah. Heightmap aslinya hilang; yang tersisa adalah
    /// cadangan EasyRoads3D, dan dari situ diambil potongan 160 × 160 m ke
    /// <c>Source/Terrain/Hutan_Height_129.r16</c> (16-bit, 129 × 129, little-endian).
    ///
    /// Tingginya sengaja dikecilkan dan area landmark, mobil, serta titik muncul diratakan,
    /// supaya angka gameplay dari GDD (jarak, kecepatan, lompatan) tetap berlaku.
    /// </summary>
    static class TerrainBuilder
    {
        const string HeightmapPath = "Source/Terrain/Hutan_Height_129.r16";
        const int Resolution = 129;
        const string TerrainDataPath = GreyboxBuilder.SceneDir + "/Hutan_Terrain.asset";
        const string LayerPath = GreyboxBuilder.Root + "/Art/Materials/Greybox/TL_Ground_Greybox.terrainlayer";
        const string GroundTexturePath = GreyboxBuilder.Root + "/Art/Textures/T_Ground_Greybox.png";

        /// <summary>Beda tinggi titik terendah dan tertinggi, dalam meter.</summary>
        const float Relief = 5f;
        /// <summary>Lebar peralihan dari area rata ke kontur asli, dalam meter.</summary>
        const float PadFalloff = 6f;

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

            /// <summary>Jarak dari tepi area rata (0 di dalam).</summary>
            public float Distance(Vector3 p)
            {
                float dx = p.x - Center.x, dz = p.z - Center.z;
                if (!IsRect) return Mathf.Max(0f, Mathf.Sqrt(dx * dx + dz * dz) - HalfSize.x);
                float ox = Mathf.Max(0f, Mathf.Abs(dx) - HalfSize.x), oz = Mathf.Max(0f, Mathf.Abs(dz) - HalfSize.y);
                return Mathf.Sqrt(ox * ox + oz * oz);
            }
        }

        public static Terrain Build(Transform parent, float mapHalfSize, Color groundColor, IReadOnlyList<Pad> pads)
        {
            float size = mapHalfSize * 2f;
            var heights = ReadHeightmap();
            FlattenPads(heights, mapHalfSize, size, pads);

            AssetDatabase.DeleteAsset(TerrainDataPath);
            var data = new TerrainData { heightmapResolution = Resolution };
            data.size = new Vector3(size, Relief, size);
            data.SetHeights(0, 0, heights);
            data.terrainLayers = new[] { EnsureGroundLayer(groundColor) };
            AssetDatabase.CreateAsset(data, TerrainDataPath);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Ground";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-mapHalfSize, 0f, -mapHalfSize);

            current = go.GetComponent<Terrain>();
            current.materialTemplate = GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;
            current.drawInstanced = true;
            return current;
        }

        public static void Clear() => current = null;

        static float[,] ReadHeightmap()
        {
            var bytes = File.ReadAllBytes(HeightmapPath);
            var heights = new float[Resolution, Resolution];
            for (int z = 0; z < Resolution; z++)
            for (int x = 0; x < Resolution; x++)
            {
                int i = (z * Resolution + x) * 2;
                heights[z, x] = (bytes[i] | (bytes[i + 1] << 8)) / 65535f;
            }
            return heights;
        }

        /// <summary>Setiap area diratakan ke tinggi rata-rata kontur di bawahnya, lalu dibaurkan ke sekitarnya.</summary>
        static void FlattenPads(float[,] heights, float mapHalfSize, float size, IReadOnlyList<Pad> pads)
        {
            Vector3 WorldAt(int z, int x) =>
                new(x / (float)(Resolution - 1) * size - mapHalfSize, 0f, z / (float)(Resolution - 1) * size - mapHalfSize);

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

        static TerrainLayer EnsureGroundLayer(Color groundColor)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath);
            if (layer) return layer;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath);
            if (!texture)
            {
                // Noise halus satu warna supaya tanah tidak terlihat seperti plastik; dibuat sendiri, bukan aset luar.
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGB24, false);
                var rng = new System.Random(7);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = 0.85f + (float)rng.NextDouble() * 0.3f;
                    tex.SetPixel(x, y, groundColor * v);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(GroundTexturePath));
                File.WriteAllBytes(GroundTexturePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(GroundTexturePath);
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath);
            }

            layer = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(6f, 6f) };
            AssetDatabase.CreateAsset(layer, LayerPath);
            return layer;
        }
    }
}
