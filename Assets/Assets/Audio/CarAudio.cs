using UnityEngine;

public class CarAudio : MonoBehaviour
{
    public SoundEvent trunkOpen, trunkClose, deposit, starterFail, engineRoar, lightsOn, driveAway;

    void Play(SoundEvent e)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(e, transform.position);
    }

    public void OpenTrunk() => Play(trunkOpen);
    public void CloseTrunk() => Play(trunkClose);
    public void Deposit() => Play(deposit);
    public void StarterFail() => Play(starterFail);
    public void LightsOn() => Play(lightsOn);
    public void EngineRoar() => Play(engineRoar); // klip tepat 3 detik; noise gameplay diurus Bayu

    public void DriveAway()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Play2D(driveAway);
    }
}