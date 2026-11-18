using UnityEngine;

public enum ItemMaterial { Metal, Wood, Ceramic }

public class ItemAudio : MonoBehaviour
{
    public ItemMaterial material;
    public float weight = 1f; // sesuai bobot GDD

    [Header("Satu set per material, boleh dipakai bersama")]
    public SoundEvent pickup, drop, place, throwWhoosh;

    public float impactThreshold = 2f; // kecepatan minimal agar bunyi jatuh terdengar

    bool thrown;

    // Bobot 0,5-4 -> volume 0,6-1 dan pitch 1,1-0,85 (berat = lebih rendah)
    float VolScale => Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(0.5f, 4f, weight));
    float PitchScale => Mathf.Lerp(1.1f, 0.85f, Mathf.InverseLerp(0.5f, 4f, weight));

    public void PlayPickup() => Play(pickup);
    public void PlayDrop() => Play(drop);   // dijatuhkan sambil berdiri
    public void PlayPlace() => Play(place); // diletakkan sambil jongkok

    public void PlayThrow()
    {
        Play(throwWhoosh);
        thrown = true;
    }

    void OnCollisionEnter(Collision c)
    {
        if (!thrown || c.relativeVelocity.magnitude < impactThreshold) return;
        thrown = false;
        Play(drop); // titik jatuh = umpan suara
    }

    void Play(SoundEvent e)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAt(e, transform.position, VolScale, PitchScale);
    }
}