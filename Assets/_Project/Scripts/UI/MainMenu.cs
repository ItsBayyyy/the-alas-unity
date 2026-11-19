using System.Collections;
using TheAlas.Core;
using TheAlas.Player;
using UnityEngine;

namespace TheAlas.UI
{
    /// <summary>
    /// Menu utama (desain Citari): Play Game memulai cutscene, Setting membuka pengaturan, Quit keluar.
    /// Musik menu berputar selama menu terbuka dan memudar saat permainan dimulai.
    /// Setelah "Ulang" di layar akhir, menu dan cutscene dilewati.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] UiSkin skin;
        [SerializeField] IntroCutscene intro;
        [SerializeField] AudioSource music;
        [SerializeField] float musicFadeOut = 1.5f;

        // Tombol di mockup menu (1920 × 1080): area klik dan posisi gambar sorotnya.
        static readonly Rect PlayHit = new(780f, 605f, 360f, 85f), SettingHit = new(810f, 700f, 300f, 80f), QuitHit = new(870f, 795f, 180f, 70f);
        static readonly Vector2 PlayArt = new(802f, 622f), SettingArt = new(838f, 710f), QuitArt = new(894f, 805f);

        bool settingsOpen;
        bool started;

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
            // Browser baru membunyikan audio setelah klik pertama; Unity WebGL melanjutkannya sendiri saat itu.
            if (music) music.Play();
        }

        void OnGUI()
        {
            if (started) return;
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
                started = true;
                StartCoroutine(FadeOutMusic());
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

        IEnumerator FadeOutMusic()
        {
            if (music)
            {
                float start = music.volume;
                for (float t = 0f; t < musicFadeOut; t += Time.unscaledDeltaTime)
                {
                    music.volume = Mathf.Lerp(start, 0f, t / musicFadeOut);
                    yield return null;
                }
                music.Stop();
            }
            enabled = false;
        }

        static bool Button(Rect hit, Vector2 art, Texture2D hover) =>
            UiDraw.Button(UiDraw.Fit(hit), UiDraw.Fit(new Rect(art.x, art.y, hover.width, hover.height)), hover);
    }
}
