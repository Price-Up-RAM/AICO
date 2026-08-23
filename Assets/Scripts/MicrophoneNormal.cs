using UnityEngine;
using UnityEngine.EventSystems;

// UI 버튼에서 MicrophoneManager를 호출하는 얇은 래퍼
public class MicrophoneNormal : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        StartRecording();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopRecording();
    }

    // UI 버튼용 (마우스로 시작)
    public void StartRecording()
    {
        if (MicrophoneManager.Instance != null)
        {
            MicrophoneManager.Instance.StartRecording();
        }
        else
        {
            Debug.LogError("MicrophoneManager instance not found!");
        }
    }

    public void StopRecording()
    {
        if (MicrophoneManager.Instance != null)
        {
            MicrophoneManager.Instance.StopRecording();
        }
    }
}

