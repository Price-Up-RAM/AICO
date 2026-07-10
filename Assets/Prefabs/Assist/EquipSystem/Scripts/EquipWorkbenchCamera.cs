using UnityEngine;

// EquipSystem 워크벤치 카메라: 피벗 궤도(우클릭 드래그) / 줌(휠) / 팬(휠클릭 드래그) /
// F=선택 캐릭터 프레이밍 / T=턴테이블 토글. 선택 캐릭터는 EquipWorkbenchController.Instance에서 읽는다.
// 캐릭터 스케일 극단(1~120000) 대응: 줌은 곱셈, 팬 속도는 거리 비례, 프레이밍은 렌더러 바운즈 기반.
public class EquipWorkbenchCamera : MonoBehaviour
{
    [Header("궤도 (우클릭 드래그)")]
    public float orbitSpeed = 4f;        // 드래그 픽셀당 회전 각도 배율
    public float pitchMin = -85f;        // 피치 하한
    public float pitchMax = 85f;         // 피치 상한

    [Header("줌 (마우스 휠)")]
    public float zoomSpeed = 0.12f;      // 휠 틱당 거리 곱셈 비율
    public float distanceMin = 0.01f;    // 최소 거리
    public float distanceMax = 1000000f; // 최대 거리 (극단 스케일 캐릭터 대응)

    [Header("팬 (휠클릭 드래그)")]
    public float panSpeed = 0.0015f;     // 드래그 픽셀당 이동 배율 (거리 비례)

    [Header("턴테이블 (T 토글)")]
    public float turntableSpeed = 20f;   // 초당 회전 각도

    [Header("키 바인딩")]
    public KeyCode frameKey = KeyCode.F;      // 선택 캐릭터 프레이밍
    public KeyCode turntableKey = KeyCode.T;  // 턴테이블 on/off

    [Header("프레이밍")]
    public float framePadding = 1.6f;    // 바운즈 반경 대비 여유 배율

    private Vector3 pivot = Vector3.zero;   // 궤도 중심점
    private float yaw = 0f;                 // 수평 각도
    private float pitch = 20f;              // 수직 각도
    private float distance = 5f;            // 피벗까지 거리
    private bool turntableOn = false;       // 턴테이블 상태
    private Vector3 lastMousePos;           // 드래그 델타 계산용

    // 시작 시 현재 트랜스폼에서 피벗/각도/거리를 역산해 이어받는다 (씬 배치 그대로 출발)
    private void Start()
    {
        Vector3 forward = transform.forward;
        pivot = transform.position + forward * distance;
        yaw = transform.eulerAngles.y;
        pitch = NormalizePitch(transform.eulerAngles.x);
        ApplyTransform();
    }

    // 입력 처리: 궤도/줌/팬/프레이밍/턴테이블
    private void Update()
    {
        Vector3 mousePos = Input.mousePosition;
        Vector3 mouseDelta = mousePos - lastMousePos;
        lastMousePos = mousePos;

        // 우클릭 드래그 → 궤도 회전
        if (Input.GetMouseButton(1))
        {
            yaw = yaw + mouseDelta.x * orbitSpeed * 0.1f;
            pitch = pitch - mouseDelta.y * orbitSpeed * 0.1f;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        }

        // 휠클릭 드래그 → 팬 (거리 비례 속도라 어떤 스케일에서도 감각 일정)
        if (Input.GetMouseButton(2))
        {
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            float scale = distance * panSpeed;
            pivot = pivot - right * mouseDelta.x * scale;
            pivot = pivot - up * mouseDelta.y * scale;
        }

        // 휠 → 줌 (곱셈이라 극단 스케일에서도 유효)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(scroll, 0f) == false)
        {
            distance = distance * (1f - scroll * zoomSpeed * 10f);
            distance = Mathf.Clamp(distance, distanceMin, distanceMax);
            AdaptClipPlanes();  // 극단 스케일: 프레이밍 전에도 줌만으로 캐릭터가 보이게 근/원평면 추종
        }

        // F → 선택 캐릭터 프레이밍
        if (Input.GetKeyDown(frameKey))
        {
            FrameSelected();
        }

        // T → 턴테이블 토글
        if (Input.GetKeyDown(turntableKey))
        {
            turntableOn = !turntableOn;
        }

        // 턴테이블 회전
        if (turntableOn)
        {
            yaw = yaw + turntableSpeed * Time.deltaTime;
        }

        ApplyTransform();
    }

    // 선택 캐릭터를 화면에 꽉 차게 프레이밍 (렌더러 바운즈 기반, 없으면 콜라이더/위치 폴백)
    public void FrameSelected()
    {
        // 워크벤치 컨트롤러가 씬에 없을 수 있는 데모 코드라 명시적 안내 가드 (EquipDemoController 선례)
        EquipWorkbenchController controller = EquipWorkbenchController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[EquipWorkbenchCamera] 씬에 EquipWorkbenchController 없음 — 프레이밍 불가");
            return;
        }

        GameObject selected = controller.Selected;
        if (selected == null)
        {
            Debug.LogWarning("[EquipWorkbenchCamera] 선택된 캐릭터 없음 — 워크벤치 패널에서 캐릭터를 선택하세요");
            return;
        }

        Bounds bounds;
        bool hasBounds = TryGetWorldBounds(selected, out bounds);
        if (hasBounds == false)
        {
            // 렌더러/콜라이더가 전혀 없으면 위치만 바라본다
            pivot = selected.transform.position;
            ApplyTransform();
            return;
        }

        pivot = bounds.center;

        // 바운즈 반경과 카메라 FOV로 거리를 계산 (스케일 1~120000 어디서든 동일한 화면 점유율)
        float radius = bounds.extents.magnitude;
        if (radius < 0.0001f)
        {
            radius = 0.0001f;
        }

        float fov = 60f;
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            fov = cam.fieldOfView;
        }

        float halfFovRad = fov * 0.5f * Mathf.Deg2Rad;
        float tanHalf = Mathf.Tan(halfFovRad);
        if (tanHalf < 0.0001f)
        {
            tanHalf = 0.0001f;
        }

        distance = radius / tanHalf * framePadding;
        distance = Mathf.Clamp(distance, distanceMin, distanceMax);

        // 극단 스케일에서 근평면/원평면이 안 맞으면 프레이밍해도 안 보이므로 함께 보정
        AdaptClipPlanes();

        ApplyTransform();
    }

    // 근/원평면을 현재 distance에 맞춰 보정 (프레이밍·줌 공용 — 스케일 1~120000 대응)
    private void AdaptClipPlanes()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null)
        {
            return;
        }

        float near = distance * 0.001f;
        if (near < 0.001f)
        {
            near = 0.001f;
        }
        cam.nearClipPlane = near;

        float far = distance * 100f;
        if (far < 1000f)
        {
            far = 1000f;
        }
        cam.farClipPlane = far;
    }

    // 대상 계층의 모든 렌더러(비활성 제외)를 합친 월드 바운즈. 렌더러 없으면 콜라이더 폴백
    private bool TryGetWorldBounds(GameObject target, out Bounds bounds)
    {
        bounds = new Bounds(target.transform.position, Vector3.zero);
        bool found = false;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(false);
        foreach (Renderer renderer in renderers)
        {
            if (found == false)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (found)
        {
            return true;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(false);
        foreach (Collider collider in colliders)
        {
            if (found == false)
            {
                bounds = collider.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return found;
    }

    // yaw/pitch/distance/pivot에서 카메라 위치·회전 계산
    private void ApplyTransform()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
        transform.position = pivot + offset;
        transform.rotation = rotation;
    }

    // 오일러 x각(0~360)을 -180~180 피치로 정규화
    private float NormalizePitch(float eulerX)
    {
        float value = eulerX;
        if (value > 180f)
        {
            value = value - 360f;
        }
        return Mathf.Clamp(value, pitchMin, pitchMax);
    }

    // 조작 안내 오버레이 (우상단, 메인 패널과 겹치지 않게)
    private void OnGUI()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("[카메라] 우클릭 드래그: 궤도 / 휠: 줌 / 휠클릭 드래그: 팬");
        string turntableState = "OFF";
        if (turntableOn)
        {
            turntableState = "ON";
        }
        sb.AppendLine($"{frameKey}: 선택 캐릭터 프레이밍 / {turntableKey}: 턴테이블 ({turntableState})");
        GUI.Label(new Rect(Screen.width - 420f, 10f, 410f, 50f), sb.ToString());
    }
}
