using TheAlas.Audio;
using TheAlas.Core;
using TheAlas.Player;
using UnityEngine;

namespace TheAlas.Items
{
    /// <summary>
    /// Barang di dunia. Saat dipungut objeknya dinonaktifkan dan disimpan di inventaris;
    /// saat dibuang atau dilempar objek yang sama diaktifkan lagi.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Item : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemData data;
        [Tooltip("Benturan di bawah kecepatan ini tidak dihitung sebagai jatuhnya lemparan")]
        [SerializeField] float minImpactSpeed = 1f;

        Rigidbody body;
        bool awaitingImpact;
        bool impactSoundPending;
        float impactVolume;

        public ItemData Data => data;

        void Awake() => body = GetComponent<Rigidbody>();

        public string GetPrompt(PlayerInteractor who) =>
            who.Inventory.IsFull
                ? "Tangan penuh"
                : $"[E] Ambil {data.DisplayName}  (beban +{Format.Number(data.Weight)})";

        public bool CanInteract(PlayerInteractor who) => !who.Inventory.IsFull;

        public void Interact(PlayerInteractor who) => who.Inventory.TryAdd(this);

        public void PickUp()
        {
            awaitingImpact = false;
            impactSoundPending = false;
            PlayAtPoint(data.PickupClips, 0.8f);
            gameObject.SetActive(false);
        }

        /// <param name="impactVolume">Kerasnya bunyi saat menyentuh tanah: diletakkan pelan, dijatuhkan, atau dilempar.</param>
        public void Release(Vector3 position, Quaternion rotation, Vector3 velocity, bool thrown, float impactVolume)
        {
            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);
            body.linearVelocity = velocity;
            body.angularVelocity = thrown ? Random.insideUnitSphere * 8f : Vector3.zero;
            awaitingImpact = thrown;
            impactSoundPending = true;
            this.impactVolume = impactVolume;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (impactSoundPending)
            {
                impactSoundPending = false;
                PlayAtPoint(data.ImpactClips, impactVolume);
            }

            if (!awaitingImpact || collision.relativeVelocity.magnitude < minImpactSpeed) return;

            // Bunyi jatuh terdengar di titik jatuh, bukan di posisi pemain: inilah yang membuat lemparan jadi umpan.
            awaitingImpact = false;
            NoiseEvents.Emit(transform.position, data.ThrowImpactNoise);
        }

        void PlayAtPoint(AudioClip[] clips, float volume)
        {
            var clip = AudioClips.Pick(clips);
            if (clip) AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }
    }
}
