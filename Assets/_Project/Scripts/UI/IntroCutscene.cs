using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheAlas.UI
{
    /// <summary>
    /// Cutscene pembuka tanpa model karakter: beberapa shot kamera dengan teks, sepasang mata menyala
    /// di kegelapan, lalu judul. Selama cutscene, timer, makhluk, kontrol pemain, dan HUD ditahan.
    /// Bisa dilewati dengan Spasi atau Enter.
    /// </summary>
    public class IntroCutscene : MonoBehaviour
    {
        [Serializable]
        public class Shot
        {
            [TextArea] public string caption;
            public float duration = 6f;
            public Vector3 fromPosition;
            public Vector3 toPosition;
            public Vector3 lookAt;
            [Tooltip("Tampilkan mata menyala di tengah shot ini")]
            public bool showEyes;
        }

        [SerializeField] Camera cutsceneCamera;
        [Tooltip("Objek kamera pemain (CameraRoot), dimatikan selama cutscene")]
        [SerializeField] GameObject playerCamera;
        [Tooltip("Komponen yang dimatikan selama cutscene: kontrol pemain, timer, makhluk, HUD")]
        [SerializeField] Behaviour[] pausedDuringIntro;
        [SerializeField] Shot[] shots;
        [SerializeField, TextArea] string[] titleLines;
        [SerializeField] float titleLineDuration = 2.6f;
        [SerializeField] float fadeDuration = 0.8f;

        [Header("Suasana cutscene")]
        [Tooltip("Cahaya bulan dibuat lebih terang selama cutscene supaya hutan terbaca; gameplay kembali gelap")]
        [SerializeField] Light moonlight;
        [SerializeField] float introMoonIntensity = 0.35f;
        [SerializeField] float introFogDensity = 0.05f;

        [Header("Mata")]
        [SerializeField] Renderer[] eyes;
        [SerializeField] Color eyeColor = new(1f, 0.78f, 0.22f);
        [Tooltip("Bagian shot (0–1) saat mata terlihat")]
        [SerializeField] Vector2 eyeWindow = new(0.3f, 0.7f);

        static readonly int EyeColorId = Shader.PropertyToID("_BaseColor"); // shader Unlit

        MaterialPropertyBlock eyeBlock;
        GUIStyle captionStyle;
        GUIStyle titleStyle;
        GUIStyle hintStyle;
        string caption;
        string title;
        float blackout = 1f;
        float textAlpha;
        bool skipRequested;
        float gameMoonIntensity;
        float gameFogDensity;

        public bool IsPlaying { get; private set; }

        void Awake()
        {
            eyeBlock = new MaterialPropertyBlock();
            SetEyes(0f);
            SetGameplayPaused(true);

            gameFogDensity = RenderSettings.fogDensity;
            RenderSettings.fogDensity = introFogDensity;
            if (moonlight)
            {
                gameMoonIntensity = moonlight.intensity;
                moonlight.intensity = introMoonIntensity;
            }
        }

        IEnumerator Start()
        {
            IsPlaying = true;
            foreach (var shot in shots)
            {
                if (skipRequested) break;
                yield return PlayShot(shot);
            }

            if (!skipRequested) yield return PlayTitle();
            Finish();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (IsPlaying && keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                skipRequested = true;
        }

        IEnumerator PlayShot(Shot shot)
        {
            caption = shot.caption;
            for (float t = 0f; t < shot.duration && !skipRequested; t += Time.deltaTime)
            {
                float p = t / shot.duration;
                float eased = Mathf.SmoothStep(0f, 1f, p);
                cutsceneCamera.transform.position = Vector3.Lerp(shot.fromPosition, shot.toPosition, eased);
                cutsceneCamera.transform.rotation = Quaternion.LookRotation(shot.lookAt - cutsceneCamera.transform.position);

                blackout = FadeEdge(t, shot.duration);
                textAlpha = 1f - blackout;
                SetEyes(shot.showEyes ? EyeVisibility(p) : 0f);
                yield return null;
            }
            SetEyes(0f);
        }

        IEnumerator PlayTitle()
        {
            caption = null;
            blackout = 1f;
            foreach (var line in titleLines)
            {
                title = line;
                for (float t = 0f; t < titleLineDuration && !skipRequested; t += Time.deltaTime)
                {
                    textAlpha = 1f - FadeEdge(t, titleLineDuration);
                    yield return null;
                }
            }
            title = null;
        }

        void Finish()
        {
            IsPlaying = false;
            caption = title = null;
            SetEyes(0f);
            SetGameplayPaused(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            StartCoroutine(FadeIntoGame());
        }

        IEnumerator FadeIntoGame()
        {
            // Suasana gameplay dipulihkan selagi layar masih hitam, supaya tidak terlihat berubah mendadak.
            RenderSettings.fogDensity = gameFogDensity;
            if (moonlight) moonlight.intensity = gameMoonIntensity;

            for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
            {
                blackout = 1f - t / fadeDuration;
                yield return null;
            }
            blackout = 0f;
            enabled = false;
        }

        void SetGameplayPaused(bool paused)
        {
            foreach (var behaviour in pausedDuringIntro)
                if (behaviour) behaviour.enabled = !paused;
            playerCamera.SetActive(!paused);
            cutsceneCamera.gameObject.SetActive(paused);
        }

        /// <summary>1 di awal dan akhir (hitam), 0 di tengah.</summary>
        float FadeEdge(float t, float duration) =>
            1f - Mathf.Clamp01(Mathf.Min(t, duration - t) / fadeDuration);

        float EyeVisibility(float p)
        {
            if (p < eyeWindow.x || p > eyeWindow.y) return 0f;
            float local = Mathf.InverseLerp(eyeWindow.x, eyeWindow.y, p);
            return Mathf.Sin(local * Mathf.PI); // muncul pelan, lalu padam
        }

        void SetEyes(float intensity)
        {
            eyeBlock.SetColor(EyeColorId, eyeColor * intensity);
            foreach (var eye in eyes)
            {
                eye.enabled = intensity > 0.01f;
                eye.SetPropertyBlock(eyeBlock);
            }
        }

        void OnGUI()
        {
            if (blackout <= 0f && string.IsNullOrEmpty(caption) && string.IsNullOrEmpty(title)) return;
            EnsureStyles();

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, blackout);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            var cream = new Color(0.89f, 0.85f, 0.74f, textAlpha);
            GUI.color = cream;
            if (!string.IsNullOrEmpty(title))
                GUI.Label(new Rect(0, Screen.height / 2f - 40, Screen.width, 80), title, titleStyle);
            else if (!string.IsNullOrEmpty(caption))
                GUI.Label(new Rect(Screen.width * 0.1f, Screen.height - 150, Screen.width * 0.8f, 80), caption, captionStyle);

            if (IsPlaying)
            {
                GUI.color = new Color(cream.r, cream.g, cream.b, 0.6f);
                GUI.Label(new Rect(Screen.width - 260, Screen.height - 40, 240, 24), "Spasi — lewati", hintStyle);
            }
            GUI.color = previous;
        }

        void EnsureStyles()
        {
            if (captionStyle != null) return;
            captionStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            captionStyle.normal.textColor = Color.white;
            titleStyle = new GUIStyle(captionStyle) { fontSize = 34, fontStyle = FontStyle.Italic };
            hintStyle = new GUIStyle(captionStyle) { fontSize = 14, alignment = TextAnchor.MiddleRight };
        }
    }
}
