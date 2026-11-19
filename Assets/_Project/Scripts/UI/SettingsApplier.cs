using TheAlas.Player;
using UnityEngine;

namespace TheAlas.UI
{
    /// <summary>Menerapkan <see cref="GameSettings"/> ke pemain, kamera, cahaya, dan audio.</summary>
    public class SettingsApplier : MonoBehaviour
    {
        [SerializeField] PlayerLook look;
        [SerializeField] Camera playerCamera;
        [SerializeField] Light flashlight;
        [SerializeField] Terrain ground;

        // Kualitas grafis rendah, sedang, tinggi.
        static readonly LightShadows[] FlashlightShadows = { LightShadows.None, LightShadows.Hard, LightShadows.Soft };
        static readonly float[] LodBias = { 0.6f, 0.8f, 1f };
        static readonly float[] GrassDistance = { 12f, 22f, 30f };

        float baseSensitivity;
        Color baseAmbient;
        float baseFlashlight;

        void Awake()
        {
            baseSensitivity = look.Sensitivity;
            baseAmbient = RenderSettings.ambientLight;
            baseFlashlight = flashlight.intensity;
        }

        void OnEnable()
        {
            GameSettings.Changed += Apply;
            Apply();
        }

        void OnDisable() => GameSettings.Changed -= Apply;

        void Apply()
        {
            look.Sensitivity = baseSensitivity * GameSettings.SensitivityMultiplier;
            look.InvertY = GameSettings.InvertY;
            playerCamera.fieldOfView = GameSettings.FieldOfView;

            float brightness = GameSettings.BrightnessMultiplier;
            RenderSettings.ambientLight = baseAmbient * brightness;
            flashlight.intensity = baseFlashlight * Mathf.Lerp(1f, brightness, 0.5f);

            int q = GameSettings.Quality;
            flashlight.shadows = FlashlightShadows[q];
            QualitySettings.lodBias = LodBias[q];
            if (ground) ground.detailObjectDistance = GrassDistance[q];

            AudioListener.volume = GameSettings.Muted ? 0f : GameSettings.Volume;
        }
    }
}
