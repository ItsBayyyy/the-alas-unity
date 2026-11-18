using System;
using System.Collections.Generic;
using System.Linq;
using TheAlas.Items;
using UnityEngine;

namespace TheAlas.Player
{
    /// <summary>
    /// Barang yang dibawa pemain. Total bobotnya menjadi beban di <see cref="PlayerMotor"/>.
    /// Barang di slot terpilih adalah barang yang dibuang atau dilempar, jadi pemain sendiri
    /// yang memilih apa yang dikorbankan.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerNoise), typeof(PlayerInputReader))]
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] Transform viewPoint;
        [SerializeField] float dropDistance = 0.8f;
        [Tooltip("Sudut lempar di atas arah pandang")]
        [SerializeField] float throwLift = 20f;
        [SerializeField] float throwNoise = 6f;

        readonly List<Item> items = new();
        PlayerMotor motor;
        PlayerNoise noise;
        PlayerInputReader input;

        public event Action Changed;

        public IReadOnlyList<Item> Items => items;
        public int SelectedIndex { get; private set; }
        public Item Selected => items.Count > 0 ? items[SelectedIndex] : null;
        public bool IsFull => items.Count >= PlayerInputReader.SlotCount;
        public float TotalWeight { get; private set; }

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            noise = GetComponent<PlayerNoise>();
            input = GetComponent<PlayerInputReader>();

            // Barang yang dilempar tidak boleh membentur badan pemain sendiri saat keluar dari tangan.
            int itemLayer = LayerMask.NameToLayer("Item");
            if (itemLayer >= 0) Physics.IgnoreLayerCollision(gameObject.layer, itemLayer);
        }

        void Update()
        {
            int slot = input.SlotPressed;
            if (slot >= 0 && slot < items.Count) Select(slot);
            if (input.ScrollStep != 0 && items.Count > 0)
                Select((SelectedIndex - input.ScrollStep + items.Count) % items.Count);

            if (input.DropPressed) DropSelected();
            else if (input.ThrowPressed) ThrowSelected();
        }

        public bool Contains(ItemData data) => items.Any(item => item.Data == data);

        public bool TryAdd(Item item)
        {
            if (IsFull) return false;

            items.Add(item);
            item.PickUp();
            noise.AddSpike(item.Data.PickupNoise);
            SelectedIndex = items.Count - 1;
            Refresh();
            return true;
        }

        /// <summary>Mengosongkan inventaris, dipakai saat menyetor ke mobil.</summary>
        public List<Item> TakeAll()
        {
            var taken = new List<Item>(items);
            items.Clear();
            SelectedIndex = 0;
            Refresh();
            return taken;
        }

        void Select(int index)
        {
            SelectedIndex = index;
            Changed?.Invoke();
        }

        void DropSelected()
        {
            var item = RemoveSelected();
            if (!item) return;

            // Jongkok berarti meletakkan pelan; berdiri berarti menjatuhkan.
            float height = motor.IsCrouching ? 0.15f : 0.9f;
            Vector3 position = transform.position + transform.forward * dropDistance + Vector3.up * height;
            item.Release(position, transform.rotation, Vector3.zero, thrown: false, impactVolume: motor.IsCrouching ? 0.25f : 0.7f);
            noise.AddSpike(motor.IsCrouching ? item.Data.PlaceNoise : item.Data.DropNoise);
        }

        void ThrowSelected()
        {
            var item = RemoveSelected();
            if (!item) return;

            Vector3 direction = Quaternion.AngleAxis(-throwLift, viewPoint.right) * viewPoint.forward;
            // Kecepatan awal dipilih supaya di tanah datar barang jatuh sejauh ThrowDistance.
            float launchAngle = Mathf.Clamp(Vector3.Angle(Vector3.ProjectOnPlane(direction, Vector3.up), direction), 15f, 60f);
            float speed = Mathf.Sqrt(item.Data.ThrowDistance * -Physics.gravity.y / Mathf.Sin(2f * launchAngle * Mathf.Deg2Rad));

            Vector3 position = viewPoint.position + viewPoint.forward * 0.5f;
            item.Release(position, Quaternion.LookRotation(direction), direction * speed, thrown: true, impactVolume: 1f);
            noise.AddSpike(throwNoise);
        }

        Item RemoveSelected()
        {
            if (items.Count == 0) return null;

            var item = items[SelectedIndex];
            items.RemoveAt(SelectedIndex);
            SelectedIndex = Mathf.Clamp(SelectedIndex, 0, Mathf.Max(0, items.Count - 1));
            Refresh();
            return item;
        }

        void Refresh()
        {
            TotalWeight = items.Sum(item => item.Data.Weight);
            motor.Load = TotalWeight;
            Changed?.Invoke();
        }
    }
}
