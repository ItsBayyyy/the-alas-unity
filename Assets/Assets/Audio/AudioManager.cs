using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Volume per kategori (hubungkan ke slider pengaturan)")]
    [Range(0f, 1f)] public float master = 1f;
    [Range(0f, 1f)] public float music = 0.7f;
    [Range(0f, 1f)] public float ambience = 0.8f;
    [Range(0f, 1f)] public float sfx = 1f;
    [Range(0f, 1f)] public float ui = 0.8f;

    [SerializeField] int poolSize = 24;

    readonly List<AudioSource> pool = new List<AudioSource>();
    int nextIndex;
    AudioSource game2D;
    AudioSource ui2D;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        game2D = gameObject.AddComponent<AudioSource>();
        game2D.playOnAwake = false;
        game2D.spatialBlend = 0f;

        ui2D = gameObject.AddComponent<AudioSource>();
        ui2D.playOnAwake = false;
        ui2D.spatialBlend = 0f;
        ui2D.ignoreListenerPause = true; // tetap bunyi saat AudioListener.pause = true

        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject("PooledAudio_" + i);
            go.transform.SetParent(transform);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.dopplerLevel = 0f;
            pool.Add(s);
        }
    }

    public float CategoryVolume(AudioCategory c)
    {
        switch (c)
        {
            case AudioCategory.Music: return master * music;
            case AudioCategory.Ambience: return master * ambience;
            case AudioCategory.UI: return master * ui;
            default: return master * sfx;
        }
    }

    // Bunyi 2D gameplay (detak jantung, stinger).
    public void Play2D(SoundEvent e, float volumeScale = 1f)
    {
        if (e == null) return;
        var clip = e.PickClip();
        if (clip == null) return;
        game2D.PlayOneShot(clip, e.PickVolume() * volumeScale * CategoryVolume(e.category));
    }

    // Bunyi UI (tetap terdengar di menu jeda).
    public void PlayUI(SoundEvent e)
    {
        if (e == null) return;
        var clip = e.PickClip();
        if (clip == null) return;
        ui2D.PlayOneShot(clip, e.PickVolume() * CategoryVolume(AudioCategory.UI));
    }

    // Bunyi di posisi dunia. 2D atau 3D mengikuti spatialBlend di SoundEvent.
    public AudioSource PlayAt(SoundEvent e, Vector3 position, float volumeScale = 1f, float pitchScale = 1f)
    {
        if (e == null) return null;
        var clip = e.PickClip();
        if (clip == null) return null;

        var s = GetSource();
        s.transform.position = position;
        s.clip = clip;
        s.loop = false;
        s.spatialBlend = e.spatialBlend;
        s.minDistance = e.minDistance;
        s.maxDistance = e.maxDistance;
        s.rolloffMode = e.rolloff;
        s.pitch = e.PickPitch() * pitchScale;
        s.volume = e.PickVolume() * volumeScale * CategoryVolume(e.category);
        s.Play();
        return s;
    }

    AudioSource GetSource()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            int idx = (nextIndex + i) % pool.Count;
            if (!pool[idx].isPlaying)
            {
                nextIndex = (idx + 1) % pool.Count;
                return pool[idx];
            }
        }
        // Semua sibuk: pakai ulang bergiliran.
        var s = pool[nextIndex];
        nextIndex = (nextIndex + 1) % pool.Count;
        s.Stop();
        return s;
    }

    // Panggil dari menu jeda.
    public static void SetPaused(bool paused) => AudioListener.pause = paused;
}