using UnityEngine;

namespace TheAlas.Items
{
    /// <summary>
    /// Data satu jenis barang. Rumus suara dan jarak lempar mengikuti tabel GDD
    /// dan prototipe, supaya balancing cukup mengubah bobot di aset ini.
    /// </summary>
    [CreateAssetMenu(fileName = "ITEM_", menuName = "The Alas/Item")]
    public class ItemData : ScriptableObject
    {
        [SerializeField] string displayName;
        [SerializeField, Min(0f)] float weight = 1f;
        [Tooltip("Suku cadang mobil yang wajib dibawa pulang")]
        [SerializeField] bool isCarPart;
        [Tooltip("Nilai barang antik dalam ribuan rupiah; 0 untuk suku cadang")]
        [SerializeField, Min(0)] int value;
        [SerializeField] GameObject worldPrefab;
        [Tooltip("Gambar barang di slot inventori (ikon dari desain UI)")]
        [SerializeField] Texture2D icon;

        [Header("Audio (sesuai bahan barang)")]
        [SerializeField] AudioClip[] pickupClips;
        [SerializeField] AudioClip[] impactClips;

        public string DisplayName => displayName;
        public AudioClip[] PickupClips => pickupClips;
        public AudioClip[] ImpactClips => impactClips;
        public float Weight => weight;
        public bool IsCarPart => isCarPart;
        public int Value => value;
        public GameObject WorldPrefab => worldPrefab;
        public Texture2D Icon => icon;

        public float PickupNoise => 16f + 3f * weight;
        public float DropNoise => 26f + 4f * weight;
        public float PlaceNoise => 8f + weight;
        public float ThrowImpactNoise => 45f + 6f * weight;
        /// <summary>Barang berat tidak bisa dilempar sejauh barang ringan.</summary>
        public float ThrowDistance => Mathf.Max(4f, 9f - 0.8f * weight);
    }
}
