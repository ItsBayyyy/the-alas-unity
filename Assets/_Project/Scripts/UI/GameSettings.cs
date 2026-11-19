using System;
using UnityEngine;

namespace TheAlas.UI
{
    /// <summary>Pengaturan pemain, disimpan di PlayerPrefs (di WebGL: penyimpanan browser).</summary>
    public static class GameSettings
    {
        public enum Slider { Sensitivity, Brightness, Volume, FieldOfView, MicSensitivity }

        public static readonly string[] QualityNames = { "RENDAH", "SEDANG", "TINGGI" };

        public static event Action Changed;

        /// <summary>Bunyi dimatikan sementara lewat tombol M; tidak disimpan.</summary>
        public static bool Muted { get; private set; }

        /// <summary>Nilai slider 0–1, sesuai posisi pegangan di layar pengaturan.</summary>
        public static float Get(Slider slider) => PlayerPrefs.GetFloat(Key(slider), Default(slider));

        public static void Set(Slider slider, float value)
        {
            PlayerPrefs.SetFloat(Key(slider), Mathf.Clamp01(value));
            Changed?.Invoke();
        }

        public static int Quality
        {
            get => PlayerPrefs.GetInt("alas.quality", 2);
            set
            {
                PlayerPrefs.SetInt("alas.quality", (int)Mathf.Repeat(value, QualityNames.Length));
                Changed?.Invoke();
            }
        }

        /// <summary>Mikrofon ikut terdengar oleh makhluk (bawaan menyala, sesuai desain).</summary>
        public static bool MicEnabled
        {
            get => PlayerPrefs.GetInt("alas.mic", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("alas.mic", value ? 1 : 0);
                Changed?.Invoke();
            }
        }

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt("alas.invertY", 0) == 1;
            set
            {
                PlayerPrefs.SetInt("alas.invertY", value ? 1 : 0);
                Changed?.Invoke();
            }
        }

        public static void ToggleMute()
        {
            Muted = !Muted;
            Changed?.Invoke();
        }

        // Nilai nyata dari posisi slider.
        public static float SensitivityMultiplier => Mathf.Lerp(0.25f, 2f, Get(Slider.Sensitivity));
        public static float BrightnessMultiplier => Mathf.Lerp(0.5f, 2.5f, Get(Slider.Brightness));
        public static float Volume => Get(Slider.Volume);
        public static float FieldOfView => Mathf.Lerp(55f, 95f, Get(Slider.FieldOfView));

        static string Key(Slider slider) => $"alas.{slider}";

        // Bawaan setara pengaturan sebelum ada layar ini: sensitivitas 1×, terang 1×, FOV 70; kepekaan mikrofon di tengah.
        static float Default(Slider slider) => slider switch
        {
            Slider.Sensitivity => Mathf.InverseLerp(0.25f, 2f, 1f),
            Slider.Brightness => Mathf.InverseLerp(0.5f, 2.5f, 1f),
            Slider.FieldOfView => Mathf.InverseLerp(55f, 95f, 70f),
            Slider.MicSensitivity => 0.5f,
            _ => 1f
        };
    }
}
