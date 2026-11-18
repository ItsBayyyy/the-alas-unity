using UnityEngine;

// Tiga keadaan makhluk di GDD. petakan enum AI ke sini.
public enum CreatureAudioState { Wander, Investigate, Hunt }

public class MusicController : MonoBehaviour
{
    public static MusicController Instance { get; private set; }

    [Header("Klip loop (tempo dan panjang sama)")]
    public AudioClip droneExplore;
    public AudioClip layerInvestigate;
    public AudioClip layerChase;
    public AudioClip layerCurse; // opsional

    [Header("Volume target")]
    [Range(0f, 1f)] public float droneVolume = 0.25f;
    [Range(0f, 1f)] public float investigateVolume = 0.4f;
    [Range(0f, 1f)] public float chaseVolume = 0.8f;
    [Range(0f, 1f)] public float cursePerItem = 0.08f;
    [Range(0f, 1f)] public float curseMax = 0.5f;

    [Header("Lama fade (detik)")]
    public float fadeInTime = 0.5f;  // cepat: bahaya terasa seketika
    public float fadeOutTime = 4f;   // lambat: tegang tidak langsung hilang

    AudioSource drone, investigate, chase, curse;
    CreatureAudioState state = CreatureAudioState.Wander;
    int curseCount;
    bool running;

    void Awake()
    {
        Instance = this;
        drone = MakeLoop(droneExplore);
        investigate = MakeLoop(layerInvestigate);
        chase = MakeLoop(layerChase);
        curse = MakeLoop(layerCurse);
    }

    AudioSource MakeLoop(AudioClip clip)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        return s;
    }

    // Panggil saat ronde dimulai. Semua layer diputar di frame yang sama.
    public void StartMusic()
    {
        foreach (var s in new[] { drone, investigate, chase, curse })
            if (s.clip != null) s.Play();
        running = true;
    }

    public void StopMusic()
    {
        foreach (var s in new[] { drone, investigate, chase, curse }) s.Stop();
        running = false;
    }

    public void SetState(CreatureAudioState newState) => state = newState;

    // Panggil setiap barang antik dipungut. Kutukan permanen, jadi tidak pernah turun.
    public void AddCurse() => curseCount++;

    void Update()
    {
        if (!running) return;
        float cat = AudioManager.Instance != null ? AudioManager.Instance.CategoryVolume(AudioCategory.Music) : 1f;

        Fade(drone, droneVolume * cat);
        Fade(investigate, (state == CreatureAudioState.Investigate ? investigateVolume : 0f) * cat);
        Fade(chase, (state == CreatureAudioState.Hunt ? chaseVolume : 0f) * cat);
        Fade(curse, Mathf.Min(curseCount * cursePerItem, curseMax) * cat);
    }

    void Fade(AudioSource s, float target)
    {
        if (s.clip == null) return;
        float time = target > s.volume ? fadeInTime : fadeOutTime;
        s.volume = Mathf.MoveTowards(s.volume, target, Time.deltaTime / Mathf.Max(0.01f, time));
    }
}