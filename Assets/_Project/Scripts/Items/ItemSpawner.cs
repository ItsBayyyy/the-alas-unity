using System.Collections.Generic;
using System.Linq;
using TheAlas.World;
using UnityEngine;

namespace TheAlas.Items
{
    /// <summary>
    /// Mengacak keempat suku cadang ke empat dari enam landmark setiap ronde (GDD),
    /// supaya pemain harus membaca petunjuk, bukan menghafal.
    /// </summary>
    public class ItemSpawner : MonoBehaviour
    {
        public readonly struct Assignment
        {
            public readonly ItemData Part;
            public readonly Landmark Landmark;

            public Assignment(ItemData part, Landmark landmark)
            {
                Part = part;
                Landmark = landmark;
            }
        }

        [SerializeField] Car car;
        [SerializeField] float spawnHeight = 0.25f;

        readonly List<Assignment> assignments = new();

        public IReadOnlyList<Assignment> Assignments => assignments;

        void Awake()
        {
            var landmarks = FindObjectsByType<Landmark>(FindObjectsSortMode.None)
                .Where(landmark => landmark.ItemSpawnPoints.Length > 0)
                .OrderBy(_ => Random.value)
                .ToList();

            if (landmarks.Count < car.RequiredParts.Count)
                Debug.LogWarning($"[ItemSpawner] Hanya {landmarks.Count} landmark untuk {car.RequiredParts.Count} suku cadang; ada yang berbagi tempat.");

            for (int i = 0; i < car.RequiredParts.Count; i++)
            {
                var part = car.RequiredParts[i];
                var landmark = landmarks[i % landmarks.Count];
                var point = landmark.ItemSpawnPoints[Random.Range(0, landmark.ItemSpawnPoints.Length)];

                Instantiate(part.WorldPrefab, point.position + Vector3.up * spawnHeight,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                assignments.Add(new Assignment(part, landmark));
            }
        }
    }
}
