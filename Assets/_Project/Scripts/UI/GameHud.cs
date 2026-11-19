using TheAlas.Core;
using TheAlas.Creature;
using TheAlas.Items;
using TheAlas.Player;
using TheAlas.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheAlas.UI
{
    /// <summary>
    /// HUD permainan dari desain Citari: timer, bar Suara dan Nafas, slot barang, petunjuk tombol,
    /// kertas tugas (H), daftar kontrol (tahan Tab), jeda (Esc), bunyi (M), layar penuh (F),
    /// dan layar akhir GAME OVER / YOU ESCAPE dengan tombol Ulang dan Menu.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [SerializeField] UiSkin skin;
        [SerializeField] RoundManager round;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerNoise noise;
        [SerializeField] PlayerInventory inventory;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] PlayerLook look;
        [SerializeField] Car car;
        [SerializeField] ItemSpawner spawner;
        [SerializeField] Transform viewPoint;
        [SerializeField] CreatureAI creature;
        [Tooltip("Suara di atas angka ini membuat makhluk memburu dari 16 m (GDD)")]
        [SerializeField] float loudThreshold = 38f;
        [Tooltip("Kertas tugas tampil sendiri selama ini di awal ronde, supaya pemain tahu tujuannya")]
        [SerializeField] float taskIntroDuration = 8f;
        [Tooltip("Jarak makhluk dari pemain saat dipanggil dengan F2 (tersembunyi)")]
        [SerializeField] float summonDistance = 10f;

        // Kertas tugas (mockup 734 × 1157): posisi tulisan tiap baris, kotak, dan centangnya.
        const float PaperWidth = 734f, PaperHeight = 1157f, MaxLabelHeight = 90f;
        static readonly Vector2[] TaskLabels = { new(310, 195), new(310, 370), new(309, 556), new(310, 715), new(309, 866) };
        static readonly Vector2 BoxOffset = new(136f - 310f, -41f), CheckOffset = new(102f - 310f, -91f);

        // Layar akhir (1920 × 1080): tombol Ulang dan Menu.
        static readonly Rect UlangHit = new(840f, 668f, 240f, 95f), MenuHit = new(840f, 843f, 240f, 85f);
        static readonly Vector2 UlangArt = new(872f, 686f), MenuArt = new(872f, 860f);

        bool showTasks;
        float taskUntil;
        bool paused;
        bool wasLocked;
        bool showDebug;

        void OnEnable() => taskUntil = Time.time + taskIntroDuration;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || round.IsOver) return;

            if (keyboard.fKey.wasPressedThisFrame) Screen.fullScreen = !Screen.fullScreen;
            if (keyboard.mKey.wasPressedThisFrame) GameSettings.ToggleMute();
            if (paused) return;

            if (keyboard.hKey.wasPressedThisFrame) showTasks = !showTasks;

            // Jeda dengan Esc. Di browser, Esc juga melepas kunci kursor tanpa sampai ke game,
            // jadi kursor yang lepas setelah sempat terkunci dihitung sebagai jeda juga.
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (keyboard.escapeKey.wasPressedThisFrame || (wasLocked && !locked)) Pause();
            wasLocked = locked;

            // Tombol tersembunyi untuk demo dan playtest di build mana pun; sengaja tidak ditulis di UI.
            if (keyboard.f2Key.wasPressedThisFrame) SummonCreature();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (keyboard.f1Key.wasPressedThisFrame) showDebug = !showDebug;
#endif
        }

        void Pause()
        {
            paused = true;
            SetPlayerActive(false);
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Resume()
        {
            paused = false;
            wasLocked = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SetPlayerActive(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void SetPlayerActive(bool active)
        {
            motor.enabled = active;
            look.enabled = active;
            inventory.enabled = active;
            interactor.enabled = active;
        }

        /// <summary>
        /// Memanggil makhluk ke belakang pemain tanpa tanda apa pun di layar, supaya AI bisa diuji
        /// atau didemokan tanpa menunggu ia berjalan dari ujung selatan. Kalau di belakang di luar
        /// peta, dicoba di depan.
        /// </summary>
        void SummonCreature()
        {
            Vector3 back = Vector3.ProjectOnPlane(-viewPoint.forward, Vector3.up).normalized;
            Vector3 origin = motor.transform.position;
            const float maxSnap = 3f; // cukup untuk menghindari pohon, tapi tidak menarik ke tepi peta
            if (!creature.TeleportTo(origin + back * summonDistance, maxSnap))
                creature.TeleportTo(origin - back * summonDistance, maxSnap);
        }

        void OnGUI()
        {
            if (round.IsOver)
            {
                if (!Jumpscare.Playing) DrawEndScreen();
                return;
            }
            if (paused)
            {
                if (SettingsPanel.Draw(skin)) Resume();
                return;
            }

            float s = UiDraw.HudScale;
            DrawTimer(s);
            DrawBars(s);
            DrawSlots(s);
            DrawPrompt(s);
            GUI.DrawTexture(new Rect(Screen.width - (UiDraw.RefWidth - 1645f) * s, 70f * s, skin.hudHints.width * s, skin.hudHints.height * s), skin.hudHints);

            if (showTasks || Time.time < taskUntil) DrawTasks();
            if (Keyboard.current != null && Keyboard.current.tabKey.isPressed) DrawControls();
            if (showDebug) DrawDebug();
        }

        void DrawTimer(float s)
        {
            var color = round.IsFinalMinute ? UiDraw.Rust : UiDraw.Cream;
            UiDraw.ShadowLabel(new Rect(Screen.width / 2f - 160 * s, 60 * s, 320 * s, 66 * s), Format.Clock(round.TimeLeft), 50 * s, color, bold: true);
        }

        void DrawBars(float s)
        {
            float Y(float mockY) => Screen.height - (UiDraw.RefHeight - mockY) * s;
            float total = noise.Total;
            GUI.DrawTexture(new Rect(61 * s, Y(970), skin.hudSuara.width * s, skin.hudSuara.height * s), skin.hudSuara);
            GUI.DrawTexture(new Rect(61 * s, Y(1002), skin.hudNafas.width * s, skin.hudNafas.height * s), skin.hudNafas);
            DrawBar(new Rect(131 * s, Y(973), 286 * s, 11 * s), total / 100f, total > loudThreshold ? UiDraw.Rust : UiDraw.Cream);
            DrawBar(new Rect(131 * s, Y(1003), 286 * s, 11 * s), motor.Stamina / motor.MaxStamina, motor.IsExhausted ? UiDraw.Rust : UiDraw.Cream);
        }

        static void DrawBar(Rect rect, float fraction, Color color)
        {
            UiDraw.Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), new Color(0.16f, 0.15f, 0.12f, 0.9f));
            UiDraw.Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), color);
        }

        /// <summary>Bingkai slot dari desain; minimal tiga, mengecil kalau barang yang dibawa lebih banyak.</summary>
        void DrawSlots(float s)
        {
            var items = inventory.Items;
            int count = Mathf.Max(3, items.Count);
            float k = s * Mathf.Min(1f, 3.4f / count);
            float w = skin.hudSlot.width * k, h = skin.hudSlot.height * k, step = 217.5f * k;
            float x0 = Screen.width / 2f - (step * (count - 1) + w) / 2f;
            float y = Screen.height - (UiDraw.RefHeight - 836f) * s - (h - skin.hudSlot.height * s);

            for (int i = 0; i < count; i++)
            {
                var rect = new Rect(x0 + i * step, y, w, h);
                bool selected = i < items.Count && i == inventory.SelectedIndex;
                var previous = GUI.color;
                GUI.color = selected ? UiDraw.Rust : Color.white;
                GUI.DrawTexture(rect, skin.hudSlot);
                GUI.color = previous;
                if (i >= items.Count) continue;

                var data = items[i].Data;
                var inner = new Rect(rect.x + 14 * k, rect.y + 14 * k, rect.width - 28 * k, rect.height - 28 * k);
                if (data.Icon)
                {
                    // Ikon mengisi bagian atas slot; nama dan beban di bawahnya.
                    float label = 40 * k;
                    GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width, inner.height - label), data.Icon, ScaleMode.ScaleToFit);
                    UiDraw.ShadowLabel(new Rect(inner.x, inner.yMax - label, inner.width, label),
                        $"<b>{data.DisplayName}</b>\n<size={Mathf.RoundToInt(14 * k)}>beban {Format.Number(data.Weight)}</size>", 17 * k, UiDraw.Cream);
                }
                else
                    UiDraw.ShadowLabel(inner, $"<b>{data.DisplayName}</b>\n<size={Mathf.RoundToInt(17 * k)}>beban {Format.Number(data.Weight)}</size>", 23 * k, UiDraw.Cream);
                UiDraw.ShadowLabel(new Rect(rect.x + 10 * k, rect.y + 6 * k, 40 * k, 30 * k), (i + 1).ToString(), 18 * k, selected ? UiDraw.Rust : UiDraw.Cream, TextAnchor.UpperLeft);
            }
        }

        void DrawPrompt(float s)
        {
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            UiDraw.Fill(new Rect(cx - 2, cy - 2, 4, 4), new Color(UiDraw.Cream.r, UiDraw.Cream.g, UiDraw.Cream.b, 0.7f));

            string prompt = round.State == RoundState.Escaping
                ? $"Mesin menyala… bertahan {Format.Number(car.EngineTimeLeft, "0.0")} dtk"
                : interactor.CurrentPrompt;
            if (string.IsNullOrEmpty(prompt)) return;

            // Panel gelap di belakang teks: tanpa itu prompt hilang di tengah sorot senter.
            float width = Mathf.Min(Screen.width * 0.8f, (prompt.Length * 10f + 30f) * s);
            UiDraw.Fill(new Rect(cx - width / 2f, cy + 26 * s, width, 30 * s), UiDraw.Panel);
            UiDraw.Label(new Rect(cx - width / 2f, cy + 26 * s, width, 30 * s), prompt, 18 * s, UiDraw.Cream);
        }

        /// <summary>Kertas tugas: suku cadang, centang yang sudah di mobil, dan landmark tempat mencarinya.</summary>
        void DrawTasks()
        {
            float height = Screen.height * 0.72f, k = height / PaperHeight;
            var paper = new Rect(30f * UiDraw.HudScale, (Screen.height - height) / 2f, PaperWidth * k, height);
            GUI.DrawTexture(paper, skin.taskPaper);
            Rect P(float x, float y, float w, float h) => new(paper.x + x * k, paper.y + y * k, w * k, h * k);

            var parts = car.RequiredParts;
            for (int i = 0; i < parts.Count && i < TaskLabels.Length; i++)
            {
                var part = parts[i];
                Vector2 at = TaskLabels[i];
                var label = i < skin.taskLabels.Length ? skin.taskLabels[i] : null;
                // Tulisan dua baris ("FILTER OIL") diperkecil supaya tetap muat satu baris tugas.
                float labelHeight = label ? Mathf.Min(label.height, MaxLabelHeight) : 80f;
                if (label) GUI.DrawTexture(P(at.x, at.y, label.width * labelHeight / label.height, labelHeight), label);

                var box = P(at.x + BoxOffset.x, at.y + BoxOffset.y, skin.taskBox.width, skin.taskBox.height);
                GUI.DrawTexture(box, skin.taskBox);
                if (car.IsDeposited(part))
                {
                    var check = skin.taskChecks[i % skin.taskChecks.Length];
                    GUI.DrawTexture(P(at.x + CheckOffset.x, at.y + CheckOffset.y, check.width, check.height), check);
                }

                // Keterangan di bawah tulisan nama: lokasi landmark, "dibawa", atau "di mobil".
                UiDraw.Label(P(at.x + 4f, at.y + labelHeight - 4f, PaperWidth - at.x - 40f, 46f), TaskHint(part), 34f * k, UiDraw.Ink, TextAnchor.UpperLeft, bold: true);
            }
        }

        string TaskHint(ItemData part)
        {
            if (car.IsDeposited(part)) return "di mobil";
            if (inventory.Contains(part)) return "dibawa";
            foreach (var a in spawner.Assignments)
                if (a.Part == part) return a.Landmark.DisplayName;
            return "";
        }

        /// <summary>Bagian "Kontrol" dari layar pengaturan, selama Tab ditahan.</summary>
        void DrawControls()
        {
            var section = SettingsPanel.ControlsSection;
            float w = Mathf.Min(Screen.width * 0.7f, section.width * UiDraw.HudScale * 0.85f);
            float h = w * section.height / section.width;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            UiDraw.Fill(rect, Color.black);
            var uv = new Rect(section.x / skin.settings.width, 1f - (section.y + section.height) / skin.settings.height,
                section.width / skin.settings.width, section.height / skin.settings.height);
            GUI.DrawTextureWithTexCoords(rect, skin.settings, uv);
        }

        void DrawEndScreen()
        {
            UiDraw.FullScreen(round.State == RoundState.Won ? skin.escape : skin.gameOver);

            string reason = round.State switch
            {
                RoundState.Won => car.LootCount == 0
                    ? "Hemat beban, selamat nyawa. Di kaca spion, sesuatu berdiri diam."
                    : "Lampu mobil menembus kabut. Di kaca spion, sesuatu berdiri diam.",
                RoundState.Caught => round.LoadAtEnd > 0f
                    ? $"Kamu memikul {Format.Number(round.LoadAtEnd)} beban saat tertangkap. Langkahmu terlalu berat."
                    : "Ia tidak perlu melihat. Ia cukup mendengar.",
                _ => "Waktu habis. Alas tidak pernah melepaskan tamunya."
            };
            string stats = $"Sisa waktu {Format.Clock(round.TimeLeft)}   ·   Beban rata-rata {Format.Number(round.AverageLoad, "0.0")}   ·   " +
                           $"Suku cadang di mobil {car.DepositedPartCount} dari {car.RequiredParts.Count}";
            float s = UiDraw.Fit(new Rect(0, 0, 1, 1)).width;
            UiDraw.Label(UiDraw.Fit(new Rect(260f, 960f, 1400f, 40f)), reason, 26f * s, UiDraw.Cream);
            UiDraw.Label(UiDraw.Fit(new Rect(260f, 1005f, 1400f, 36f)), stats, 20f * s, new Color(UiDraw.Cream.r, UiDraw.Cream.g, UiDraw.Cream.b, 0.7f));

            if (EndButton(UlangHit, UlangArt, skin.endUlangHover)) GameFlow.Reload(skipIntro: true);
            else if (EndButton(MenuHit, MenuArt, skin.endMenuHover)) GameFlow.Reload(skipIntro: false);
        }

        static bool EndButton(Rect hit, Vector2 art, Texture2D hover) =>
            UiDraw.Button(UiDraw.Fit(hit), UiDraw.Fit(new Rect(art.x, art.y, hover.width, hover.height)), hover);

        void DrawDebug()
        {
            string text =
                $"[F1] Makhluk: {creature.State}, jarak {creature.DistanceToPlayer:0.0} m\n" +
                $"Suara tubuh {noise.BodyNoise:0}, total {noise.Total:0}, permukaan {noise.CurrentSurface}\n" +
                $"Kecepatan {motor.HorizontalSpeed:0.00} m/s, lompat {motor.JumpHeight:0.00} m";
            var rect = new Rect(16, 150, 420, 70);
            UiDraw.Fill(rect, UiDraw.Panel);
            UiDraw.Label(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, rect.height - 8), text, 13, UiDraw.Cream, TextAnchor.UpperLeft);
        }
    }
}
