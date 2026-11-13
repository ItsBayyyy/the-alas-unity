using UnityEngine;

namespace TheAlas.Player
{
    /// <summary>
    /// Menoleh dengan mouse: yaw memutar badan pemain, pitch hanya memutar kamera.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] Transform cameraRoot;
        [Tooltip("Derajat per piksel gerak mouse")]
        [SerializeField, Range(0.01f, 0.5f)] float sensitivity = 0.1f;
        [SerializeField, Range(60f, 89f)] float maxPitch = 85f;

        PlayerInputReader input;
        float yaw;
        float pitch;

        public float Sensitivity
        {
            get => sensitivity;
            set => sensitivity = Mathf.Clamp(value, 0.01f, 0.5f);
        }

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            yaw = transform.eulerAngles.y;
        }

        void Start() => SetCursorCaptured(true);

        void Update()
        {
            HandleCursorCapture();
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 delta = input.Look * sensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void HandleCursorCapture()
        {
            if (input.ReleaseCursorPressed) SetCursorCaptured(false);
            else if (input.CaptureCursorPressed) SetCursorCaptured(true);
        }

        static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
