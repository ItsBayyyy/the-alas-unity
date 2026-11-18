using UnityEngine;

namespace TheAlas.Audio
{
    public static class AudioClips
    {
        /// <summary>Satu klip acak dari daftar; null kalau daftar kosong (rekaman belum ada).</summary>
        public static AudioClip Pick(AudioClip[] clips) =>
            clips == null || clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];

        /// <summary>Memutar satu klip acak dengan sedikit variasi nada supaya pengulangan tidak terdengar sama.</summary>
        public static void PlayRandom(AudioSource source, AudioClip[] clips, float volume = 1f, float pitchJitter = 0.08f)
        {
            var clip = Pick(clips);
            if (!source || !clip) return;
            source.pitch = Random.Range(1f - pitchJitter, 1f + pitchJitter);
            source.PlayOneShot(clip, volume);
        }
    }
}
