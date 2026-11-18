using System;
using UnityEngine;

[Serializable]
public class SurfaceSounds
{
    public Surface surface;
    public SoundEvent crouch;
    public SoundEvent walk;
    public SoundEvent run;
    [Tooltip("Dipakai kalau beban >= heavyLoadThreshold")]
    public SoundEvent walkHeavy;
    public SoundEvent runHeavy;
}

public class PlayerFootsteps : MonoBehaviour
{
    [Header("Diisi controller Bayu setiap frame")]
    public MoveMode mode = MoveMode.Walk;
    public float load;
    public bool grounded = true;

    [Header("Bunyi")]
    public SurfaceSounds[] surfaces;
    public SoundEvent bushRustle;
    public float heavyLoadThreshold = 3f;

    [Header("Jarak per langkah (meter)")]
    public float strideCrouch = 0.55f;
    public float strideWalk = 0.8f;
    public float strideRun = 1.3f;

    [Header("Deteksi permukaan")]
    public LayerMask groundMask = ~0;
    public Surface fallbackSurface = Surface.Dirt;
    [Tooltip("Kalau peta memakai Terrain: indeks terrain layer ke jenis permukaan")]
    public Surface[] terrainLayerToSurface;

    // Posisi dan nilai suara langkah (0-100). Bayu bisa subscribe untuk noise bar.
    public static event Action<Vector3, float> OnStepNoise;

    Vector3 lastPos;
    float distanceAccum;
    int bushCount;

    void Start() => lastPos = transform.position;

    void Update()
    {
        Vector3 delta = transform.position - lastPos;
        delta.y = 0f;
        lastPos = transform.position;
        if (!grounded) return;

        distanceAccum += delta.magnitude;
        float stride = mode == MoveMode.Crouch ? strideCrouch
                     : mode == MoveMode.Run ? strideRun
                     : strideWalk;

        if (distanceAccum >= stride)
        {
            distanceAccum = 0f;
            Step();
        }
    }

    public void Step()
    {
        Surface s = DetectGroundSurface();
        var am = AudioManager.Instance;
        if (am != null)
        {
            am.PlayAt(PickEvent(s), transform.position);
            if (bushCount > 0) am.PlayAt(bushRustle, transform.position);
        }
        OnStepNoise?.Invoke(transform.position, ComputeStepNoise(s));
    }

    // Rumus GDD: jongkok 5, jalan 20, lari 55; + 3 x beban (jongkok 2 x beban);
    // semak +14; kerikil +10. Kalau Bayu sudah menghitung ini, pakai punya Bayu saja.
    public float ComputeStepNoise(Surface ground)
    {
        float baseNoise = mode == MoveMode.Crouch ? 5f : mode == MoveMode.Run ? 55f : 20f;
        float perLoad = mode == MoveMode.Crouch ? 2f : 3f;
        float n = baseNoise + perLoad * load;
        if (bushCount > 0) n += 14f;
        if (ground == Surface.Gravel) n += 10f;
        return Mathf.Clamp(n, 0f, 100f);
    }

    SoundEvent PickEvent(Surface s)
    {
        foreach (var ss in surfaces)
        {
            if (ss.surface != s) continue;
            bool heavy = load >= heavyLoadThreshold;
            switch (mode)
            {
                case MoveMode.Crouch: return ss.crouch;
                case MoveMode.Run: return heavy && ss.runHeavy != null ? ss.runHeavy : ss.run;
                default: return heavy && ss.walkHeavy != null ? ss.walkHeavy : ss.walk;
            }
        }
        return null;
    }

    Surface DetectGroundSurface()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2f, groundMask, QueryTriggerInteraction.Ignore))
            return fallbackSurface;

        var tag = hit.collider.GetComponentInParent<SurfaceTag>();
        if (tag != null) return tag.surface;

        var terrain = hit.collider.GetComponent<Terrain>();
        if (terrain != null && terrainLayerToSurface != null && terrainLayerToSurface.Length > 0)
        {
            int layer = DominantTerrainLayer(terrain, hit.point);
            if (layer < terrainLayerToSurface.Length) return terrainLayerToSurface[layer];
        }
        return fallbackSurface;
    }

    static int DominantTerrainLayer(Terrain t, Vector3 worldPos)
    {
        TerrainData td = t.terrainData;
        Vector3 p = worldPos - t.transform.position;
        int x = Mathf.Clamp((int)(p.x / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
        int z = Mathf.Clamp((int)(p.z / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
        float[,,] a = td.GetAlphamaps(x, z, 1, 1);

        int best = 0; float bestVal = 0f;
        for (int i = 0; i < a.GetLength(2); i++)
            if (a[0, 0, i] > bestVal) { bestVal = a[0, 0, i]; best = i; }
        return best;
    }

    void OnTriggerEnter(Collider other)
    {
        var tag = other.GetComponentInParent<SurfaceTag>();
        if (tag != null && tag.surface == Surface.Bush) bushCount++;
    }

    void OnTriggerExit(Collider other)
    {
        var tag = other.GetComponentInParent<SurfaceTag>();
        if (tag != null && tag.surface == Surface.Bush) bushCount = Mathf.Max(0, bushCount - 1);
    }
}