using System.Globalization;

namespace TheAlas.Core
{
    /// <summary>
    /// Angka dengan koma desimal ala Indonesia. Tidak memakai CultureInfo("id-ID")
    /// karena build WebGL berjalan dengan globalisasi invarian dan bisa gagal memuatnya.
    /// </summary>
    public static class Format
    {
        static readonly NumberFormatInfo Indonesian = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

        public static string Number(float value, string pattern = "0.#") => value.ToString(pattern, Indonesian);

        public static string Clock(float seconds)
        {
            int total = UnityEngine.Mathf.CeilToInt(UnityEngine.Mathf.Max(0f, seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
