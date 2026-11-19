using UnityEngine;
using UnityEngine.AI;

namespace TheAlas.Creature
{
    /// <summary>
    /// Menyalurkan gerak NavMeshAgent ke Animator model makhluk: kecepatan untuk blend diam–jalan–lari,
    /// tempo putar animasi yang ikut kecepatan, dan tanda sedang mencari di titik bunyi.
    /// Gerak tubuh tetap dikendalikan agen (tanpa root motion).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(CreatureAI))]
    public class CreatureAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int PlaySpeedId = Animator.StringToHash("PlaySpeed");
        static readonly int SearchingId = Animator.StringToHash("Searching");

        [Tooltip("Kecepatan agen (m/s) yang cocok dengan tempo asli klip gerak; lebih pelan = langkah diperlambat")]
        [SerializeField] float clipSpeed = 2.3f; // langkah kaki klip jalan ±2,3 m/s; selisihnya terlihat sebagai kaki meluncur
        [Tooltip("Batas tempo animasi gerak")]
        [SerializeField] Vector2 playSpeedRange = new(0.3f, 2.4f);
        [SerializeField] float damping = 0.15f;

        NavMeshAgent agent;
        CreatureAI ai;
        Animator animator;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            ai = GetComponent<CreatureAI>();
            animator = GetComponentInChildren<Animator>();
            // Masih model greybox tanpa Animator: komponen ini tidak perlu jalan.
            if (!animator) enabled = false;
        }

        void Update()
        {
            float speed = agent.velocity.magnitude;
            float dt = Time.deltaTime;
            animator.SetFloat(SpeedId, speed, damping, dt);
            // Tempo langkah sebanding dengan kecepatan, jadi berkeliaran terbaca sebagai jalan pelan, memburu sebagai lari.
            animator.SetFloat(PlaySpeedId, Mathf.Clamp(speed / clipSpeed, playSpeedRange.x, playSpeedRange.y), damping, dt);
            animator.SetBool(SearchingId, ai.State == CreatureState.Investigate && speed < 0.2f);
        }
    }
}
