using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CardArranger : MonoBehaviour
{
    [Header("간격 설정 (단위: 미터)")]
    public float spacingX = 0.2f; // 카드의 가로 크기에 맞춰 조절하세요 (예: 0.2 = 20cm)
    public float spacingY = 0.0f;
    public float spacingZ = 0.0f;

    [ContextMenu("자식 오브젝트 일렬로 정렬 (Arrange Children)")]
    public void ArrangeChildren()
    {
        int childCount = transform.childCount;
        
        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            
            // 실행 취소(Ctrl+Z)를 위한 에디터 기록
#if UNITY_EDITOR
            Undo.RecordObject(child.transform, "Arrange Card Targets");
#endif
            
            // X축 기준으로 i번째 위치에 배치
            child.localPosition = new Vector3(i * spacingX, i * spacingY, i * spacingZ);
        }

        Debug.Log($"[{gameObject.name}] {childCount}개의 카드를 X축으로 {spacingX} 간격만큼 정렬했습니다!");
    }
}