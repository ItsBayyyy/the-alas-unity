using TheAlas.Core;
using TheAlas.Player;
using UnityEngine;
using UnityEngine.AI;

namespace TheAlas.Creature
{
    public enum CreatureState
    {
        Wander,
        Investigate,
        Hunt
    }

    /// <summary>
    /// Penunggu alas: buta, hanya mendengar. Tiga keadaan, makin cepat dari Berkeliaran ke
    /// Menyelidiki ke Memburu. Angka dan aturan perpindahan keadaan diambil dari prototipe.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CreatureAI : MonoBehaviour
    {
        [SerializeField] PlayerNoise player;

        [Header("Kecepatan (m/s)")]
        [SerializeField] float wanderSpeed = 1.45f;
        [SerializeField] float investigateSpeed = 2.8f;
        [SerializeField] float huntSpeed = 4.3f;
        [SerializeField] float finalMinuteSpeedBonus = 0.15f;

        [Header("Pendengaran (GDD)")]
        [Tooltip("Jarak dengar = perNoise × suara + base")]
        [SerializeField] float hearingPerNoise = 0.32f;
        [SerializeField] float hearingBase = 0.8f;
        [Tooltip("Suara apa pun dalam jarak ini langsung memicu Memburu")]
        [SerializeField] float huntCloseRange = 7f;
        [SerializeField] float loudThreshold = 38f;
        [SerializeField] float loudHuntRange = 16f;
        [Tooltip("Makin jauh, makin meleset tebakan posisi: galat = jarak × nilai ini")]
        [SerializeField] float positionErrorPerMeter = 0.12f;
        [SerializeField] float maxPositionError = 7f;

        [Header("Ingatan")]
        [Tooltip("Lama tidak mendengar pemain sebelum Memburu turun ke Menyelidiki")]
        [SerializeField] float huntMemory = 2.2f;
        [Tooltip("Lama mencari di titik bunyi sebelum kembali Berkeliaran")]
        [SerializeField] float searchDuration = 2.8f;
        [SerializeField] float arriveDistance = 1.2f;

        [Header("Berkeliaran")]
        [SerializeField] Vector2 retargetInterval = new(5f, 9f);
        [Tooltip("Peluang mengarah ke sekitar pemain di awal dan akhir ronde (GDD: 20% → 75%)")]
        [SerializeField] Vector2 playerBias = new(0.2f, 0.75f);
        [SerializeField] float finalMinuteBias = 0.9f;
        [SerializeField] float nearPlayerRadius = 24f;
        [SerializeField] float roamRadius = 40f;

        [Header("Menangkap")]
        [SerializeField] float catchDistance = 1.1f;
        [SerializeField] float repathInterval = 0.25f;

        [Header("Mata")]
        [SerializeField] Renderer[] eyes;
        [SerializeField, ColorUsage(false, true)] Color wanderEyeColor = new(0.25f, 0.2f, 0.1f);
        [SerializeField, ColorUsage(false, true)] Color investigateEyeColor = new(3f, 2.2f, 0.6f);
        [SerializeField, ColorUsage(false, true)] Color huntEyeColor = new(4f, 0.4f, 0.25f);

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        NavMeshAgent agent;
        MaterialPropertyBlock eyeBlock;
        Vector3 target;
        Vector3 lastHeard;
        float lostTimer;
        float searchTimer;
        float wanderTimer;
        float repathTimer;

        public CreatureState State { get; private set; } = CreatureState.Wander;

        /// <summary>Pengali dari kutukan keserakahan (diisi nanti).</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public float HearingMultiplier { get; set; } = 1f;

        public float DistanceToPlayer => FlatDistance(transform.position, player.transform.position);

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            eyeBlock = new MaterialPropertyBlock();
        }

        void OnEnable() => NoiseEvents.Emitted += OnWorldNoise;
        void OnDisable() => NoiseEvents.Emitted -= OnWorldNoise;

        void Start() => EnterWander();

        /// <summary>
        /// Memindahkan makhluk ke titik navmesh dalam jarak <paramref name="maxSnap"/> dari posisi
        /// yang diminta, lalu mulai berkeliaran dari sana.
        /// </summary>
        public bool TeleportTo(Vector3 position, float maxSnap)
        {
            if (!NavMesh.SamplePosition(position, out var hit, maxSnap, NavMesh.AllAreas)) return false;
            agent.Warp(hit.position);
            EnterWander();
            return true;
        }

        void Update()
        {
            var round = RoundManager.Instance;
            if (round.IsOver)
            {
                agent.isStopped = true;
                return;
            }

            float distance = DistanceToPlayer;
            bool heard = ListenToPlayer(distance, round);
            Tick(heard, Time.deltaTime);

            agent.speed = CurrentSpeed(round);
            if (distance <= catchDistance) round.Caught();
        }

        bool ListenToPlayer(float distance, RoundManager round)
        {
            float loudness = player.Total;
            if (distance > HearingRadius(loudness)) return false;

            float error = Mathf.Min(maxPositionError, distance * positionErrorPerMeter);
            Vector3 offset = new(Random.Range(-error, error), 0f, Random.Range(-error, error));
            lastHeard = player.transform.position + offset;
            lostTimer = 0f;

            bool alarming = distance < huntCloseRange
                            || (loudness > loudThreshold && distance < loudHuntRange)
                            || round.State == RoundState.Escaping;

            if (State == CreatureState.Hunt)
            {
                SetTarget(player.transform.position, immediate: false);
            }
            else if (alarming)
            {
                EnterHunt();
            }
            else if (State == CreatureState.Investigate)
            {
                searchTimer = 0f;
                SetTarget(lastHeard, immediate: false);
            }
            else
            {
                EnterInvestigate(lastHeard);
            }
            return true;
        }

        void OnWorldNoise(Vector3 position, float loudness)
        {
            // Memburu yang masih mendengar pemain tidak teralihkan; yang sudah kehilangan jejak bisa terpancing.
            if (State == CreatureState.Hunt && lostTimer <= 0f) return;
            if (FlatDistance(transform.position, position) > HearingRadius(loudness)) return;
            EnterInvestigate(position);
        }

        void Tick(bool heardPlayer, float dt)
        {
            switch (State)
            {
                case CreatureState.Hunt:
                    if (heardPlayer) break;
                    lostTimer += dt;
                    SetTarget(lastHeard, immediate: false);
                    if (lostTimer > huntMemory) EnterInvestigate(lastHeard);
                    break;

                case CreatureState.Investigate:
                    if (HasArrived())
                    {
                        searchTimer += dt;
                        if (searchTimer > searchDuration) EnterWander();
                    }
                    break;

                case CreatureState.Wander:
                    wanderTimer -= dt;
                    if (wanderTimer <= 0f || HasArrived()) PickWanderTarget();
                    break;
            }
        }

        void EnterHunt()
        {
            SetState(CreatureState.Hunt);
            SetTarget(player.transform.position, immediate: true);
        }

        void EnterInvestigate(Vector3 position)
        {
            SetState(CreatureState.Investigate);
            searchTimer = 0f;
            lostTimer = 0f;
            SetTarget(position, immediate: true);
        }

        void EnterWander()
        {
            SetState(CreatureState.Wander);
            PickWanderTarget();
        }

        void PickWanderTarget()
        {
            var round = RoundManager.Instance;
            float bias = round.IsFinalMinute ? finalMinuteBias : Mathf.Lerp(playerBias.x, playerBias.y, round.Progress);

            Vector3 center = Random.value < bias ? player.transform.position : transform.position;
            float radius = center == player.transform.position ? nearPlayerRadius : roamRadius;
            Vector2 offset = Random.insideUnitCircle * radius;

            SetTarget(center + new Vector3(offset.x, 0f, offset.y), immediate: true);
            wanderTimer = Random.Range(retargetInterval.x, retargetInterval.y);
        }

        void SetTarget(Vector3 position, bool immediate)
        {
            repathTimer -= Time.deltaTime;
            if (!immediate && repathTimer > 0f) return;
            repathTimer = repathInterval;

            if (!NavMesh.SamplePosition(position, out var hit, 6f, NavMesh.AllAreas)) return;
            target = hit.position;
            agent.isStopped = false;
            agent.SetDestination(target);
        }

        bool HasArrived() =>
            !agent.pathPending && (agent.remainingDistance <= arriveDistance || !agent.hasPath);

        float CurrentSpeed(RoundManager round)
        {
            float speed = State switch
            {
                CreatureState.Hunt => huntSpeed,
                CreatureState.Investigate => investigateSpeed,
                _ => wanderSpeed
            };
            if (round.IsFinalMinute) speed *= 1f + finalMinuteSpeedBonus;
            return speed * SpeedMultiplier;
        }

        float HearingRadius(float loudness) => (hearingPerNoise * loudness + hearingBase) * HearingMultiplier;

        void SetState(CreatureState state)
        {
            State = state;
            Color color = state switch
            {
                CreatureState.Hunt => huntEyeColor,
                CreatureState.Investigate => investigateEyeColor,
                _ => wanderEyeColor
            };

            eyeBlock.SetColor(EmissionColorId, color);
            foreach (var eye in eyes) eye.SetPropertyBlock(eyeBlock);
        }

        static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
