using System.Collections;
using UnityEngine;

// 파티클과 오라 이펙트 관리
public class EffectManager : MonoBehaviour
{
    private static EffectManager instance; // 현재 이펙트 관리자

    public static EffectManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<EffectManager>();
            }

            return instance;
        }
    }

    public GameObject fxPrefabLoveAura; // 호감 이펙트 프리팹
    [SerializeField] private ParticleSystem fxChange; // 캐릭터 변경 이펙트
    [SerializeField] private ParticleSystem fxClick; // 클릭 지점 이펙트
    [SerializeField] private float clickMarkerSize = 30f; // 클릭 마커 한 변 길이(px)
    [SerializeField] private Color clickMarkerColor = new Color(1f, 0f, 0f, 0.7f); // 클릭 마커 색

    // 지정한 월드 위치에서 캐릭터 변경 이펙트 재생
    public void PlayChangeEffectAt(Vector3 worldPosition)
    {
        if (fxChange == null)
        {
            return;
        }

        fxChange.transform.position = worldPosition;
        fxChange.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
        fxChange.Play(true);
    }

    // Windows 화면 좌표에 클릭 지점을 표시한다 (마커 + 파티클).
    // 클릭을 수행하는 쪽에서 호출한다 — 표시가 없으면 클릭이 실행됐는지 눈으로 판단할 수 없다.
    public void ShowClickMarker(int winX, int winY, float duration = 2f)
    {
#if UNITY_STANDALONE_WIN
        Debug.Log($"[EffectManager] ShowClickMarker: ({winX}, {winY})");
        StartCoroutine(ShowClickMarkerCoroutine(winX, winY, duration));
#endif
    }

    // 클릭 마커를 Canvas에 띄우고 duration 뒤에 지운다
    private IEnumerator ShowClickMarkerCoroutine(int winX, int winY, float duration)
    {
#if !UNITY_STANDALONE_WIN
        yield break;
#else
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[EffectManager] Canvas를 찾을 수 없어 클릭 마커를 생략합니다.");
            yield break;
        }

        GameObject marker = new GameObject("ClickMarker");
        marker.transform.SetParent(canvas.transform, false);

        var image = marker.AddComponent<UnityEngine.UI.Image>();
        image.color = clickMarkerColor;
        image.raycastTarget = false;  // 마커가 클릭을 가로채면 안 된다

        RectTransform rt = marker.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(clickMarkerSize, clickMarkerSize);

        // Windows 좌표는 위에서 아래로 증가하므로 Unity 화면 좌표로 뒤집는다
        Vector2 unityScreenPoint = new Vector2(winX, Screen.height - winY);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            unityScreenPoint,
            canvas.worldCamera,
            out Vector2 localPoint
        );

        rt.anchoredPosition = localPoint;
        Debug.Log($"[EffectManager] 클릭 마커: Windows({winX}, {winY}) -> Unity({unityScreenPoint.x}, {unityScreenPoint.y}) -> Local({localPoint.x}, {localPoint.y})");

        if (fxClick != null)
        {
            Vector3 worldPos = canvas.transform.TransformPoint(new Vector3(localPoint.x, localPoint.y, 0f));
            fxClick.transform.position = worldPos;
            fxClick.Play();
        }

        yield return new WaitForSeconds(duration);

        if (marker != null)
        {
            Destroy(marker);
        }
#endif
    }

    // 대상에 호감 이펙트 부착
    public GameObject CreateEffectToGameObject(GameObject target, string fxName = "love")
    {
        GameObject fxPrefab = fxPrefabLoveAura;
        if (fxName == "love")
        {
            fxPrefab = fxPrefabLoveAura;
        }

        GameObject fxInstance = Instantiate(fxPrefab, target.transform);
        return fxInstance;
    }
}
