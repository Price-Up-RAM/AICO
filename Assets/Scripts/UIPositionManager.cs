using UnityEngine;

public class UIPositionManager : MonoBehaviour
{
    private static UIPositionManager instance;

    public static UIPositionManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<UIPositionManager>();
            }
            return instance;
        }
    }

    private Canvas canvas;
    private RectTransform canvasRect;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            canvas = CanvasManager.Instance.canvasUI;
            canvasRect = canvas.GetComponent<RectTransform>();
        }
    }

    // 캔버스 중앙 위치 반환
    public Vector3 GetCanvasPositionCenter()
    {
        return canvas.transform.TransformPoint(Vector3.zero);
    }

    // 캔버스 왼쪽 중앙 위치 (X - Width/2)
    public Vector3 GetCanvasPositionLeft()
    {
        return canvas.transform.TransformPoint(new Vector3(-canvasRect.rect.width / 2 + 200f, 0f, 0f));
    }

    // 캔버스 오른쪽 중앙 위치 (X + Width/2)
    public Vector3 GetCanvasPositionRight()
    {
        return canvas.transform.TransformPoint(new Vector3(canvasRect.rect.width / 2 - 200f, 0f, 0f));
    }

    // 캔버스 상단 중앙 위치
    public Vector3 GetCanvasPositionTop()
    {
        return canvas.transform.TransformPoint(new Vector3(0f, canvasRect.rect.height / 2 - 100f, 0f));
    }

    // 캔버스 하단 중앙 위치
    public Vector3 GetCanvasPositionBottom()
    {
        return canvas.transform.TransformPoint(new Vector3(0f, -canvasRect.rect.height / 2 + 100f, 0f));
    }

    // 캔버스 좌상단
    public Vector3 GetCanvasPositionTopLeft()
    {
        return canvas.transform.TransformPoint(new Vector3(-canvasRect.rect.width / 2 + 100f, canvasRect.rect.height / 2 - 100f, 0f));
    }

    // 캔버스 우상단
    public Vector3 GetCanvasPositionTopRight()
    {
        return canvas.transform.TransformPoint(new Vector3(canvasRect.rect.width / 2 - 100f, canvasRect.rect.height / 2 - 100f, 0f));
    }

    // 캔버스 좌하단
    public Vector3 GetCanvasPositionBottomLeft()
    {
        return canvas.transform.TransformPoint(new Vector3(-canvasRect.rect.width / 2 + 100f, -canvasRect.rect.height / 2 + 100f, 0f));
    }

    // 캔버스 우하단
    public Vector3 GetCanvasPositionBottomRight()
    {
        return canvas.transform.TransformPoint(new Vector3(canvasRect.rect.width / 2 - 100f, -canvasRect.rect.height / 2 + 100f, 0f));
    }

    // ⚠ 말풍선 앵커 계산 — MR에서는 반드시 (0,0)을 돌려준다 (2026-08-22)
    //
    // 왜 분기가 필요한가 (Kickoff Guide §4-36 / §4-38 / §4-45와 같은 계열, 네 번째 재발)
    // --------------------------------------------------------------------------
    // 아래 데스크톱 식은 "캐릭터보다 300px 위"라는 뜻이다:
    //     charPosition.y + 200 * (char_size / 100) + 100      // char_size=100 → +300
    //
    // 그런데 MR에서 말풍선은 MR/WorldUI/Balloons 밑의 **독립 월드 스페이스 캔버스**이고
    // 부모가 평범한 Transform(scale 1)이다. 이때 anchoredPosition은 사실상 부모 기준
    // 로컬 위치이고 **1 단위 = 1 m**가 된다. 즉 이 값이 그대로 대입되면 말풍선이
    // **300 m 밖으로 텔레포트**한다.
    //
    // 이 값을 쓰는 매니저는 4개다 — AnswerBalloonManager / AnswerBalloonSimpleManager /
    // AskBalloonManager / SubAnswerBalloonManager. 전부 Update()에서 **매 프레임** 대입한다.
    // 호출부 4곳을 각각 고치는 대신 **공급원인 여기 한 곳**을 막는다
    // (§4-33에서 GetMenuPosition에 쓴 것과 같은 처방이고, 새 호출부가 생겨도 자동으로 안전하다).
    //
    // MR에서 말풍선의 실제 배치는 MRBalloonWorldFollow가 **월드 좌표로** 담당한다.
    // 그러므로 여기서는 "부모 원점"만 돌려주면 되고, 그게 (0,0)이다.
    //
    // ⚠ 이 분기를 지우지 말 것. 지우면 다음 증상이 그대로 재현된다 —
    //    "말풍선을 grab/ray로 옮기는 순간 Y가 300으로 튀고 다시는 안 돌아온다."
    //    평소에는 MRBalloonWorldFollow가 매 LateUpdate에 transform.position을 덮어써서
    //    이 대입을 지우고 있지만, grab 중에는 위치 소유권을 사용자에게 넘기느라
    //    덮어쓰기를 멈춘다. 그 순간 이 +300이 살아난다.

    // 캐릭터의 Transform을 기반으로 말풍선의 AnchoredPosition 계산
    public Vector2 GetBalloonAnchoredPosition(RectTransform charTransform)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        // MR: 배치는 MRBalloonWorldFollow가 월드 좌표로 한다. 여기서는 부모 원점만 준다.
        return Vector2.zero;
#else
        Vector2 charPosition = charTransform.anchoredPosition;

        // 캔버스 범위를 벗어나지 않도록 X 좌표 제한
        float leftBound = -canvasRect.rect.width / 2;
        float rightBound = canvasRect.rect.width / 2;
        float charPositionX = Mathf.Clamp(charPosition.x, leftBound + 250, rightBound - 250);

        // Y 좌표는 캐릭터 위치에 비례하여 위로 띄움
        float charSizeScale = SettingManager.Instance.settings.char_size / 100f;
        return new Vector2(charPositionX, charPosition.y + 200 * charSizeScale + 100);
#endif
    }

    // 특정 좌표를 기반으로 말풍선의 AnchoredPosition 계산
    public Vector2 GetBalloonAnchoredPositionByPosition(Vector2 targetPosition)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        // MR: 위와 같은 이유. SubAnswerBalloonManager가 쓴다.
        return Vector2.zero;
#else
        // 캔버스 범위를 벗어나지 않도록 X 좌표 제한
        float leftBound = -canvasRect.rect.width / 2;
        float rightBound = canvasRect.rect.width / 2;
        float positionX = Mathf.Clamp(targetPosition.x, leftBound + 250, rightBound - 250);

        // Y 좌표는 캐릭터 위치에 비례하여 위로 띄움
        float charSizeScale = SettingManager.Instance.settings.char_size / 100f;
        return new Vector2(positionX, targetPosition.y + 200 * charSizeScale + 100);
#endif
    }

    // 특정 메뉴 이름에 따라 하드코딩된 위치 반환
    //
    // 데스크톱은 메인 캔버스의 픽셀 좌표를 그대로 월드로 변환해 쓴다.
    // MR에서는 그럴 수 없다 — 메인 Canvas는 World Space에 lossyScale 0.75라
    // 700px가 525m가 되어 패널이 방 밖으로 날아간다 (MR_Phase_Kickoff_Guide.md §4-36).
    public Vector3 GetMenuPosition(string menuName)
    {
        Vector2 offsetPx = GetMenuOffsetPixels(menuName);

#if UNITY_ANDROID || UNITY_EDITOR
        // 여기서 돌려주는 값은 **최초 소환 기본 위치**일 뿐이다.
        // 이미 배치된 패널을 다시 여는 경우 MRFloatingPanel이 비활성 직전의 포즈를
        // 복원해 이 값을 덮으므로, 사용자가 옮겨둔 자리가 지켜진다 (§4-27).
        return GetMenuPositionMR(offsetPx);
#else
        return canvas.transform.TransformPoint(new Vector3(offsetPx.x, offsetPx.y, 0f));
#endif
    }

    // 메뉴별 캔버스 픽셀 오프셋. 데스크톱과 MR이 공유하는 "배치 의도"다.
    // 값을 바꾸면 양쪽 모두에 반영된다.
    private Vector2 GetMenuOffsetPixels(string menuName)
    {
        switch (menuName)
        {
            case "guideline":
                return new Vector2(0f, 100f);                                                   // 중앙보다 위
            case "charChange":
                return new Vector2(700f, 150f);
            case "characterDetail":
                return new Vector2(150f, 40f);
            case "charSummon":
                return new Vector2(400f, -100f);
            case "chatHistory":
                return new Vector2(300f, -100f);
            case "settings":
                return new Vector2(250f, -50f);
            case "version":
                return new Vector2(0f, -200f);
            case "chatBalloonBottom":
                return new Vector2(0f, -canvasRect.rect.height / 2 + 150f);
            case "debugBalloon2":
                return new Vector2(canvasRect.rect.width / 2 - 250f, canvasRect.rect.height / 2 - 200f);
            case "ocrAutoMapper":
                return new Vector2(-300f, 0f);
            case "pomodoro":
                // 캔버스 우측 상단 (Pomodoro 모드 UI 위치)
                return new Vector2(canvasRect.rect.width / 2 - 250f, canvasRect.rect.height / 2 - 200f);
            case "alarm":
                return new Vector2(300f, 0f);
            case "skill":
                return new Vector2(300f, 0f);
            case "mission":
                return new Vector2(300f, 0f);
            case "calendar":
                return new Vector2(40f, 0f);
            case "todolist":
                return new Vector2(300f, 0f);
            case "aistatus":
                return new Vector2(-300f, 0f);
            case "jukebox":
                return new Vector2(-360f, 0f);
            case "alarmmini":
                return new Vector2(520f, 180f);
            case "inventory":
                return new Vector2(420f, 0f);                                                   // 메인 인벤토리는 우측
            case "inventoryChar":
                return new Vector2(-180f, 0f);                                                  // 캐릭터 인벤토리는 좌측
            case "choiceInput":
                return Vector2.zero;                                                            // 중앙 배치
            default:
                return Vector2.zero;                                                            // 기본값은 중앙
        }
    }

#if UNITY_ANDROID || UNITY_EDITOR
    // 캔버스 픽셀 오프셋 1px당 미터. 700px가 약 0.56m가 되도록 잡았다.
    private const float MenuPixelsToMeters = 0.0008f;

    // 사용자 정면으로부터의 소환 거리(m).
    private const float MenuSpawnDistance = 0.7f;

    // 캔버스 픽셀 오프셋을 사용자 정면 기준 월드 좌표로 환산한다.
    // 좌우(x)는 시선 기준 오른쪽, 상하(y)는 월드 위쪽에 대응시켜 데스크톱의 배치 의도를 유지한다.
    private Vector3 GetMenuPositionMR(Vector2 offsetPx)
    {
        Transform eye = ResolveEyeForMenu();
        if (eye == null)
        {
            // 눈을 못 찾아도 최소한 방 안에 둔다. 트래킹 원점은 바닥이므로 y를 눈높이로 올린다 (§4-4).
            return new Vector3(offsetPx.x * MenuPixelsToMeters,
                               1.2f + offsetPx.y * MenuPixelsToMeters,
                               1.0f);
        }

        Vector3 forward = eye.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);

        return eye.position
             + forward * MenuSpawnDistance
             + right * (offsetPx.x * MenuPixelsToMeters)
             + Vector3.up * (offsetPx.y * MenuPixelsToMeters);
    }

    // MR 코드에서는 Camera.main을 직접 쓰지 않는다 — CenterEyeAnchor를 이름으로 먼저 찾는다.
    // FindFirstObjectByType 폴백은 두지 않는다: 그 API는 컴포넌트가 disabled여도 반환해
    // 좌안 카메라나 PortraitCamera를 집을 수 있다 (§4-28 정정).
    private Transform ResolveEyeForMenu()
    {
        GameObject byName = GameObject.Find("CenterEyeAnchor");
        if (byName != null)
        {
            return byName.transform;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            return cam.transform;
        }

        return null;
    }
#endif
}
