using TheAlas.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheAlas.DebugTools
{
    /// <summary>
    /// Panel uji sementara sampai HUD dari Citari masuk. Tombol - dan = mengubah beban
    /// supaya efek beban bisa dirasakan sebelum sistem inventaris ada.
    /// Hanya aktif di Editor dan development build.
    /// </summary>
    public class DebugStatsOverlay : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] float loadStep = 0.5f;

        GUIStyle style;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.equalsKey.wasPressedThisFrame) motor.Load += loadStep;
            if (keyboard.minusKey.wasPressedThisFrame) motor.Load -= loadStep;
        }

        void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };

            string state = motor.IsCrouching ? "Jongkok" : motor.IsRunning ? "Lari" : "Jalan";
            if (!motor.IsGrounded) state += " (di udara)";
            if (motor.IsExhausted) state += " <color=#B8352B>KELELAHAN</color>";

            string text =
                $"Stamina  {motor.Stamina,5:0} / {motor.MaxStamina:0}\n" +
                $"Kecepatan {motor.HorizontalSpeed,4:0.00} m/s  (x{motor.SpeedMultiplier:0.00})\n" +
                $"Beban     {motor.Load,4:0.0}   [-] / [=]\n" +
                $"Lompat    {motor.JumpHeight,4:0.00} m, biaya {motor.JumpCost:0}\n" +
                $"Status    {state}\n" +
                "<size=13>WASD jalan · Shift lari · C jongkok · Spasi lompat · F senter · Esc lepas kursor</size>";

            GUI.Box(new Rect(12, 12, 420, 140), GUIContent.none);
            GUI.Label(new Rect(22, 18, 410, 140), text, style);
        }
#endif
    }
}
