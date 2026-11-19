using UnityEngine;

namespace TheAlas.UI
{
    /// <summary>Gambar dan tombol IMGUI dalam koordinat mockup Citari.</summary>
    public static class UiDraw
    {
        public const float RefWidth = 1920f;
        public const float RefHeight = 1080f;

        public static readonly Color Cream = new Color32(0xE3, 0xD8, 0xBD, 0xFF);
        public static readonly Color Rust = new Color32(0xB8, 0x35, 0x2B, 0xFF);
        public static readonly Color Ink = new Color32(0x2B, 0x22, 0x18, 0xFF);
        public static readonly Color Panel = new(0.05f, 0.06f, 0.04f, 0.72f);

        static GUIStyle text;

        /// <summary>Skala HUD: mockup 1080 piksel tingginya.</summary>
        public static float HudScale => Screen.height / RefHeight;

        /// <summary>Warna latar mockup, untuk mengisi sisa layar di luar desain 16:9.</summary>
        static readonly Color Backdrop = new Color32(0x16, 0x15, 0x10, 0xFF);

        /// <summary>
        /// Rect mockup layar penuh ke layar: seluruh desain selalu terlihat (tombol tidak pernah
        /// terpotong), dipusatkan; sisa layar di rasio lain diisi warna latar.
        /// </summary>
        public static Rect Fit(Rect mock)
        {
            float s = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            float ox = (Screen.width - RefWidth * s) / 2f, oy = (Screen.height - RefHeight * s) / 2f;
            return new Rect(ox + mock.x * s, oy + mock.y * s, mock.width * s, mock.height * s);
        }

        public static void FullScreen(Texture texture)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), Backdrop);
            GUI.DrawTexture(Fit(new Rect(0, 0, RefWidth, RefHeight)), texture);
        }

        /// <summary>Tombol tak terlihat di atas tombol yang sudah tergambar di mockup; saat disorot, potongan versi merahnya ditempel.</summary>
        public static bool Button(Rect hit, Rect art, Texture hover)
        {
            if (hover && hit.Contains(Event.current.mousePosition)) GUI.DrawTexture(art, hover);
            return GUI.Button(hit, GUIContent.none, GUIStyle.none);
        }

        public static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static void Label(Rect rect, string content, float size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false)
        {
            text ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            text.alignment = anchor;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.normal.textColor = color;
            GUI.Label(rect, content, text);
        }

        /// <summary>Teks dengan bayangan gelap, supaya terbaca di atas sorot senter.</summary>
        public static void ShadowLabel(Rect rect, string content, float size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false)
        {
            Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), content, size, new Color(0f, 0f, 0f, 0.85f), anchor, bold);
            Label(rect, content, size, color, anchor, bold);
        }
    }
}
