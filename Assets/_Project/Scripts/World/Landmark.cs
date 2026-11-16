using UnityEngine;

namespace TheAlas.World
{
    /// <summary>Tempat bernama di peta, sekaligus lokasi acak suku cadang.</summary>
    public class Landmark : MonoBehaviour
    {
        [SerializeField] string displayName;
        [Tooltip("Titik di tanah tempat suku cadang boleh muncul")]
        [SerializeField] Transform[] itemSpawnPoints;

        public string DisplayName => displayName;
        public Transform[] ItemSpawnPoints => itemSpawnPoints;
    }
}
