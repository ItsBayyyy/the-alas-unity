using UnityEngine;
using UnityEngine.InputSystem;

namespace TheAlas.Player
{
    /// <summary>
    /// Satu-satunya tempat binding keyboard dan mouse pemain.
    /// Ctrl sengaja tidak dipakai: di build WebGL, Ctrl+W menutup tab browser juri.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        InputAction move;
        InputAction look;
        InputAction sprint;
        InputAction crouch;
        InputAction jump;
        InputAction flashlight;
        InputAction releaseCursor;
        InputAction captureCursor;

        public Vector2 Move => move.ReadValue<Vector2>();
        public Vector2 Look => look.ReadValue<Vector2>();
        public bool SprintHeld => sprint.IsPressed();
        public bool CrouchPressed => crouch.WasPressedThisFrame();
        public bool JumpPressed => jump.WasPressedThisFrame();
        public bool FlashlightPressed => flashlight.WasPressedThisFrame();
        public bool ReleaseCursorPressed => releaseCursor.WasPressedThisFrame();
        public bool CaptureCursorPressed => captureCursor.WasPressedThisFrame();

        void Awake()
        {
            move = new InputAction("Move");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            look = new InputAction("Look", binding: "<Mouse>/delta");
            sprint = new InputAction("Sprint", binding: "<Keyboard>/leftShift");
            crouch = new InputAction("Crouch", binding: "<Keyboard>/c");
            jump = new InputAction("Jump", binding: "<Keyboard>/space");
            flashlight = new InputAction("Flashlight", binding: "<Keyboard>/f");
            releaseCursor = new InputAction("ReleaseCursor", binding: "<Keyboard>/escape");
            captureCursor = new InputAction("CaptureCursor", binding: "<Mouse>/leftButton");
        }

        void OnEnable() => SetEnabled(true);
        void OnDisable() => SetEnabled(false);

        void OnDestroy()
        {
            foreach (var action in AllActions())
                action.Dispose();
        }

        void SetEnabled(bool enabled)
        {
            foreach (var action in AllActions())
            {
                if (enabled) action.Enable();
                else action.Disable();
            }
        }

        InputAction[] AllActions() =>
            new[] { move, look, sprint, crouch, jump, flashlight, releaseCursor, captureCursor };
    }
}
