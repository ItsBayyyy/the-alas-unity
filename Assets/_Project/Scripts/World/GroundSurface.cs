using UnityEngine;

namespace TheAlas.World
{
    public enum SurfaceType
    {
        Ground,
        Gravel,
        Bush
    }

    /// <summary>
    /// Penanda jenis permukaan untuk sistem suara langkah (kerikil kali +10, semak +14 di GDD).
    /// Pasang pada collider trigger yang menutupi area permukaan tersebut.
    /// </summary>
    public class GroundSurface : MonoBehaviour
    {
        [SerializeField] SurfaceType type;

        public SurfaceType Type => type;
    }
}
