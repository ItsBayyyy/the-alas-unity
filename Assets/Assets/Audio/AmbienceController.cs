using System;
using UnityEngine;

[Serializable]
public class RandomAmbient
{
    public SoundEvent sound;
    public float minInterval = 8f;
    public float maxInterval = 20f;
    public float minRadius = 15f;
    public float maxRadius = 40f;
    [Tooltip("Opsional: kalau diisi, bunyi selalu dari titik ini (misal kentongan di Pos Ronda)")]
    public Transform fixedPoint;
    [NonSerialized] public float timer;
}

public class AmbienceController : MonoBehaviour
{
    public AudioClip bedLoop;
    [Range(0f, 1f)] public float bedVolume = 0.35f;
    public Transform listener; // kamera atau pemain
    public RandomAmbient[] randoms;

    AudioSource bed;

    void Start()
    {
        bed = gameObject.AddComponent<AudioSource>();
        bed.clip = bedLoop;
        bed.loop = true;
        bed.spatialBlend = 0f;
        bed.playOnAwake = false;
        if (bedLoop != null) bed.Play();

        foreach (var r in randoms) r.timer = UnityEngine.Random.Range(r.minInterval, r.maxInterval);
    }

    void Update()
    {
        var am = AudioManager.Instance;
        if (am == null || listener == null) return;

        bed.volume = bedVolume * am.CategoryVolume(AudioCategory.Ambience);

        foreach (var r in randoms)
        {
            r.timer -= Time.deltaTime;
            if (r.timer > 0f) continue;

            Vector3 pos;
            if (r.fixedPoint != null) pos = r.fixedPoint.position;
            else
            {
                Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
                float dist = UnityEngine.Random.Range(r.minRadius, r.maxRadius);
                pos = listener.position + new Vector3(dir.x, 0f, dir.y) * dist;
            }

            am.PlayAt(r.sound, pos);
            r.timer = UnityEngine.Random.Range(r.minInterval, r.maxInterval);
        }
    }
}