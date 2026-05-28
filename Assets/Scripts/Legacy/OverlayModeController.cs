using UnityEngine;

public class OverlayModeController : MonoBehaviour
{
    private bool isOverlayMode = false;
    private GameObject selectedObject;

    void Start()
    {
        // 초기 상태는 풀스크린 모드
        SetFullScreenMode();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // 클릭 위치에서 Raycast 수행
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // 오브젝트 클릭 시 오버레이 모드 전환
                if (!isOverlayMode)
                {
                    EnterOverlayMode(hit.collider.gameObject);
                }
                else
                {
                    ExitOverlayMode();
                }
            }
        }
    }

    void EnterOverlayMode(GameObject obj)
    {
        isOverlayMode = true;
        selectedObject = obj;

        // 선택한 오브젝트만 활성화, 나머지는 비활성화
        foreach (GameObject go in FindObjectsOfType<GameObject>())
        {
            if (go != obj) {
                go.SetActive(false);
            } else {
                Debug.Log("EnterOverlayMode obj : " + obj.ToString());
            }
        }

        // 오버레이 모드로 전환
        SetOverlayMode(obj);
    }

    void ExitOverlayMode()
    {
        isOverlayMode = false;

        // 모든 오브젝트 활성화
        foreach (GameObject go in FindObjectsOfType<GameObject>())
        {
            go.SetActive(true);
        }

        // 풀스크린 모드로 복귀
        SetFullScreenMode();
    }

    void SetOverlayMode(GameObject obj)
    {
        // 안드로이드 창 크기 조절 및 투명 모드 활성화
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow");
                AndroidJavaObject layoutParams = window.Call<AndroidJavaObject>("getAttributes");

                // 오브젝트 크기 계산
                RectTransform rect = obj.GetComponent<RectTransform>();
                int width = (int)(rect.rect.width * Screen.dpi / 160f);
                int height = (int)(rect.rect.height * Screen.dpi / 160f);

                Debug.Log("width :" + width);
                Debug.Log("height :" + height);

                // 창 크기 설정
                layoutParams.Set("width", width);
                layoutParams.Set("height", height);

                window.Call("setAttributes", layoutParams);
            }));
        }
    }

    void SetFullScreenMode()
    {
        // 전체화면 모드 복구
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow");
                AndroidJavaObject layoutParams = window.Call<AndroidJavaObject>("getAttributes");

                // 전체화면 크기 복원
                layoutParams.Set("width", -1); // MATCH_PARENT
                layoutParams.Set("height", -1); // MATCH_PARENT

                window.Call("setAttributes", layoutParams);
            }));
        }
    }
}
