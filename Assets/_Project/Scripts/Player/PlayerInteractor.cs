using TheAlas.Core;
using UnityEngine;

namespace TheAlas.Player
{
    /// <summary>
    /// Mencari benda yang bisa dipakai di depan crosshair dan menjalankannya dengan E.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerInputReader))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] Transform viewPoint;
        [SerializeField] float reach = 2.6f;
        [Tooltip("Jari-jari pencarian, supaya barang kecil di tanah tetap mudah dibidik")]
        [SerializeField] float probeRadius = 0.2f;
        [SerializeField] LayerMask probeMask = ~0;

        readonly RaycastHit[] hits = new RaycastHit[16];
        PlayerInputReader input;

        public PlayerInventory Inventory { get; private set; }
        public IInteractable Current { get; private set; }
        public string CurrentPrompt => Current?.GetPrompt(this);

        void Awake()
        {
            Inventory = GetComponent<PlayerInventory>();
            input = GetComponent<PlayerInputReader>();
        }

        void OnDisable() => Current = null;

        void Update()
        {
            Current = FindTarget();
            if (input.InteractPressed && Current != null && Current.CanInteract(this))
                Current.Interact(this);
        }

        IInteractable FindTarget()
        {
            // Trigger ikut dicek karena barang punya area pungut berupa trigger.
            int count = Physics.SphereCastNonAlloc(viewPoint.position, probeRadius, viewPoint.forward, hits, reach,
                probeMask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestDistance = float.MaxValue;
            float blockDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.transform.IsChildOf(transform)) continue;

                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    if (hit.distance < bestDistance)
                    {
                        best = interactable;
                        bestDistance = hit.distance;
                    }
                }
                else if (!hit.collider.isTrigger)
                {
                    blockDistance = Mathf.Min(blockDistance, hit.distance);
                }
            }

            // Tembok atau pohon di antara pemain dan barang menghalangi.
            return bestDistance <= blockDistance ? best : null;
        }
    }
}
