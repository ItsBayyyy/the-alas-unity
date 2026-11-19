using UnityEngine;

namespace TheAlas.Player
{
    /// <summary>
    /// Senter di tangan pemain, selalu menyala (tombol F dipakai untuk layar penuh di desain UI).
    /// Arah sorotnya sedikit tertinggal dari kamera supaya terasa dipegang, bukan ditempel ke mata.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class Flashlight : MonoBehaviour
    {
        [Tooltip("Makin besar, makin cepat sorot mengejar arah pandang")]
        [SerializeField] float followSharpness = 14f;

        Quaternion smoothedRotation;

        void Awake() => smoothedRotation = transform.parent.rotation;

        void LateUpdate()
        {
            Quaternion target = transform.parent.rotation;
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            smoothedRotation = Quaternion.Slerp(smoothedRotation, target, t);
            transform.rotation = smoothedRotation;
        }
    }
}
