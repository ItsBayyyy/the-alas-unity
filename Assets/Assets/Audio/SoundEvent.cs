using UnityEngine;

public enum AudioCategory { Music, Ambience, SFX, UI }

[CreateAssetMenu(fileName = "SE_New", menuName = "The Alas/Audio/Sound Event")]
public class SoundEvent : ScriptableObject
{
    [Tooltip("Variasi klip. Diambil acak, tidak mengulang klip yang sama dua kali berturut-turut.")]
    public AudioClip[] clips;

    public AudioCategory category = AudioCategory.SFX;

    [Tooltip("Rentang volume acak (x = min, y = max)")]
    public Vector2 volume = new Vector2(0.9f, 1f);

    [Tooltip("Rentang pitch acak (x = min, y = max)")]
    public Vector2 pitch = new Vector2(0.95f, 1.05f);

    [Tooltip("0 = 2D (pemain, UI), 1 = 3D (makhluk, barang jatuh)")]
    [Range(0f, 1f)] public float spatialBlend = 1f;

    public float minDistance = 1f;
    public float maxDistance = 35f;
    public AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;

    [System.NonSerialized] int lastIndex = -1;

    public AudioClip PickClip()
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];

        int i;
        do { i = Random.Range(0, clips.Length); } while (i == lastIndex);
        lastIndex = i;
        return clips[i];
    }

    public float PickVolume() => Random.Range(volume.x, volume.y);
    public float PickPitch() => Random.Range(pitch.x, pitch.y);
}