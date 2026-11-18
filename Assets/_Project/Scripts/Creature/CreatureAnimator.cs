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

        [Tooltip("Tempo animasi saat diam dan saat berlari paling cepat")]
        [SerializeField] Vector2 playSpeedRange = new(0.5f, 1.6f);
        [Tooltip("Kecepatan agen (m/s) yang memakai tempo tertinggi")]
        [SerializeField] float fastestSpeed = 5f;
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
            animator.SetFloat(PlaySpeedId, Mathf.Lerp(playSpeedRange.x, playSpeedRange.y, speed / fastestSpeed), damping, dt);
            animator.SetBool(SearchingId, ai.State == CreatureState.Investigate && speed < 0.2f);
        }
    }
}
