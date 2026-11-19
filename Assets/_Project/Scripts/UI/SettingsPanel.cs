using UnityEngine;

namespace TheAlas.UI
{
    /// <summary>
    /// Layar Pengaturan dari desain Citari (1200 × 2000, digulir). Gambar mockup menjadi latar; slider,
    /// persen, pilihan kualitas, dan centang digambar di atasnya sesuai <see cref="GameSettings"/>.
    /// Bagian mikrofon diredupkan: Unity WebGL tidak bisa membaca mikrofon.
    /// </summary>
    public static class SettingsPanel
    {
        const float Width = 1200f, Height = 2000f;

        // Posisi dalam koordinat mockup pengaturan.
        static readonly float[] SliderRows = { 1215f, 1271f, 1325f, 1381f, 1437f };
        const float TrackLeft = 578f, TrackRight = 992f, TrackHeight = 22f;
        static readonly Rect QualityBox = new(718f, 1481f, 277f, 56f);
        static readonly Rect InvertBox = new(577f, 1568f, 49f, 48f);
        static readonly Rect CheckArt = new(574f, 1566f, 53f, 55f);
        static readonly Rect EnterButton = new(430f, 1735f, 350f, 125f);
        static readonly Rect MicSection = new(34f, 34f, 1129f, 327f);
        public static readonly Rect ControlsSection = new(34f, 388f, 1129f, 686f);

        static Vector2 scroll;
        static int dragging = -1;

        /// <summary>Menggambar layar; true saat "Masuk Ke Alas" ditekan.</summary>
        public static bool Draw(UiSkin skin)
        {
            UiDraw.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.07f, 0.07f, 0.05f, 1f));
            float s = Screen.height / 1080f * 0.8f;
            float viewWidth = Mathf.Min(Width * s, Screen.width);
            var view = new Rect((Screen.width - viewWidth) / 2f, 0f, viewWidth + 16f, Screen.height);
            var content = new Rect(0f, 0f, Width * s, Height * s);
            Rect M(Rect r) => new(r.x * s, r.y * s, r.width * s, r.height * s);

            scroll = GUI.BeginScrollView(view, scroll, content, false, true);
            GUI.DrawTexture(content, skin.settings);

            UiDraw.Fill(M(MicSection), new Color(0.05f, 0.05f, 0.04f, 0.72f));
            UiDraw.Label(M(new Rect(60f, 290f, 1080f, 60f)), "Mikrofon belum tersedia di versi browser", 30f * s, UiDraw.Cream);

            for (int i = 0; i < SliderRows.Length; i++)
                DrawSlider(i, s, enabled: (GameSettings.Slider)i != GameSettings.Slider.MicSensitivity);

            UiDraw.Label(M(new Rect(QualityBox.x + 22f, QualityBox.y, 200f, QualityBox.height)),
                GameSettings.QualityNames[GameSettings.Quality], 30f * s, UiDraw.Cream, TextAnchor.MiddleLeft);
            if (GUI.Button(M(QualityBox), GUIContent.none, GUIStyle.none)) GameSettings.Quality++;

            if (GameSettings.InvertY) GUI.DrawTexture(M(CheckArt), skin.settingsCheck);
            if (GUI.Button(M(InvertBox), GUIContent.none, GUIStyle.none)) GameSettings.InvertY = !GameSettings.InvertY;

            var enter = M(EnterButton);
            if (enter.Contains(Event.current.mousePosition)) UiDraw.Fill(enter, new Color(UiDraw.Rust.r, UiDraw.Rust.g, UiDraw.Rust.b, 0.25f));
            bool closed = GUI.Button(enter, GUIContent.none, GUIStyle.none);

            GUI.EndScrollView();
            if (closed) dragging = -1;
            return closed;
        }

        static void DrawSlider(int row, float s, bool enabled)
        {
            var slider = (GameSettings.Slider)row;
            float value = GameSettings.Get(slider);
            float y = SliderRows[row] * s;
            var track = new Rect(TrackLeft * s, y - TrackHeight * s / 2f, (TrackRight - TrackLeft) * s, TrackHeight * s);

            var color = enabled ? UiDraw.Cream : new Color(0.45f, 0.43f, 0.38f);
            UiDraw.Fill(track, new Color(0.16f, 0.15f, 0.12f));
            UiDraw.Fill(new Rect(track.x, track.y, track.width * value, track.height), color);
            UiDraw.Fill(new Rect(track.x + track.width * value - 10f * s, y - 20f * s, 20f * s, 40f * s), color);
            UiDraw.Label(new Rect(1035f * s, y - 25f * s, 100f * s, 50f * s), $"{value * 100f:0}%", 30f * s, color, TextAnchor.MiddleRight);
            if (!enabled) return;

            var hit = new Rect(track.x - 12f * s, y - 26f * s, track.width + 24f * s, 52f * s);
            var e = Event.current;
            if (e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) dragging = row;
            if (e.type == EventType.MouseUp) dragging = -1;
            if (dragging == row && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                GameSettings.Set(slider, Mathf.InverseLerp(track.x, track.xMax, e.mousePosition.x));
                e.Use();
            }
        }
    }
}
