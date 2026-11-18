using System.Runtime.InteropServices;
using UnityEngine;

public class MicInput : MonoBehaviour
{
    public static MicInput Instance { get; private set; }

    [Header("Pemetaan level")]
    [Tooltip("Level dBFS yang dianggap 100 (teriak)")]
    public float loudDb = -6f;
    [Tooltip("Batas di atas noise floor sebelum suara dihitung")]
    public float gateMarginDb = 6f;
    [Range(0.5f, 2f)] public float sensitivity = 1f; // dari slider pengaturan
    public float attack = 20f, release = 4f;         // kecepatan naik dan turun bar
    public float calibrationSeconds = 2f;

    public float Level { get; private set; }        // 0-100
    public bool IsRunning { get; private set; }
    public bool IsDenied { get; private set; }
    public bool IsCalibrating => calibrateTimer > 0f;

    float noiseFloorDb = -60f;
    float calibrateTimer, calibrateSum;
    int calibrateCount;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void MicWebStart();
    [DllImport("__Internal")] static extern int MicWebStatus();
    [DllImport("__Internal")] static extern float MicWebGetRms();
    [DllImport("__Internal")] static extern void MicWebStop();
#else
    const int Window = 1024;
    readonly float[] buffer = new float[Window];
    AudioClip micClip;
    string device;
#endif

    void Awake() => Instance = this;

    // Panggil dari tombol UI. Browser butuh interaksi pengguna.
    public void StartMic()
    {
        IsDenied = false;
#if UNITY_WEBGL && !UNITY_EDITOR
        MicWebStart();
        IsRunning = true;
#else
        if (Microphone.devices.Length == 0) { IsDenied = true; return; }
        device = Microphone.devices[0];
        Microphone.GetDeviceCaps(device, out int minF, out int maxF);
        int freq = maxF == 0 ? 44100 : Mathf.Clamp(44100, minF, maxF);
        micClip = Microphone.Start(device, true, 1, freq);
        IsRunning = micClip != null;
#endif
        Recalibrate();
    }

    public void StopMic()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        MicWebStop();
#else
        if (device != null) Microphone.End(device);
#endif
        IsRunning = false;
        Level = 0f;
    }

    // Ukur kebisingan latar beberapa detik. Minta pemain diam.
    public void Recalibrate()
    {
        calibrateTimer = calibrationSeconds;
        calibrateSum = 0f;
        calibrateCount = 0;
    }

    void Update()
    {
        if (!IsRunning) { Level = 0f; return; }

        float rms = ReadRms();
        if (rms < 0f) return; // belum siap atau ditolak

        float db = 20f * Mathf.Log10(Mathf.Max(rms, 1e-7f));

        if (calibrateTimer > 0f)
        {
            calibrateTimer -= Time.unscaledDeltaTime;
            calibrateSum += db;
            calibrateCount++;
            if (calibrateTimer <= 0f && calibrateCount > 0) noiseFloorDb = calibrateSum / calibrateCount;
            Level = 0f;
            return;
        }

        float gate = Mathf.Min(noiseFloorDb + gateMarginDb, loudDb - 6f);
        float target = Mathf.Clamp(Mathf.InverseLerp(gate, loudDb, db) * 100f * sensitivity, 0f, 100f);
        float speed = target > Level ? attack : release;
        Level = Mathf.Lerp(Level, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
    }

    float ReadRms()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        int status = MicWebStatus();
        if (status < 0) { IsDenied = true; IsRunning = false; return -1f; }
        if (status == 0) return -1f;
        return MicWebGetRms();
#else
        if (micClip == null) return -1f;
        int start = Microphone.GetPosition(device) - Window;
        if (start < 0) return -1f; // lewati frame saat buffer melingkar
        micClip.GetData(buffer, start);
        float sum = 0f;
        for (int i = 0; i < Window; i++) sum += buffer[i] * buffer[i];
        return Mathf.Sqrt(sum / Window);
#endif
    }

    void OnDestroy() { if (IsRunning) StopMic(); }
}