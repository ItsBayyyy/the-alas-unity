using System;
using UnityEngine;

namespace TheAlas.Player
{
    /// <summary>
    /// Gerak first-person: jalan, lari, jongkok, lompat, dan stamina.
    /// Semua angka default diambil dari tabel parameter GDD; beban (load) memotong
    /// kecepatan, stamina, dan tinggi lompatan sesuai rumus di sana.
    /// Pivot transform berada di telapak kaki.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] Transform cameraRoot;

        [Header("Kecepatan (m/s)")]
        [SerializeField] float crouchSpeed = 1.45f;
        [SerializeField] float walkSpeed = 2.9f;
        [SerializeField] float runSpeed = 5.3f;
        [Tooltip("Seberapa cepat kecepatan mencapai target (m/s per detik)")]
        [SerializeField] float acceleration = 14f;

        [Header("Beban")]
        [Tooltip("Sementara diatur manual untuk uji; nanti diisi sistem inventaris")]
        [SerializeField, Min(0f)] float load;
        [SerializeField] float speedLossPerLoad = 0.06f;
        [SerializeField] float minSpeedMultiplier = 0.3f;

        [Header("Stamina")]
        [SerializeField] float maxStamina = 100f;
        [SerializeField] float runDrainBase = 22f;
        [SerializeField] float runDrainPerLoad = 5f;
        [SerializeField] float regenBase = 18f;
        [SerializeField] float regenLossPerLoad = 1.2f;
        [SerializeField] float regenMin = 6f;
        [Tooltip("Setelah stamina habis, lari baru bisa lagi saat stamina mencapai nilai ini")]
        [SerializeField] float exhaustionRecoverAt = 25f;

        [Header("Lompat")]
        [Tooltip("Dipilih supaya batang tumbang 0,45 m tidak terlewati di atas sekitar 3 unit beban")]
        [SerializeField] float baseJumpHeight = 0.6f;
        [SerializeField] float jumpHeightLossPerLoad = 0.07f;
        [SerializeField] float jumpCostBase = 10f;
        [SerializeField] float jumpCostPerLoad = 2f;
        [SerializeField] float gravity = -20f;

        [Header("Jongkok")]
        [SerializeField] float standHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.1f;
        [SerializeField] float standEyeHeight = 1.65f;
        [SerializeField] float crouchEyeHeight = 0.95f;
        [SerializeField] float crouchTransitionSpeed = 6f;

        CharacterController controller;
        PlayerInputReader input;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        bool wasGrounded;

        public event Action Jumped;
        /// <summary>Dipanggil saat menyentuh tanah lagi, dengan kecepatan jatuh (m/s, positif).</summary>
        public event Action<float> Landed;

        public float Load
        {
            get => load;
            set => load = Mathf.Max(0f, value);
        }

        public float Stamina { get; private set; }
        public float MaxStamina => maxStamina;
        public bool IsCrouching { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsExhausted { get; private set; }
        public bool IsGrounded => controller.isGrounded;
        public float HorizontalSpeed => horizontalVelocity.magnitude;
        public float SpeedMultiplier => Mathf.Max(minSpeedMultiplier, 1f - speedLossPerLoad * load);
        public float JumpHeight => baseJumpHeight * Mathf.Max(0f, 1f - jumpHeightLossPerLoad * load);
        public float JumpCost => jumpCostBase + jumpCostPerLoad * load;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
            Stamina = maxStamina;
            ApplyHeight(standHeight);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 moveInput = input.Move;

            if (input.CrouchPressed) ToggleCrouch();

            bool wantsRun = input.SprintHeld && moveInput.y > 0.1f && !IsCrouching;
            UpdateStamina(wantsRun, dt);
            UpdateHorizontal(moveInput, dt);
            UpdateVertical(dt);

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);

            DetectLanding();
            UpdateCrouchTransition(dt);
        }

        void UpdateHorizontal(Vector2 moveInput, float dt)
        {
            float baseSpeed = IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed;
            Vector3 wish = transform.right * moveInput.x + transform.forward * moveInput.y;
            Vector3 target = Vector3.ClampMagnitude(wish, 1f) * (baseSpeed * SpeedMultiplier);
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, acceleration * dt);
        }

        void UpdateVertical(float dt)
        {
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // tekanan kecil ke bawah supaya isGrounded stabil di turunan

            if (input.JumpPressed) TryJump();

            verticalVelocity += gravity * dt;
        }

        void TryJump()
        {
            if (!controller.isGrounded || IsCrouching || Stamina < JumpCost || JumpHeight <= 0.01f) return;

            Stamina -= JumpCost;
            verticalVelocity = Mathf.Sqrt(2f * JumpHeight * -gravity);
            Jumped?.Invoke();
        }

        void DetectLanding()
        {
            bool grounded = controller.isGrounded;
            if (grounded && !wasGrounded)
                Landed?.Invoke(-controller.velocity.y);
            wasGrounded = grounded;
        }

        void UpdateStamina(bool wantsRun, float dt)
        {
            if (IsExhausted && Stamina >= exhaustionRecoverAt)
                IsExhausted = false;

            IsRunning = wantsRun && !IsExhausted;

            if (IsRunning)
            {
                Stamina -= (runDrainBase + runDrainPerLoad * load) * dt;
                if (Stamina <= 0f)
                {
                    Stamina = 0f;
                    IsExhausted = true;
                    IsRunning = false;
                }
            }
            else
            {
                float regen = Mathf.Max(regenMin, regenBase - regenLossPerLoad * load);
                Stamina = Mathf.Min(maxStamina, Stamina + regen * dt);
            }
        }

        void ToggleCrouch()
        {
            if (IsCrouching && !HasHeadroomToStand()) return;
            IsCrouching = !IsCrouching;
        }

        bool HasHeadroomToStand()
        {
            // SphereCast mengabaikan collider yang sudah tumpang-tindih di titik awal, jadi collider sendiri aman.
            float radius = controller.radius * 0.95f;
            Vector3 origin = transform.position + Vector3.up * (controller.height - radius);
            float distance = standHeight - controller.height;
            return !Physics.SphereCast(origin, radius, Vector3.up, out _, distance, ~0, QueryTriggerInteraction.Ignore);
        }

        void UpdateCrouchTransition(float dt)
        {
            float targetHeight = IsCrouching ? crouchHeight : standHeight;
            float targetEye = IsCrouching ? crouchEyeHeight : standEyeHeight;
            float step = crouchTransitionSpeed * dt;

            ApplyHeight(Mathf.MoveTowards(controller.height, targetHeight, step));

            Vector3 eye = cameraRoot.localPosition;
            eye.y = Mathf.MoveTowards(eye.y, targetEye, step);
            cameraRoot.localPosition = eye;
        }

        void ApplyHeight(float height)
        {
            controller.height = height;
            controller.center = Vector3.up * (height * 0.5f);
        }
    }
}
