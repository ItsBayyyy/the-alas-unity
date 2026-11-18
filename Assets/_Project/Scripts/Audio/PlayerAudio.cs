using TheAlas.Player;
using TheAlas.World;
using UnityEngine;

namespace TheAlas.Audio
{
    /// <summary>
    /// Bunyi tubuh pemain: langkah sesuai permukaan dan beban, pendaratan, dan napas saat kehabisan stamina.
    /// Hanya suara yang didengar pemain; kerasnya bagi makhluk tetap dihitung <see cref="PlayerNoise"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerNoise))]
    public class PlayerAudio : MonoBehaviour
    {
        [SerializeField] AudioSource source;

        [Header("Langkah")]
        [SerializeField] AudioClip[] groundSteps;
        [SerializeField] AudioClip[] gravelSteps;
        [SerializeField] AudioClip[] bushSteps;
        [SerializeField] AudioClip[] heavySteps;
        [Tooltip("Mulai beban ini langkah terdengar berat")]
        [SerializeField] float heavyLoad = 4f;
        [Tooltip("Jeda antarlangkah (detik): jongkok, jalan, lari")]
        [SerializeField] Vector3 stepInterval = new(0.8f, 0.55f, 0.34f);
        [Tooltip("Volume langkah: jongkok, jalan, lari")]
        [SerializeField] Vector3 stepVolume = new(0.3f, 0.6f, 1f);

        [Header("Napas")]
        [SerializeField] AudioClip[] breathClips;
        [SerializeField] AudioClip[] recoverClips;
        [SerializeField] float breathInterval = 1.7f;

        PlayerMotor motor;
        PlayerNoise noise;
        float stepTimer;
        float breathTimer;
        bool wasExhausted;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            noise = GetComponent<PlayerNoise>();
        }

        void OnEnable() => motor.Landed += OnLanded;
        void OnDisable() => motor.Landed -= OnLanded;

        void Update()
        {
            // Motor mati selama cutscene dan setelah ronde berakhir; kecepatannya tidak lagi diperbarui.
            if (!motor.enabled) return;

            UpdateSteps(Time.deltaTime);
            UpdateBreath(Time.deltaTime);
        }

        void UpdateSteps(float dt)
        {
            bool moving = motor.HorizontalSpeed > 0.3f && motor.IsGrounded;
            if (!moving)
            {
                stepTimer = Mathf.Min(stepTimer, 0.15f); // langkah pertama segera terdengar saat mulai bergerak
                return;
            }

            stepTimer -= dt;
            if (stepTimer > 0f) return;

            int gait = motor.IsCrouching ? 0 : motor.IsRunning ? 2 : 1;
            stepTimer = stepInterval[gait];
            AudioClips.PlayRandom(source, StepClips(), stepVolume[gait]);
        }

        AudioClip[] StepClips() => noise.CurrentSurface switch
        {
            SurfaceType.Bush => bushSteps,
            _ when motor.Load >= heavyLoad => heavySteps,
            SurfaceType.Gravel => gravelSteps,
            _ => groundSteps
        };

        void UpdateBreath(float dt)
        {
            if (motor.IsExhausted)
            {
                breathTimer -= dt;
                if (breathTimer <= 0f)
                {
                    breathTimer = breathInterval;
                    AudioClips.PlayRandom(source, breathClips, 0.8f, 0.05f);
                }
            }
            else if (wasExhausted)
            {
                breathTimer = 0f;
                AudioClips.PlayRandom(source, recoverClips, 0.6f, 0.05f);
            }
            wasExhausted = motor.IsExhausted;
        }

        void OnLanded(float fallSpeed) => AudioClips.PlayRandom(source, heavySteps, 1f);
    }
}
