using TheAlas.Core;
using TheAlas.Creature;
using UnityEngine;

namespace TheAlas.Audio
{
    /// <summary>
    /// Lapisan suara yang tidak berasal dari satu benda: ambience hutan dan serangga, detak jantung
    /// yang makin cepat saat makhluk mendekat, musik bahaya selama makhluk memburu, dan bunyi akhir ronde.
    /// </summary>
    public class AtmosphereAudio : MonoBehaviour
    {
        [SerializeField] CreatureAI creature;

        [Header("Ambience")]
        [Tooltip("Sumber berulang (hutan, jangkrik, tonggeret); diputar sejak cutscene")]
        [SerializeField] AudioSource[] ambienceLoops;

        [Header("Detak jantung")]
        [SerializeField] AudioSource heartbeatSource;
        [SerializeField] AudioClip heartbeatClip;
        [Tooltip("Mulai jarak ini detak jantung terdengar")]
        [SerializeField] float heartbeatRange = 22f;
        [Tooltip("Jeda antardetak (detik) di tepi jangkauan dan saat makhluk sangat dekat")]
        [SerializeField] Vector2 heartbeatInterval = new(1.1f, 0.4f);

        [Header("Musik bahaya")]
        [SerializeField] AudioSource dangerMusic;
        [SerializeField] float dangerVolume = 0.5f;
        [Tooltip("Lama naik dan turunnya musik (detik)")]
        [SerializeField] float dangerFade = 1.5f;

        [Header("Akhir ronde")]
        [SerializeField] AudioSource stingerSource;
        [SerializeField] AudioClip winClip;
        [SerializeField] AudioClip caughtClip;

        [Header("Bunyi seram acak")]
        [Tooltip("Geraman, endusan, dan semak bergoyang di sekitar pemain supaya mencari barang tidak terasa sepi")]
        [SerializeField] AudioClip[] scareClips;
        [SerializeField] Vector2 scareInterval = new(18f, 40f);
        [SerializeField] Vector2 scareDistance = new(10f, 22f);
        [SerializeField] float scareVolume = 0.7f;

        float heartbeatTimer;
        float scareTimer;
        bool ambienceStarted;

        void Start()
        {
            RoundManager.Instance.StateChanged += OnRoundStateChanged;
        }

        /// <summary>Dipanggil menu utama saat permainan dimulai; di menu hanya musik menu yang terdengar.</summary>
        public void StartAmbience()
        {
            foreach (var loop in ambienceLoops)
                if (!loop.isPlaying) loop.Play();
            ambienceStarted = true;
            scareTimer = Random.Range(scareInterval.x, scareInterval.y);
        }

        /// <summary>Bunyi pendek dari arah acak di sekitar pendengar; tidak menarik perhatian makhluk.</summary>
        void UpdateScares(bool hunting, float dt)
        {
            if (!ambienceStarted || hunting || scareClips.Length == 0) return;
            scareTimer -= dt;
            if (scareTimer > 0f) return;
            scareTimer = Random.Range(scareInterval.x, scareInterval.y);

            var listener = Camera.main;
            if (!listener) return;
            Vector2 direction = Random.insideUnitCircle.normalized * Random.Range(scareDistance.x, scareDistance.y);
            var position = listener.transform.position + new Vector3(direction.x, 0f, direction.y);
            AudioSource.PlayClipAtPoint(AudioClips.Pick(scareClips), position, scareVolume);
        }

        void OnDestroy()
        {
            if (RoundManager.Instance) RoundManager.Instance.StateChanged -= OnRoundStateChanged;
        }

        void Update()
        {
            if (RoundManager.Instance.IsOver) return;

            // Selama cutscene makhluk belum aktif: tidak ada detak jantung atau musik.
            bool active = creature.isActiveAndEnabled && !creature.IsDormant;
            bool hunting = active && creature.State == CreatureState.Hunt;
            float closeness = !active ? 0f : hunting ? 1f : Mathf.Clamp01(1f - creature.DistanceToPlayer / heartbeatRange);

            UpdateHeartbeat(closeness, Time.deltaTime);
            UpdateDangerMusic(hunting, Time.deltaTime);
            UpdateScares(hunting, Time.deltaTime);
        }

        void UpdateHeartbeat(float closeness, float dt)
        {
            if (closeness <= 0f) return;
            heartbeatTimer -= dt;
            if (heartbeatTimer > 0f) return;

            heartbeatTimer = Mathf.Lerp(heartbeatInterval.x, heartbeatInterval.y, closeness);
            heartbeatSource.PlayOneShot(heartbeatClip, Mathf.Lerp(0.3f, 1f, closeness));
        }

        void UpdateDangerMusic(bool hunting, float dt)
        {
            float target = hunting ? dangerVolume : 0f;
            dangerMusic.volume = Mathf.MoveTowards(dangerMusic.volume, target, dangerVolume / dangerFade * dt);
            if (dangerMusic.volume > 0f && !dangerMusic.isPlaying) dangerMusic.Play();
            else if (dangerMusic.volume <= 0f && dangerMusic.isPlaying) dangerMusic.Stop();
        }

        void OnRoundStateChanged(RoundState state)
        {
            if (state is not (RoundState.Won or RoundState.Caught or RoundState.TimeUp)) return;

            dangerMusic.Stop();
            if (state == RoundState.Won) stingerSource.PlayOneShot(winClip);
            else if (state == RoundState.Caught) stingerSource.PlayOneShot(caughtClip);
        }
    }
}
