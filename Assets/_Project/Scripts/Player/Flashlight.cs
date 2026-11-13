using UnityEngine;

namespace TheAlas.Player
{
    /// <summary>
    /// Senter di tangan pemain. Arah sorotnya sedikit tertinggal dari kamera
    /// supaya terasa dipegang, bukan ditempel ke mata.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class Flashlight : MonoBehaviour
    {
        [Tooltip("Makin besar, makin cepat sorot mengejar arah pandang")]
        [SerializeField] float followSharpness = 14f;
        [SerializeField] bool startOn = true;

        Light spot;
        PlayerInputReader input;
        Quaternion smoothedRotation;

        public bool IsOn => spot.enabled;

        void Awake()
        {
            spot = GetComponent<Light>();
            input = GetComponentInParent<PlayerInputReader>();
            spot.enabled = startOn;
            smoothedRotation = transform.parent.rotation;
        }

        void Update()
        {
            if (input.FlashlightPressed) spot.enabled = !spot.enabled;
        }

        void LateUpdate()
        {
            Quaternion target = transform.parent.rotation;
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            smoothedRotation = Quaternion.Slerp(smoothedRotation, target, t);
            transform.rotation = smoothedRotation;
        }
    }
}
