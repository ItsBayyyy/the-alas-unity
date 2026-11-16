using System;
using TheAlas.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheAlas.Core
{
    public enum RoundState
    {
        Playing,
        /// <summary>Mesin dinyalakan; pemain harus bertahan beberapa detik.</summary>
        Escaping,
        Won,
        Caught,
        TimeUp
    }

    /// <summary>
    /// Timer ronde 7:30, kondisi menang dan kalah, serta catatan beban rata-rata
    /// untuk layar akhir.
    /// </summary>
    public class RoundManager : MonoBehaviour
    {
        [SerializeField] float duration = 450f;
        [SerializeField] float finalMinute = 60f;
        [Tooltip("Gerak di bawah kecepatan ini tidak dihitung untuk beban rata-rata")]
        [SerializeField] float movingThreshold = 0.3f;

        [Header("Pemain")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerLook look;
        [SerializeField] PlayerInventory inventory;
        [SerializeField] PlayerInteractor interactor;

        float loadSeconds;
        float movingSeconds;

        public static RoundManager Instance { get; private set; }

        public event Action<RoundState> StateChanged;

        public RoundState State { get; private set; } = RoundState.Playing;
        public float TimeLeft { get; private set; }
        public float Duration => duration;
        /// <summary>0 di awal ronde, 1 saat waktu habis.</summary>
        public float Progress => 1f - TimeLeft / duration;
        public bool IsFinalMinute => TimeLeft <= finalMinute;
        public bool IsOver => State is RoundState.Won or RoundState.Caught or RoundState.TimeUp;
        /// <summary>Beban rata-rata selama bergerak (GDD: ditampilkan di layar akhir, dipakai skor).</summary>
        public float AverageLoad => movingSeconds > 0f ? loadSeconds / movingSeconds : 0f;
        public float LoadAtEnd { get; private set; }

        void Awake()
        {
            Instance = this;
            TimeLeft = duration;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (IsOver)
            {
                if (input.RestartPressed) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                End(RoundState.TimeUp);
                return;
            }

            if (motor.HorizontalSpeed > movingThreshold)
            {
                loadSeconds += motor.Load * Time.deltaTime;
                movingSeconds += Time.deltaTime;
            }
        }

        public void BeginEscape()
        {
            if (State != RoundState.Playing) return;
            SetPlayerControl(false);
            ChangeState(RoundState.Escaping);
        }

        public void Win()
        {
            if (State == RoundState.Escaping) End(RoundState.Won);
        }

        public void Caught() => End(RoundState.Caught);

        void End(RoundState result)
        {
            if (IsOver) return;
            LoadAtEnd = inventory.TotalWeight;
            SetPlayerControl(false);
            look.enabled = false;
            ChangeState(result);
        }

        void SetPlayerControl(bool enabled)
        {
            motor.enabled = enabled;
            inventory.enabled = enabled;
            interactor.enabled = enabled;
        }

        void ChangeState(RoundState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
