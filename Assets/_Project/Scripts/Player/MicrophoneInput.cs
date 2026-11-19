using TheAlas.UI;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace TheAlas.Player
{
    /// <summary>
    /// Suara pemain di dunia nyata lewat mikrofon, masuk ke <see cref="PlayerNoise.MicNoise"/> (0–100)
    /// sehingga makhluk ikut mendengarnya: berbisik aman, bicara biasa terdengar belasan meter,
    /// teriakan lebih jauh. Di WebGL dibaca lewat plugin <c>MicLevel.jslib</c>, di editor lewat Microphone.
    /// </summary>
    public class MicrophoneInput : MonoBehaviour
    {
        public enum MicState { Off, Waiting, On, Denied }

        [SerializeField] PlayerNoise noise;
        [Tooltip("Kerasnya suara (dB RMS) yang dihitung 0 dan 100 pada kepekaan 50%")]
        [SerializeField] Vector2 decibelRange = new(-48f, -8f);
        [Tooltip("Pergeseran dB dari slider kepekaan: 0% = −nilai ini, 100% = +nilai ini")]
        [SerializeField] float sensitivityRangeDb = 12f;
        [Tooltip("Turunnya suara per detik setelah berhenti bicara (naiknya seketika)")]
        [SerializeField] float releasePerSecond = 120f;

        float level;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void PointerLockGuard_Install();

        // Komponen ini ada di setiap ronde sejak awal, jadi tempat yang pas untuk memasang pengaman Pointer Lock.
        void Awake() => PointerLockGuard_Install();
#endif

        public static MicState State { get; private set; }

        /// <summary>Kerasnya suara mikrofon 0–1, untuk bar di layar pengaturan.</summary>
        public static float Level01 { get; private set; }

        /// <summary>
        /// Minta akses mikrofon. Browser hanya mengizinkannya dari klik pemain, jadi dipanggil dari
        /// tombol Play Game dan tombol mikrofon di pengaturan.
        /// </summary>
        public static void Begin()
        {
            if (!GameSettings.MicEnabled || State == MicState.On) return;
            State = MicState.Waiting;
            PlatformStart();
        }

        public static void End()
        {
            PlatformStop();
            State = MicState.Off;
            Level01 = 0f;
        }

        void Update()
        {
            if (GameSettings.MicEnabled && State != MicState.Off) State = PlatformState();
            if (State != MicState.On)
            {
                level = 0f;
            }
            else
            {
                float sensitivityDb = (GameSettings.Get(GameSettings.Slider.MicSensitivity) - 0.5f) * 2f * sensitivityRangeDb;
                float db = 20f * Mathf.Log10(Mathf.Max(PlatformRms(), 1e-5f)) + sensitivityDb;
                float target = Mathf.InverseLerp(decibelRange.x, decibelRange.y, db) * 100f;
                level = target > level ? target : Mathf.MoveTowards(level, target, releasePerSecond * Time.unscaledDeltaTime);
            }
            Level01 = level / 100f;
            noise.MicNoise = level;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void MicLevel_Start();
        [DllImport("__Internal")] static extern void MicLevel_Stop();
        [DllImport("__Internal")] static extern float MicLevel_Get();
        [DllImport("__Internal")] static extern int MicLevel_State();

        static void PlatformStart() => MicLevel_Start();
        static void PlatformStop() => MicLevel_Stop();
        static float PlatformRms() => MicLevel_Get();
        static MicState PlatformState() => MicLevel_State() switch { 1 => MicState.On, -1 => MicState.Denied, _ => MicState.Waiting };
#else
        static readonly float[] Samples = new float[512];
        static AudioClip clip;
        static string device;

        static void PlatformStart()
        {
            if (Microphone.devices.Length == 0) return;
            device = Microphone.devices[0];
            clip = Microphone.Start(device, true, 1, 16000);
        }

        static void PlatformStop()
        {
            if (clip) Microphone.End(device);
            clip = null;
        }

        static float PlatformRms()
        {
            int start = Microphone.GetPosition(device) - Samples.Length;
            if (!clip || start < 0) return 0f;
            clip.GetData(Samples, start);
            float sum = 0f;
            foreach (float s in Samples) sum += s * s;
            return Mathf.Sqrt(sum / Samples.Length);
        }

        static MicState PlatformState() => clip ? MicState.On : MicState.Denied;
#endif
    }
}
