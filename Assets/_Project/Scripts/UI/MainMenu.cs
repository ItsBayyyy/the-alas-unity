using TheAlas.Core;
using TheAlas.Player;
using UnityEngine;

namespace TheAlas.UI
{
    /// <summary>
    /// Menu utama (desain Citari): Play Game memulai cutscene, Setting membuka pengaturan, Quit keluar.
    /// Setelah "Ulang" di layar akhir, menu dan cutscene dilewati.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] UiSkin skin;
        [SerializeField] IntroCutscene intro;

        // Tombol di mockup menu (1920 × 1080): area klik dan posisi gambar sorotnya.
        static readonly Rect PlayHit = new(780f, 605f, 360f, 85f), SettingHit = new(810f, 700f, 300f, 80f), QuitHit = new(870f, 795f, 180f, 70f);
        static readonly Vector2 PlayArt = new(802f, 622f), SettingArt = new(838f, 710f), QuitArt = new(894f, 805f);

        bool settingsOpen;

        void Start()
        {
            if (GameFlow.SkipIntroOnce)
            {
                GameFlow.SkipIntroOnce = false;
                intro.SkipAll();
                enabled = false;
                return;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnGUI()
        {
            GUI.depth = -10; // di atas layar hitam cutscene
            if (settingsOpen)
            {
                if (SettingsPanel.Draw(skin)) settingsOpen = false;
                return;
            }

            UiDraw.FullScreen(skin.menu);
            if (Button(PlayHit, PlayArt, skin.menuPlayHover))
            {
                MicrophoneInput.Begin(); // browser hanya memberi akses mikrofon dari klik pemain
                intro.Play();
                enabled = false;
            }
            else if (Button(SettingHit, SettingArt, skin.menuSettingHover))
            {
                settingsOpen = true;
            }
            else if (Button(QuitHit, QuitArt, skin.menuQuitHover))
            {
                Application.Quit(); // di WebGL tidak berpengaruh; pemain cukup menutup tab
            }
        }

        static bool Button(Rect hit, Vector2 art, Texture2D hover) =>
            UiDraw.Button(UiDraw.Fit(hit), UiDraw.Fit(new Rect(art.x, art.y, hover.width, hover.height)), hover);
    }
}
