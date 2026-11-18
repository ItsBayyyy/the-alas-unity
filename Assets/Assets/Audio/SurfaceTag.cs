using UnityEngine;

public enum Surface { Leaves, Dirt, Gravel, Bush }
public enum MoveMode { Crouch, Walk, Run }

// Pasang di collider tanah, atau di trigger semak (Surface = Bush, Is Trigger aktif).
public class SurfaceTag : MonoBehaviour
{
    public Surface surface = Surface.Dirt;
}