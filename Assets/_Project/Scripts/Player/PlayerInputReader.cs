using UnityEngine;
using UnityEngine.InputSystem;

namespace TheAlas.Player
{
    /// <summary>
    /// Satu-satunya tempat binding keyboard dan mouse pemain.
    /// Ctrl sengaja tidak dipakai: di build WebGL, Ctrl+W menutup tab browser juri.
    /// Klik kiri dipakai untuk mengunci kursor, jadi interaksi hanya lewat E.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        public const int SlotCount = 9;

        InputAction move;
        InputAction look;
        InputAction sprint;
        InputAction crouch;
        InputAction jump;
        InputAction flashlight;
        InputAction interact;
        InputAction drop;
        InputAction throwItem;
        InputAction scroll;
        InputAction restart;
        InputAction releaseCursor;
        InputAction captureCursor;
        readonly InputAction[] slots = new InputAction[SlotCount];

        public Vector2 Move => move.ReadValue<Vector2>();
        public Vector2 Look => look.ReadValue<Vector2>();
        public bool SprintHeld => sprint.IsPressed();
        public bool CrouchPressed => crouch.WasPressedThisFrame();
        public bool JumpPressed => jump.WasPressedThisFrame();
        public bool FlashlightPressed => flashlight.WasPressedThisFrame();
        public bool InteractPressed => interact.WasPressedThisFrame();
        public bool DropPressed => drop.WasPressedThisFrame();
        public bool ThrowPressed => throwItem.WasPressedThisFrame();
        public bool RestartPressed => restart.WasPressedThisFrame();
        public bool ReleaseCursorPressed => releaseCursor.WasPressedThisFrame();
        public bool CaptureCursorPressed => captureCursor.WasPressedThisFrame();

        /// <summary>-1, 0, atau 1 sesuai arah putaran scroll pada frame ini.</summary>
        public int ScrollStep
        {
            get
            {
                float value = scroll.ReadValue<float>();
                return value > 0.01f ? 1 : value < -0.01f ? -1 : 0;
            }
        }

        /// <summary>Indeks slot (0–8) yang tombol angkanya ditekan frame ini, atau -1.</summary>
        public int SlotPressed
        {
            get
            {
                for (int i = 0; i < SlotCount; i++)
                    if (slots[i].WasPressedThisFrame()) return i;
                return -1;
            }
        }

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
            interact = new InputAction("Interact", binding: "<Keyboard>/e");
            drop = new InputAction("Drop", binding: "<Keyboard>/q");
            throwItem = new InputAction("Throw", binding: "<Keyboard>/g");
            throwItem.AddBinding("<Mouse>/rightButton");
            scroll = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll/y");
            restart = new InputAction("Restart", binding: "<Keyboard>/r");
            releaseCursor = new InputAction("ReleaseCursor", binding: "<Keyboard>/escape");
            captureCursor = new InputAction("CaptureCursor", binding: "<Mouse>/leftButton");

            for (int i = 0; i < SlotCount; i++)
                slots[i] = new InputAction($"Slot{i + 1}", binding: $"<Keyboard>/{i + 1}");
        }

        void OnEnable()
        {
            foreach (var action in AllActions()) action.Enable();
        }

        void OnDisable()
        {
            foreach (var action in AllActions()) action.Disable();
        }

        void OnDestroy()
        {
            foreach (var action in AllActions()) action.Dispose();
        }

        System.Collections.Generic.IEnumerable<InputAction> AllActions()
        {
            yield return move;
            yield return look;
            yield return sprint;
            yield return crouch;
            yield return jump;
            yield return flashlight;
            yield return interact;
            yield return drop;
            yield return throwItem;
            yield return scroll;
            yield return restart;
            yield return releaseCursor;
            yield return captureCursor;
            foreach (var slot in slots) yield return slot;
        }
    }
}
