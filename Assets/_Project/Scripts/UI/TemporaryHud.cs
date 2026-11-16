using System.Linq;
using TheAlas.Core;
using TheAlas.Creature;
using TheAlas.Items;
using TheAlas.Player;
using TheAlas.World;
using UnityEngine;

namespace TheAlas.UI
{
    /// <summary>
    /// HUD sementara (IMGUI) untuk vertical slice, sampai HUD dari Citari masuk.
    /// Tata letak mengikuti GDD: tugas kiri atas, timer dan kompas tengah atas,
    /// bar Suara dan Nafas kiri bawah, beban kanan bawah, slot tengah bawah.
    /// </summary>
    public class TemporaryHud : MonoBehaviour
    {
        [SerializeField] RoundManager round;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerNoise noise;
        [SerializeField] PlayerInventory inventory;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] Car car;
        [SerializeField] ItemSpawner spawner;
        [SerializeField] Transform viewPoint;
        [SerializeField] CreatureAI creature;
        [Tooltip("Suara di atas angka ini membuat makhluk memburu dari 16 m (GDD)")]
        [SerializeField] float loudThreshold = 38f;
        [Tooltip("Jarak makhluk dari pemain saat dipanggil dengan F2 (playtest)")]
        [SerializeField] float summonDistance = 20f;

        static readonly Color Cream = new Color32(0xE3, 0xD8, 0xBD, 0xFF);
        static readonly Color Lantern = new Color32(0xE0, 0xA6, 0x3C, 0xFF);
        static readonly Color Rust = new Color32(0xB8, 0x35, 0x2B, 0xFF);
        static readonly Color Panel = new(0.05f, 0.06f, 0.04f, 0.72f);
        static readonly (float angle, string label)[] Cardinals =
        {
            (0f, "U"), (45f, "TL"), (90f, "T"), (135f, "TG"), (180f, "S"), (225f, "BD"), (270f, "B"), (315f, "BL")
        };

        GUIStyle label;
        GUIStyle small;
        GUIStyle centered;
        GUIStyle title;
        bool showDebug;
        float summonNoticeUntil;

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) showDebug = !showDebug;
            if (keyboard.f2Key.wasPressedThisFrame && !round.IsOver) SummonCreature();
#endif
        }

        /// <summary>
        /// Alat playtest: memanggil makhluk ke belakang pemain supaya AI bisa diuji tanpa menunggu
        /// ia berjalan dari ujung selatan. Kalau di belakang di luar peta, dicoba di depan.
        /// </summary>
        void SummonCreature()
        {
            Vector3 back = Vector3.ProjectOnPlane(-viewPoint.forward, Vector3.up).normalized;
            Vector3 origin = motor.transform.position;
            const float maxSnap = 3f; // cukup untuk menghindari pohon, tapi tidak menarik ke tepi peta
            if (creature.TeleportTo(origin + back * summonDistance, maxSnap) ||
                creature.TeleportTo(origin - back * summonDistance, maxSnap))
                summonNoticeUntil = Time.unscaledTime + 2f;
        }

        void OnGUI()
        {
            EnsureStyles();
            // Teks digambar putih lalu diwarnai lewat GUI.color, jadi warna default HUD adalah krem.
            GUI.color = Cream;

            if (round.IsOver)
            {
                DrawEndScreen();
                return;
            }

            DrawTasks();
            DrawTimerAndCompass();
            DrawBars();
            DrawLoad();
            DrawSlots();
            DrawCrosshairAndPrompt();
            if (showDebug) DrawDebug();
            if (Time.unscaledTime < summonNoticeUntil)
                GUI.Label(new Rect(Screen.width / 2f - 250, 100, 500, 24),
                    $"[F2] Makhluk dipanggil ke {summonDistance:0} m di belakangmu", centered);
        }

        void DrawTasks()
        {
            var lines = spawner.Assignments.Select(a =>
            {
                string status = car.IsDeposited(a.Part) ? "✓" : inventory.Contains(a.Part) ? "•" : "  ";
                return $"{status} {a.Part.DisplayName}  <color=#8C8573>— {a.Landmark.DisplayName}</color>";
            });

            var rect = new Rect(16, 16, 330, 30 + spawner.Assignments.Count * 22);
            Fill(rect, Panel);
            GUI.Label(new Rect(28, 22, 310, 22), "<b>Suku cadang</b>", label);
            GUI.Label(new Rect(28, 44, 320, rect.height), string.Join("\n", lines), small);
        }

        void DrawTimerAndCompass()
        {
            float cx = Screen.width / 2f;
            GUI.color = round.IsFinalMinute ? Rust : Cream;
            GUI.Label(new Rect(cx - 60, 10, 120, 36), $"<size=28><b>{Format.Clock(round.TimeLeft)}</b></size>", centered);
            GUI.color = Cream;

            var strip = new Rect(cx - 300, 50, 600, 26);
            Fill(strip, Panel);
            float heading = viewPoint.eulerAngles.y;

            foreach (var (angle, text) in Cardinals)
                DrawCompassMark(strip, heading, angle, text.Length == 1 ? $"<b>{text}</b>" : text, Cream);

            DrawCompassTarget(strip, heading, car.transform.position, "MOBIL", Lantern);
            foreach (var a in spawner.Assignments)
            {
                if (car.IsDeposited(a.Part) || inventory.Contains(a.Part)) continue;
                DrawCompassTarget(strip, heading, a.Landmark.transform.position, a.Landmark.DisplayName, Cream);
            }

            Fill(new Rect(cx - 1, strip.y, 2, strip.height), Lantern);
        }

        void DrawCompassTarget(Rect strip, float heading, Vector3 worldPos, string name, Color color)
        {
            Vector3 delta = worldPos - viewPoint.position;
            float bearing = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            DrawCompassMark(strip, heading, bearing, $"▼ {name} {distance:0}m", color, below: true);
        }

        void DrawCompassMark(Rect strip, float heading, float bearing, string text, Color color, bool below = false)
        {
            float delta = Mathf.DeltaAngle(heading, bearing);
            if (Mathf.Abs(delta) > 90f) return;

            float x = strip.center.x + delta / 90f * (strip.width / 2f);
            float y = below ? strip.yMax + 2 : strip.y + 3;
            GUI.color = color;
            GUI.Label(new Rect(x - 90, y, 180, 22), text, below ? small : centered);
            GUI.color = Cream;
        }

        void DrawBars()
        {
            float y = Screen.height - 86;
            float total = noise.Total;
            DrawBar(new Rect(16, y, 260, 18), "Suara", total / 100f, total > loudThreshold ? Rust : Cream);
            DrawBar(new Rect(16, y + 30, 260, 18), "Nafas", motor.Stamina / motor.MaxStamina,
                motor.IsExhausted ? Rust : new Color(0.55f, 0.65f, 0.5f));
        }

        void DrawBar(Rect rect, string name, float fraction, Color color)
        {
            GUI.Label(new Rect(rect.x, rect.y - 2, 60, 22), name, small);
            var bar = new Rect(rect.x + 60, rect.y + 2, rect.width - 60, rect.height - 4);
            Fill(bar, Panel);
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fraction), bar.height), color);
        }

        void DrawLoad()
        {
            var rect = new Rect(Screen.width - 196, Screen.height - 86, 180, 66);
            Fill(rect, Panel);
            GUI.Label(new Rect(rect.x + 12, rect.y + 6, 170, 60),
                $"<size=20><b>Beban {Format.Number(inventory.TotalWeight)}</b></size>\nLangkah {motor.SpeedMultiplier * 100f:0}%",
                label);
        }

        void DrawSlots()
        {
            const float w = 118, h = 44, gap = 6;
            var items = inventory.Items;
            float y = Screen.height - h - 16;

            if (items.Count == 0)
            {
                GUI.Label(new Rect(Screen.width / 2f - 100, y + 12, 200, 22), "Tangan kosong", centered);
                return;
            }

            float startX = Screen.width / 2f - (items.Count * (w + gap) - gap) / 2f;
            for (int i = 0; i < items.Count; i++)
            {
                var rect = new Rect(startX + i * (w + gap), y, w, h);
                bool selected = i == inventory.SelectedIndex;
                Fill(rect, Panel);
                if (selected) Outline(rect, Lantern);

                var data = items[i].Data;
                GUI.color = data.IsCarPart ? Lantern : Cream;
                GUI.Label(new Rect(rect.x + 8, rect.y + 3, w - 12, h),
                    $"{i + 1}  {data.DisplayName}\n<size=11>beban {Format.Number(data.Weight)}</size>", small);
                GUI.color = Cream;
            }

            GUI.Label(new Rect(Screen.width / 2f - 200, y - 22, 400, 20),
                "<size=11>Q buang (jongkok: letakkan pelan) · G / klik kanan lempar</size>", centered);
        }

        void DrawCrosshairAndPrompt()
        {
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            Fill(new Rect(cx - 2, cy - 2, 4, 4), new Color(Cream.r, Cream.g, Cream.b, 0.7f));

            string prompt = round.State == RoundState.Escaping
                ? $"Mesin menyala… bertahan {Format.Number(car.EngineTimeLeft, "0.0")} dtk"
                : interactor.CurrentPrompt;

            if (string.IsNullOrEmpty(prompt)) return;

            // Panel gelap di belakang teks: tanpa itu prompt hilang di tengah sorot senter.
            Vector2 size = centered.CalcSize(new GUIContent(prompt));
            Fill(new Rect(cx - size.x / 2f - 10, cy + 26, size.x + 20, 24), Panel);
            GUI.Label(new Rect(cx - 250, cy + 25, 500, 26), prompt, centered);
        }

        void DrawEndScreen()
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.03f, 0.03f, 0.02f, 0.88f));

            (string heading, string sub) = round.State switch
            {
                RoundState.Won => ("Kamu lolos", car.LootCount == 0
                    ? "Hemat beban, selamat nyawa. Di kaca spion, sesuatu berdiri diam."
                    : "Lampu mobil menembus kabut. Di kaca spion, sesuatu berdiri diam."),
                RoundState.Caught => ("Tertangkap", round.LoadAtEnd > 0f
                    ? $"Kamu memikul {Format.Number(round.LoadAtEnd)} beban saat tertangkap. Langkahmu terlalu berat."
                    : "Ia tidak perlu melihat. Ia cukup mendengar."),
                _ => ("Waktu habis", "Alas tidak pernah melepaskan tamunya.")
            };

            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            GUI.color = round.State == RoundState.Won ? Cream : Rust;
            GUI.Label(new Rect(cx - 300, cy - 120, 600, 60), heading, title);
            GUI.color = Cream;
            GUI.Label(new Rect(cx - 300, cy - 50, 600, 30), sub, centered);

            string stats =
                $"Sisa waktu  {Format.Clock(round.TimeLeft)}\n" +
                $"Beban rata-rata  {Format.Number(round.AverageLoad, "0.0")}\n" +
                $"Suku cadang di mobil  {car.DepositedPartCount} dari {car.RequiredParts.Count}";
            GUI.Label(new Rect(cx - 300, cy, 600, 80), stats, centered);
            GUI.Label(new Rect(cx - 300, cy + 100, 600, 30), "<b>Tekan R untuk main lagi</b>", centered);
        }

        void DrawDebug()
        {
            string text =
                $"[F1] Makhluk: {creature.State}, jarak {creature.DistanceToPlayer:0.0} m\n" +
                $"Suara tubuh {noise.BodyNoise:0}, total {noise.Total:0}, permukaan {noise.CurrentSurface}\n" +
                $"Kecepatan {motor.HorizontalSpeed:0.00} m/s, lompat {motor.JumpHeight:0.00} m · F2 panggil makhluk";
            var rect = new Rect(16, 150, 380, 64);
            Fill(rect, Panel);
            GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width, rect.height), text, small);
        }

        void EnsureStyles()
        {
            if (label != null) return;

            label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 13 };
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            title = new GUIStyle(centered) { fontSize = 44, fontStyle = FontStyle.Bold };
        }

        static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void Outline(Rect rect, Color color)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, 2), color);
            Fill(new Rect(rect.x, rect.yMax - 2, rect.width, 2), color);
            Fill(new Rect(rect.x, rect.y, 2, rect.height), color);
            Fill(new Rect(rect.xMax - 2, rect.y, 2, rect.height), color);
        }
    }
}
