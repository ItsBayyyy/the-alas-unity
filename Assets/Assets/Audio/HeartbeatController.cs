using UnityEngine;

public class HeartbeatController : MonoBehaviour
{
    public SoundEvent heartbeat; // satu klip lub-dub per detak, spatialBlend 0
    public Transform player;
    public Transform creature;

    public float startDistance = 25f; // detak mulai terdengar di bawah jarak ini
    public float closeDistance = 3f;  // jarak detak paling cepat
    public float minBpm = 70f;
    public float maxBpm = 150f;
    public float huntBoost = 0.25f;   // tambahan intensitas saat Memburu

    bool hunting;
    float timer;

    public void SetHunting(bool value) => hunting = value;

    void Update()
    {
        if (player == null || creature == null || AudioManager.Instance == null) return;

        float d = Vector3.Distance(player.position, creature.position);
        if (d > startDistance && !hunting) { timer = 0f; return; }

        float t = Mathf.InverseLerp(startDistance, closeDistance, d); // 0 jauh, 1 dekat
        if (hunting) t = Mathf.Clamp01(t + huntBoost);

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            AudioManager.Instance.Play2D(heartbeat, Mathf.Lerp(0.4f, 1f, t));
            timer = 60f / Mathf.Lerp(minBpm, maxBpm, t);
        }
    }
}