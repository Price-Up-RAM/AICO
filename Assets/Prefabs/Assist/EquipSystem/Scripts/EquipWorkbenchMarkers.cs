using UnityEngine;

// 소켓/부착점 화면 마커 오버레이 — 저작 검증용. 선택 캐릭터의 소켓(●)과 placeholder(◆, refDist 표기)를 표시. M 키 토글.
// (전용 파일인 이유: 에디트 모드 씬 베이크는 파일명=클래스명인 MonoBehaviour만 AddComponent+직렬화 가능)
public class EquipWorkbenchMarkers : MonoBehaviour
{
    public bool showMarkers = true;         // 마커 표시 여부
    public KeyCode toggleKey = KeyCode.M;   // 토글 키

    private GUIStyle socketStyle;       // 소켓 라벨 (시안)
    private GUIStyle placeholderStyle;  // 부착점 라벨 (주황)
    private GUIStyle shadowStyle;       // 가독성용 그림자

    // 토글 입력 처리
    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showMarkers = !showMarkers;
        }
    }

    // GUIStyle은 OnGUI 안에서만 생성 가능 — 최초 1회 구성
    private void EnsureStyles()
    {
        if (socketStyle != null)
        {
            return;
        }

        socketStyle = new GUIStyle(GUI.skin.label);
        socketStyle.fontSize = 12;
        socketStyle.normal.textColor = new Color(0.35f, 0.9f, 1f);

        placeholderStyle = new GUIStyle(socketStyle);
        placeholderStyle.normal.textColor = new Color(1f, 0.75f, 0.3f);

        shadowStyle = new GUIStyle(socketStyle);
        shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
    }

    // 화면 마커 그리기
    private void OnGUI()
    {
        if (showMarkers == false)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        EnsureStyles();

        EquipSocket[] sockets = CollectSockets();
        foreach (EquipSocket socket in sockets)
        {
            if (socket == null)
            {
                continue;
            }

            DrawLabel(cam, socket.transform.position, "● " + socket.slotId, socketStyle);

            EquipPlaceholder[] placeholders = socket.GetComponentsInChildren<EquipPlaceholder>(true);
            foreach (EquipPlaceholder ph in placeholders)
            {
                if (ph == null)
                {
                    continue;
                }

                DrawLabel(cam, ph.transform.position, $"◆ {ph.placeholderId} r={ph.bakedRefDistLocal:F3}", placeholderStyle);
            }
        }
    }

    // 마커 대상 소켓 수집 — 선택 캐릭터 우선
    private EquipSocket[] CollectSockets()
    {
        // 데모 가드: 씬에 워크벤치 컨트롤러/선택이 없으면 안내 대신 씬 전체 소켓으로 폴백
        EquipWorkbenchController controller = EquipWorkbenchController.Instance;
        if (controller != null && controller.Selected != null)
        {
            return controller.Selected.GetComponentsInChildren<EquipSocket>(true);
        }

        return Object.FindObjectsOfType<EquipSocket>(true);
    }

    // 월드 위치 → 화면 라벨 (카메라 뒤는 스킵, GUI 좌표계는 y 반전)
    private void DrawLabel(Camera cam, Vector3 worldPos, string text, GUIStyle style)
    {
        Vector3 screen = cam.WorldToScreenPoint(worldPos);
        if (screen.z <= 0f)
        {
            return;
        }

        float x = screen.x;
        float y = Screen.height - screen.y;
        GUI.Label(new Rect(x + 1f, y - 9f, 320f, 22f), text, shadowStyle);
        GUI.Label(new Rect(x, y - 10f, 320f, 22f), text, style);
    }
}
