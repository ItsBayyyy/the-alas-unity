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

        [Header("Kemunculan")]
        [Tooltip("Makhluk baru muncul setelah sekian detik ronde berjalan, atau saat pemain mengambil barang pertama")]
        [SerializeField] float spawnDelay = 30f;
        [SerializeField] PlayerInventory inventory;

        [Header("Kecepatan (m/s)")]
        [SerializeField] float wanderSpeed = 2f;
        [SerializeField] float investigateSpeed = 3.6f;
        [SerializeField] float huntSpeed = 5f;
        [SerializeField] float finalMinuteSpeedBonus = 0.15f;

        [Header("Pendengaran")]
        [Tooltip("Jarak dengar = perNoise × suara + base")]
        [SerializeField] float hearingPerNoise = 0.5f;
        [SerializeField] float hearingBase = 1.5f;
        [Tooltip("Suara apa pun dalam jarak ini langsung memicu Memburu")]
        [SerializeField] float huntCloseRange = 8f;
        [SerializeField] float loudThreshold = 38f;
        [SerializeField] float loudHuntRange = 40f;
        [Tooltip("Makin jauh, makin meleset tebakan posisi: galat = jarak × nilai ini")]
        [SerializeField] float positionErrorPerMeter = 0.12f;
        [SerializeField] float maxPositionError = 7f;

        [Header("Ingatan")]
        [Tooltip("Batas lama menuju titik suara keras terakhir sebelum Memburu turun ke Menyelidiki")]
        [SerializeField] float huntMemory = 10f;
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

        [Header("Belok (derajat per detik)")]
        [Tooltip("Pelan saat berkeliaran supaya tidak terlihat berputar-putar; cepat hanya saat memburu")]
        [SerializeField] float wanderTurnSpeed = 120f;
        [SerializeField] float investigateTurnSpeed = 200f;
        [SerializeField] float huntTurnSpeed = 420f;

        [Header("Menangkap")]
        [Tooltip("Dari pusat makhluk; lengan model menjulur sekitar 1,6 m ke depan")]
        [SerializeField] float catchDistance = 1.4f;
        [SerializeField] float repathInterval = 0.25f;

        [Header("Mata")]
        [SerializeField] Renderer[] eyes;
        [SerializeField] Color wanderEyeColor = new(0.12f, 0.1f, 0.05f);
        [SerializeField] Color investigateEyeColor = new(1f, 0.78f, 0.22f);
        [SerializeField] Color huntEyeColor = new(1f, 0.12f, 0.08f);

        // Mata memakai shader Unlit, jadi warnanya selalu penuh tanpa butuh cahaya.
        static readonly int EyeColorId = Shader.PropertyToID("_BaseColor");

        NavMeshAgent agent;
        MaterialPropertyBlock eyeBlock;
        Vector3 target;
        Vector3 lastHeard;
        float lostTimer;
        float searchTimer;
        float wanderTimer;
        float repathTimer;
        bool dormant = true;
        Renderer[] bodyRenderers;

        public CreatureState State { get; private set; } = CreatureState.Wander;

        /// <summary>Pengali dari kutukan keserakahan (diisi nanti).</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public float HearingMultiplier { get; set; } = 1f;

        public float DistanceToPlayer => FlatDistance(transform.position, player.transform.position);

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            eyeBlock = new MaterialPropertyBlock();
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
        }

        void OnEnable() => NoiseEvents.Emitted += OnWorldNoise;
        void OnDisable() => NoiseEvents.Emitted -= OnWorldNoise;

        void Start()
        {
            EnterWander();
            SetDormant(true);
        }

        public bool IsDormant => dormant;

        void SetDormant(bool value)
        {
            dormant = value;
            agent.isStopped = value;
            foreach (var r in bodyRenderers) r.enabled = !value;
        }

        /// <summary>
        /// Bangun setelah <see cref="spawnDelay"/>, atau lebih awal saat barang pertama diambil;
        /// yang kedua langsung menyelidiki sekitar pemain supaya ancaman terasa sejak barang pertama.
        /// </summary>
        bool TryWake(RoundManager round)
        {
            bool pickedUp = inventory && inventory.Items.Count > 0;
            if (!pickedUp && round.Duration - round.TimeLeft < spawnDelay) return false;
            SetDormant(false);
            if (pickedUp)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * nearPlayerRadius * 0.5f;
                EnterInvestigate(player.transform.position + new Vector3(offset.x, 0f, offset.y));
            }
            else EnterWander();
            return true;
        }

        /// <summary>
        /// Memindahkan makhluk ke titik navmesh dalam jarak <paramref name="maxSnap"/> dari posisi
        /// yang diminta, lalu mulai berkeliaran dari sana.
        /// </summary>
        public bool TeleportTo(Vector3 position, float maxSnap)
        {
            if (!NavMesh.SamplePosition(position, out var hit, maxSnap, NavMesh.AllAreas)) return false;
            agent.Warp(hit.position);
            SetDormant(false);
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
            if (dormant && !TryWake(round)) return;

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

            // Suara keras (atau terlalu dekat) menunjuk posisi pemain dengan tepat dan memicu Memburu.
            bool alarming = distance < huntCloseRange
                            || (loudness > loudThreshold && distance < loudHuntRange)
                            || round.State == RoundState.Escaping;
            if (alarming)
            {
                lastHeard = player.transform.position;
                lostTimer = 0f;
                if (State == CreatureState.Hunt) SetTarget(lastHeard, immediate: false);
                else EnterHunt();
                return true;
            }

            // Saat memburu, suara pelan tidak cukup untuk dilacak: makhluk menuju titik suara keras terakhir.
            if (State == CreatureState.Hunt) return false;

            float error = Mathf.Min(maxPositionError, distance * positionErrorPerMeter);
            Vector3 offset = new(Random.Range(-error, error), 0f, Random.Range(-error, error));
            lastHeard = player.transform.position + offset;
            lostTimer = 0f;

            if (State == CreatureState.Investigate)
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
            if (dormant) return;
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
                    if (HasArrived() || lostTimer > huntMemory) EnterInvestigate(lastHeard);
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
            agent.angularSpeed = state switch
            {
                CreatureState.Hunt => huntTurnSpeed,
                CreatureState.Investigate => investigateTurnSpeed,
                _ => wanderTurnSpeed
            };
            Color color = state switch
            {
                CreatureState.Hunt => huntEyeColor,
                CreatureState.Investigate => investigateEyeColor,
                _ => wanderEyeColor
            };

            eyeBlock.SetColor(EyeColorId, color);
            foreach (var eye in eyes) eye.SetPropertyBlock(eyeBlock);
        }

        static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
