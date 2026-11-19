using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheAlas.Core
{
    /// <summary>Perpindahan antarronde: main lagi langsung, atau kembali ke menu utama.</summary>
    public static class GameFlow
    {
        /// <summary>
        /// Diset sebelum scene dimuat ulang lewat "Ulang": ronde berikutnya langsung mulai tanpa
        /// menu dan cutscene. Dibaca sekali oleh menu utama.
        /// </summary>
        public static bool SkipIntroOnce { get; set; }

        public static void Reload(bool skipIntro)
        {
            SkipIntroOnce = skipIntro;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
