using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TheAlas.Audio;
using TheAlas.Core;
using TheAlas.Items;
using TheAlas.Player;
using UnityEngine;

namespace TheAlas.World
{
    /// <summary>
    /// Tujuan pemain: setor barang, lalu nyalakan mesin setelah semua suku cadang lengkap.
    /// Mesin meraung beberapa detik dengan suara maksimal sebelum pemain benar-benar lolos.
    /// Mencoba starter sebelum lengkap hanya menghasilkan bunyi starter gagal yang berisik.
    /// </summary>
    public class Car : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemData[] requiredParts;
        [SerializeField] float engineDuration = 3f;
        [SerializeField] float depositNoiseBase = 8f;
        [SerializeField] float depositNoisePerItem = 4f;
        [SerializeField] float starterFailNoise = 30f;
        [Tooltip("Jeda antara mencoba starter, supaya bunyinya tidak bertumpuk")]
        [SerializeField] float starterCooldown = 1.5f;

        [Header("Audio")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip[] trunkClips;
        [SerializeField] AudioClip[] doorClips;
        [SerializeField] AudioClip[] engineClips;
        [SerializeField] AudioClip[] starterFailClips;
        [Tooltip("Jeda dari pintu ditutup sampai mesin hidup")]
        [SerializeField] float doorToEngineDelay = 0.6f;

        float nextStarterTime;

        readonly HashSet<ItemData> deposited = new();

        public IReadOnlyList<ItemData> RequiredParts => requiredParts;
        public bool HasAllParts => requiredParts.All(deposited.Contains);
        public int DepositedPartCount => deposited.Count;
        public int LootCount { get; private set; }
        public int LootValue { get; private set; }
        public float EngineTimeLeft { get; private set; }

        public bool IsDeposited(ItemData part) => deposited.Contains(part);

        public string GetPrompt(PlayerInteractor who)
        {
            if (RoundManager.Instance.State != RoundState.Playing) return null;

            int carried = who.Inventory.Items.Count;
            if (carried > 0) return $"[E] Setor {carried} barang ke mobil";
            if (HasAllParts) return "[E] Nyalakan mesin dan kabur";

            var missing = requiredParts.Where(part => !deposited.Contains(part)).Select(part => part.DisplayName);
            return $"Mobil masih butuh: {string.Join(", ", missing)}   [E] Coba starter";
        }

        public bool CanInteract(PlayerInteractor who) => RoundManager.Instance.State == RoundState.Playing;

        public void Interact(PlayerInteractor who)
        {
            if (who.Inventory.Items.Count > 0) Deposit(who);
            else if (HasAllParts) StartCoroutine(StartEngine());
            else TryStarter(who);
        }

        void TryStarter(PlayerInteractor who)
        {
            if (Time.time < nextStarterTime) return;
            nextStarterTime = Time.time + starterCooldown;
            AudioClips.PlayRandom(audioSource, starterFailClips, 1f, 0.04f);
            who.GetComponent<PlayerNoise>().AddSpike(starterFailNoise);
        }

        void Deposit(PlayerInteractor who)
        {
            var items = who.Inventory.TakeAll();
            foreach (var item in items)
            {
                if (item.Data.IsCarPart)
                {
                    deposited.Add(item.Data);
                }
                else
                {
                    LootCount++;
                    LootValue += item.Data.Value;
                }
                Destroy(item.gameObject);
            }

            who.GetComponent<PlayerNoise>().AddSpike(depositNoiseBase + depositNoisePerItem * items.Count);
            AudioClips.PlayRandom(audioSource, trunkClips);
        }

        IEnumerator StartEngine()
        {
            var round = RoundManager.Instance;
            round.BeginEscape();

            AudioClips.PlayRandom(audioSource, doorClips, 1f, 0f);
            yield return new WaitForSeconds(doorToEngineDelay);
            AudioClips.PlayRandom(audioSource, engineClips, 1f, 0f);

            EngineTimeLeft = engineDuration;
            while (EngineTimeLeft > 0f)
            {
                if (round.State != RoundState.Escaping) yield break;
                EngineTimeLeft -= Time.deltaTime;
                yield return null;
            }

            EngineTimeLeft = 0f;
            round.Win();
        }
    }
}
