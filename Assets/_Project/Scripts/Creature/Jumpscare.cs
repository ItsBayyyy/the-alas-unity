using System.Collections;
using TheAlas.Core;
using UnityEngine;
using UnityEngine.AI;

namespace TheAlas.Creature
{
    /// <summary>
    /// Saat pemain tertangkap: kamera tersentak ke wajah makhluk yang menerjang dari depan, layar
    /// bergetar dan berkilat merah diiringi teriakan. Layar akhir baru muncul setelah ini selesai.
    /// </summary>
    public class Jumpscare : MonoBehaviour
    {
        [SerializeField] CreatureAI creature;
        [SerializeField] Transform viewPoint;
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip scream;

        [SerializeField] float duration = 1.6f;
        [Tooltip("Makhluk menerjang dari jarak ini ke jarak akhir di depan kamera")]
        [SerializeField] float startDistance = 2.8f;
        [SerializeField] float endDistance = 0.8f;
        [Tooltip("Kepala sedikit di atas mata pemain, jadi terlihat menunduk menatap")]
        [SerializeField] float faceLift = 0.15f;
        [SerializeField] float lungeTime = 0.22f;
        [SerializeField] float shakeAngle = 4f;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int PlaySpeedId = Animator.StringToHash("PlaySpeed");

        /// <summary>Selama true, layar akhir ditahan.</summary>
        public static bool Playing { get; private set; }

        float flash;

        void Start()
        {
            Playing = false;
            RoundManager.Instance.StateChanged += OnRoundStateChanged;
        }

        void OnDestroy()
        {
            Playing = false;
            if (RoundManager.Instance) RoundManager.Instance.StateChanged -= OnRoundStateChanged;
        }

        void OnRoundStateChanged(RoundState state)
        {
            if (state == RoundState.Caught) StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Playing = true;
            var body = creature.transform;
            creature.enabled = false;
            if (body.TryGetComponent<NavMeshAgent>(out var agent)) agent.enabled = false;

            var animator = body.GetComponentInChildren<Animator>();
            if (animator)
            {
                animator.SetFloat(SpeedId, 6f);
                animator.SetFloat(PlaySpeedId, 2.4f);
            }
            Transform head = FindHead(body);
            yield return null; // satu frame supaya pose lari sudah diterapkan sebelum tinggi kepala diukur

            // Makhluk berdiri tepat di depan arah pandang, menghadap pemain.
            // Tubuhnya diturunkan (seperti membungkuk menerkam) supaya wajahnya sejajar mata pemain.
            Vector3 forward = Vector3.ProjectOnPlane(viewPoint.forward, Vector3.up).normalized;
            body.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            float headHeight = head ? head.position.y - body.position.y : 2.2f;
            // Kepala menjulur jauh di depan pusat tubuh; jarak terjangan diukur dari wajah ke mata pemain.
            float headForward = head ? Vector3.Dot(head.position - body.position, body.forward) : 0f;
            Vector3 ground = new(viewPoint.position.x, viewPoint.position.y - headHeight + faceLift, viewPoint.position.z);

            // Cahaya dingin di wajahnya; malam terlalu gelap untuk melihat apa yang menangkapmu.
            var lightObject = new GameObject("JumpscareLight");
            var faceLight = lightObject.AddComponent<Light>();
            faceLight.type = LightType.Point;
            faceLight.range = 5f;
            faceLight.intensity = 4f;
            faceLight.color = new Color(0.75f, 0.85f, 1f);

            if (scream) source.PlayOneShot(scream, 1f);
            Quaternion baseRotation = viewPoint.rotation;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float lunge = Mathf.SmoothStep(0f, 1f, t / lungeTime);
                body.position = ground + forward * (Mathf.Lerp(startDistance, endDistance, lunge) + headForward);

                Vector3 face = head ? head.position : body.position + Vector3.up * 2.2f;
                lightObject.transform.position = Vector3.Lerp(viewPoint.position, face, 0.4f);

                // Kamera tersentak ke wajah lalu bergetar makin pelan.
                var look = Quaternion.LookRotation(face - viewPoint.position);
                float fade = 1f - t / duration;
                var shake = Quaternion.Euler(Random.Range(-1f, 1f) * shakeAngle * fade, Random.Range(-1f, 1f) * shakeAngle * fade, 0f);
                viewPoint.rotation = Quaternion.Slerp(baseRotation, look, Mathf.Clamp01(t / 0.12f)) * shake;

                flash = Mathf.Clamp01(1f - t / duration) * (t < lungeTime ? 0.25f : 0.55f);
                yield return null;
            }

            flash = 0f;
            Destroy(lightObject);
            Playing = false;
        }

        static Transform FindHead(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t.name.EndsWith("Head")) return t;
            return null;
        }

        void OnGUI()
        {
            if (!Playing || flash <= 0f) return;
            var previous = GUI.color;
            GUI.color = new Color(0.55f, 0f, 0f, flash);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
