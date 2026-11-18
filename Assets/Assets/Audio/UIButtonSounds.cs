using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonSounds : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public SoundEvent hover;
    public SoundEvent click;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUI(hover);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUI(click);
    }
}