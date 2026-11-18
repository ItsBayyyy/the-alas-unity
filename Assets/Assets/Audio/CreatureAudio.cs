using UnityEngine;

public class CreatureAudio : MonoBehaviour
{
    [Header("SoundEvent")]
    public SoundEvent footstep;    // 3D
    public SoundEvent growl;       // 3D, saat masuk Memburu
    public SoundEvent listen;      // 3D, saat masuk Menyelidiki
    public SoundEvent calm;        // 3D, saat kembali Berkeliaran
    public SoundEvent huntSting;   // 2D
    public SoundEvent caughtSting; // 2D

    [Header("Napas loop (AudioSource 3D di kepala makhluk)")]
    public AudioSource breathLoop;
    public float breathVolWander = 0.4f, breathVolInvestigate = 0.6f, breathVolHunt = 1f;

    [Header("Langkah (kalau tidak pakai Animation Event)")]
    public bool useTimerSteps = true;
    public float intervalWander = 0.9f, intervalInvestigate = 0.6f, intervalHunt = 0.38f;
    [Range(0f, 0.5f)] public float jitter = 0.2f; // tidak teratur = kesan patah-patah

    [Header("Suara mulut (AudioSource 3D di kepala makhluk)")]
    public AudioSource voiceSource;

    public HeartbeatController heartbeat;

    CreatureAudioState state = CreatureAudioState.Wander;
    float stepTimer;
    Vector3 lastPos;

    void Start() => lastPos = transform.position;

    // Dipanggil AI setiap kali keadaan berubah.
    public void SetState(CreatureAudioState newState)
    {
        if (newState == state) return;
        var prev = state;
        state = newState;
        var am = AudioManager.Instance;

        if (am != null)
        {
            if (newState == CreatureAudioState.Hunt)
            {
                am.PlayAt(growl, transform.position);
                am.Play2D(huntSting);
            }
            else if (newState == CreatureAudioState.Investigate && prev == CreatureAudioState.Wander)
                am.PlayAt(listen, transform.position);
            else if (newState == CreatureAudioState.Wander)
                am.PlayAt(calm, transform.position);
        }

        if (MusicController.Instance != null) MusicController.Instance.SetState(newState);
        if (heartbeat != null) heartbeat.SetHunting(newState == CreatureAudioState.Hunt);
    }

    // Dipanggil saat pemain tertangkap.
    public void PlayCaught()
    {
        if (MusicController.Instance != null) MusicController.Instance.StopMusic();
        if (AudioManager.Instance != null) AudioManager.Instance.Play2D(caughtSting);
    }

    public void PlayVoice(SoundEvent e)
    {
        if (e == null || voiceSource == null) return;
        var clip = e.PickClip();
        if (clip == null) return;
        voiceSource.pitch = e.PickPitch();
        float cat = AudioManager.Instance != null ? AudioManager.Instance.CategoryVolume(e.category) : 1f;
        voiceSource.PlayOneShot(clip, e.PickVolume() * cat);
    }

    public void AnimStep()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(footstep, transform.position);
    }

    void Update()
    {
        if (breathLoop != null)
        {
            float target = state == CreatureAudioState.Hunt ? breathVolHunt
                         : state == CreatureAudioState.Investigate ? breathVolInvestigate
                         : breathVolWander;
            float cat = AudioManager.Instance != null ? AudioManager.Instance.CategoryVolume(AudioCategory.SFX) : 1f;
            breathLoop.volume = Mathf.MoveTowards(breathLoop.volume, target * cat, Time.deltaTime);
        }

        if (!useTimerSteps) return;

        float speed = (transform.position - lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPos = transform.position;
        if (speed < 0.1f) return;

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            AnimStep();
            float interval = state == CreatureAudioState.Hunt ? intervalHunt
                           : state == CreatureAudioState.Investigate ? intervalInvestigate
                           : intervalWander;
            stepTimer = interval * (1f + Random.Range(-jitter, jitter));
        }
    }
}