using UnityEngine;

public static class NoiseMath
{
    // GDD: suara total = suara terbesar + 30% suara yang lebih kecil.
    public static float Combine(float gameNoise, float micNoise)
    {
        float big = Mathf.Max(gameNoise, micNoise);
        float small = Mathf.Min(gameNoise, micNoise);
        return Mathf.Clamp(big + 0.3f * small, 0f, 100f);
    }

    // GDD: jarak dengar = 0,32 m x suara total + 0,8 m.
    public static float HearingRadius(float totalNoise) => 0.32f * totalNoise + 0.8f;
}