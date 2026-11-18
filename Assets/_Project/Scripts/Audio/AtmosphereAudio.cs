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

        float heartbeatTimer;

        void Start()
        {
            foreach (var loop in ambienceLoops) loop.Play();
            RoundManager.Instance.StateChanged += OnRoundStateChanged;
        }

        void OnDestroy()
        {
            if (RoundManager.Instance) RoundManager.Instance.StateChanged -= OnRoundStateChanged;
        }

        void Update()
        {
            if (RoundManager.Instance.IsOver) return;

            // Selama cutscene makhluk belum aktif: tidak ada detak jantung atau musik.
            bool active = creature.isActiveAndEnabled;
            bool hunting = active && creature.State == CreatureState.Hunt;
            float closeness = !active ? 0f : hunting ? 1f : Mathf.Clamp01(1f - creature.DistanceToPlayer / heartbeatRange);

            UpdateHeartbeat(closeness, Time.deltaTime);
            UpdateDangerMusic(hunting, Time.deltaTime);
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
