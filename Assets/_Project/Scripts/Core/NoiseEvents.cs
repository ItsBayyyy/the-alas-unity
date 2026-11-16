using System;
using UnityEngine;

namespace TheAlas.Core
{
    /// <summary>
    /// Bunyi sesaat di dunia yang bukan berasal dari tubuh pemain, misalnya barang yang dilempar
    /// jatuh ke tanah. Suara pemain sendiri dibaca langsung dari <see cref="Player.PlayerNoise"/>.
    /// </summary>
    public static class NoiseEvents
    {
        /// <summary>Posisi bunyi dan kerasnya (0–100).</summary>
        public static event Action<Vector3, float> Emitted;

        public static void Emit(Vector3 position, float loudness) =>
            Emitted?.Invoke(position, Mathf.Clamp(loudness, 0f, 100f));
    }
}
