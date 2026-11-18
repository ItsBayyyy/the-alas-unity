using TheAlas.Player;
using UnityEngine;

namespace TheAlas.World
{
    /// <summary>
    /// Ranting kering di tanah: patah sekali saat diinjak pemain, menambah lonjakan suara
    /// (lebih pelan kalau jongkok, lebih keras kalau beban berat), lalu berganti ke model patah.
    /// Klip suara diisi dari rekaman tim; tanpa klip, efek gameplay-nya tetap jalan.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Twig : MonoBehaviour
    {
        [SerializeField] GameObject whole;
        [SerializeField] GameObject broken;

        [Header("Suara (0–100)")]
        [SerializeField] float snapNoise = 30f;
        [SerializeField] float crouchSnapNoise = 12f;
        [SerializeField] float noisePerLoad = 2f;

        [Header("Audio")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip[] snapClips;

        bool snapped;

        void OnTriggerEnter(Collider other)
        {
            if (snapped) return;
            var noise = other.GetComponentInParent<PlayerNoise>();
            if (!noise) return;

            snapped = true;
            var motor = noise.GetComponent<PlayerMotor>();
            float loudness = (motor.IsCrouching ? crouchSnapNoise : snapNoise) + noisePerLoad * motor.Load;
            noise.AddSpike(loudness);

            whole.SetActive(false);
            broken.SetActive(true);
            PlaySnap(loudness);
        }

        void PlaySnap(float loudness)
        {
            if (!audioSource || snapClips == null || snapClips.Length == 0) return;
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(snapClips[Random.Range(0, snapClips.Length)], Mathf.Clamp01(loudness / 40f));
        }
    }
}
