using TheAlas.Core;
using TheAlas.World;
using UnityEngine;

namespace TheAlas.Player
{
    /// <summary>
    /// Seberapa keras pemain terdengar saat ini (0–100). Langkah membentuk tingkat dasar,
    /// aksi seperti mengambil atau mendarat menambah lonjakan yang lalu meluruh.
    /// Model ini sama dengan prototipe yang sudah diuji.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerNoise : MonoBehaviour
    {
        [Header("Langkah (GDD)")]
        [SerializeField] float crouchStep = 5f;
        [SerializeField] float walkStep = 20f;
        [SerializeField] float runStep = 55f;
        [SerializeField] float crouchPerLoad = 2f;
        [SerializeField] float perLoad = 3f;
        [SerializeField] float bushBonus = 14f;
        [SerializeField] float gravelBonus = 10f;

        [Header("Lonjakan")]
        [SerializeField] float jumpNoise = 6f;
        [SerializeField] float landBase = 20f;
        [SerializeField] float landPerLoad = 5f;
        [SerializeField] float engineNoise = 100f;

        [Header("Dinamika")]
        [Tooltip("Seberapa cepat suara naik ke tingkat langkah (per detik)")]
        [SerializeField] float riseRate = 3f;
        [Tooltip("Penurunan suara per detik saat lebih keras dari tingkat langkah")]
        [SerializeField] float decayPerSecond = 28f;
        [SerializeField] float surfaceProbeRadius = 0.3f;

        readonly Collider[] surfaceHits = new Collider[8];
        PlayerMotor motor;
        float bodyNoise;

        /// <summary>Suara dari tubuh: langkah dan aksi.</summary>
        public float BodyNoise => bodyNoise;

        /// <summary>Level mikrofon 0–100; diisi sistem mikrofon.</summary>
        public float MicNoise { get; set; }

        /// <summary>Suara terbesar ditambah 30% suara yang lebih kecil (GDD).</summary>
        public float Total => Mathf.Min(100f, Mathf.Max(bodyNoise, MicNoise) + 0.3f * Mathf.Min(bodyNoise, MicNoise));

        public SurfaceType CurrentSurface { get; private set; }

        void Awake() => motor = GetComponent<PlayerMotor>();

        void OnEnable()
        {
            motor.Jumped += OnJumped;
            motor.Landed += OnLanded;
        }

        void OnDisable()
        {
            motor.Jumped -= OnJumped;
            motor.Landed -= OnLanded;
        }

        public void AddSpike(float amount) => bodyNoise = Mathf.Min(100f, bodyNoise + amount);

        void Update()
        {
            CurrentSurface = ProbeSurface();
            float target = StepNoise();

            float dt = Time.deltaTime;
            if (bodyNoise < target)
                bodyNoise = Mathf.Min(100f, bodyNoise + (target - bodyNoise) * Mathf.Min(1f, dt * riseRate));
            else
                bodyNoise = Mathf.Max(target, bodyNoise - decayPerSecond * dt);
        }

        float StepNoise()
        {
            if (RoundManager.Instance && RoundManager.Instance.State == RoundState.Escaping)
                return engineNoise;

            bool moving = motor.HorizontalSpeed > 0.3f && motor.IsGrounded;
            if (!moving) return 0f;

            float step = motor.IsCrouching ? crouchStep : motor.IsRunning ? runStep : walkStep;
            step += motor.Load * (motor.IsCrouching ? crouchPerLoad : perLoad);
            if (CurrentSurface == SurfaceType.Bush) step += bushBonus;
            else if (CurrentSurface == SurfaceType.Gravel) step += gravelBonus;
            return step;
        }

        SurfaceType ProbeSurface()
        {
            Vector3 feet = transform.position + Vector3.up * surfaceProbeRadius;
            int count = Physics.OverlapSphereNonAlloc(feet, surfaceProbeRadius, surfaceHits, ~0, QueryTriggerInteraction.Collide);

            var result = SurfaceType.Ground;
            for (int i = 0; i < count; i++)
            {
                if (!surfaceHits[i].TryGetComponent(out GroundSurface surface)) continue;
                if (surface.Type == SurfaceType.Bush) return SurfaceType.Bush; // semak paling berisik
                result = surface.Type;
            }
            return result;
        }

        void OnJumped() => AddSpike(jumpNoise);

        void OnLanded(float fallSpeed) => AddSpike(landBase + landPerLoad * motor.Load);
    }
}
