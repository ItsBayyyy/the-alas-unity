using UnityEngine;

public class MicTest : MonoBehaviour
{
    public float gameNoise = 20f; // simulasi noise langkah jalan (nilai GDD)

    void OnGUI()
    {
        GUI.skin.label.fontSize = 22;
        GUI.skin.button.fontSize = 22;

        GUILayout.BeginArea(new Rect(20, 20, 760, 560));

        var mic = MicInput.Instance;
        if (mic == null)
        {
            GUILayout.Label("MicInput tidak ada di scene");
            GUILayout.EndArea();
            return;
        }

        if (GUILayout.Button("Aktifkan Mikrofon", GUILayout.Height(50))) mic.StartMic();
        if (GUILayout.Button("Kalibrasi ulang", GUILayout.Height(50))) mic.Recalibrate();

        GUILayout.Label("Platform: " + Application.platform);
        #if !UNITY_WEBGL || UNITY_EDITOR
            GUILayout.Label("Perangkat: " + (Microphone.devices.Length > 0 ? Microphone.devices[0] : "tidak ada"));
        #endif
            GUILayout.Label("IsRunning: " + mic.IsRunning);
            GUILayout.Label("IsDenied: " + mic.IsDenied);
            GUILayout.Label("IsCalibrating: " + mic.IsCalibrating);

            GUILayout.Label("Level mik (0-100): " + mic.Level.ToString("F0"));
            GUILayout.HorizontalSlider(mic.Level, 0f, 100f, GUILayout.Width(500));

            float total = NoiseMath.Combine(gameNoise, mic.Level);
            GUILayout.Label("Suara total (game " + gameNoise.ToString("F0") + " + mik): " + total.ToString("F0"));
            GUILayout.Label("Jarak dengar makhluk: " + NoiseMath.HearingRadius(total).ToString("F1") + " m");

            GUILayout.Label("Kepekaan: " + mic.sensitivity.ToString("F2"));
            mic.sensitivity = GUILayout.HorizontalSlider(mic.sensitivity, 0.5f, 2f, GUILayout.Width(500));

            GUILayout.EndArea();
    }
}