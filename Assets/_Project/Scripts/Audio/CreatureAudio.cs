using TheAlas.Core;
using TheAlas.Creature;
using UnityEngine;

namespace TheAlas.Audio
{
    /// <summary>
    /// Bunyi penunggu alas dari posisinya di dunia (3D): mengendus saat menyelidiki atau berkeliaran,
    /// menggeram saat mulai dan selama memburu. Pemain bisa menebak keadaan dan arahnya dari telinga.
    /// </summary>
    [RequireComponent(typeof(CreatureAI))]
    public class CreatureAudio : MonoBehaviour
    {
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip[] growlClips;
        [SerializeField] AudioClip[] sniffClips;

        [Header("Jeda acak antarbunyi (detik)")]
        [SerializeField] Vector2 huntInterval = new(3.5f, 6f);
        [SerializeField] Vector2 investigateInterval = new(2.5f, 4.5f);
        [SerializeField] Vector2 wanderInterval = new(9f, 16f);
        [Tooltip("Endusan saat berkeliaran lebih pelan, sekadar tanda makhluk ada di sekitar")]
        [SerializeField] float wanderVolume = 0.5f;

        CreatureAI ai;
        CreatureState lastState;
        float timer;

        void Awake() => ai = GetComponent<CreatureAI>();

        void Start()
        {
            lastState = ai.State;
            timer = Next(lastState);
        }

        void Update()
        {
            if (RoundManager.Instance.IsOver) return;

            if (ai.State != lastState)
            {
                lastState = ai.State;
                timer = Next(lastState);
                // Perubahan ke keadaan lebih waspada langsung terdengar; kembali berkeliaran tidak.
                if (lastState != CreatureState.Wander) Play(lastState);
                return;
            }

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Next(lastState);
            Play(lastState);
        }

        void Play(CreatureState state)
        {
            if (state == CreatureState.Hunt) AudioClips.PlayRandom(source, growlClips);
            else AudioClips.PlayRandom(source, sniffClips, state == CreatureState.Wander ? wanderVolume : 1f);
        }

        float Next(CreatureState state)
        {
            var range = state switch
            {
                CreatureState.Hunt => huntInterval,
                CreatureState.Investigate => investigateInterval,
                _ => wanderInterval
            };
            return Random.Range(range.x, range.y);
        }
    }
}
