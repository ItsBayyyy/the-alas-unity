using UnityEngine;

namespace TheAlas.UI
{
    /// <summary>
    /// Semua gambar UI dari desain Citari di satu tempat. Koordinat yang dipakai komponen UI adalah
    /// koordinat piksel di mockup aslinya (1920 × 1080; pengaturan 1200 × 2000; kertas tugas 734 × 1157).
    /// Diisi otomatis oleh builder dari <c>Assets/_Project/UI/Sprites/UI_*.png</c>.
    /// </summary>
    public class UiSkin : ScriptableObject
    {
        [Header("Menu utama")]
        public Texture2D menu;
        public Texture2D menuPlayHover;
        public Texture2D menuSettingHover;
        public Texture2D menuQuitHover;

        [Header("Cutscene")]
        public Texture2D story;
        public Texture2D storySkipHover;

        [Header("Layar akhir")]
        public Texture2D gameOver;
        public Texture2D escape;
        public Texture2D endUlangHover;
        public Texture2D endMenuHover;

        [Header("Pengaturan")]
        public Texture2D settings;
        public Texture2D settingsCheck;

        [Header("HUD")]
        public Texture2D hudSlot;
        public Texture2D hudSuara;
        public Texture2D hudNafas;
        public Texture2D hudHints;

        [Header("Kertas tugas")]
        public Texture2D taskPaper;
        public Texture2D taskBox;
        public Texture2D[] taskChecks;
        [Tooltip("Tulisan nama suku cadang, urutannya sama dengan daftar suku cadang di mobil")]
        public Texture2D[] taskLabels;
    }
}
