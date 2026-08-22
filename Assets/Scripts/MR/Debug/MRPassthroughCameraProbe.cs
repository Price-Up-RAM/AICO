// Passthrough Camera Access 런타임 진단 프로브 (MR_HandFrame_ImageInput_Plan.md §8-1)
//
// 목적
// ----
// Play 한 번으로 4-C의 선행 조건을 전부 확인한다. 지원 여부·권한·해상도 협상 결과·
// 좌표 변환 정확도가 한 화면에 나오므로, "안 되는데 왜인지 모르겠다"로 라운드를 태우지 않는다.
//
// 로그 규약 (Kickoff Guide §7-1 C / D)
// -----------------------------------
// C. 한 줄에 "지금 값"과 "기대 값"을 같이 찍는다. 확인과 검증이 한 번에 끝난다.
// D. 경로가 둘이므로 태그로 가른다 — [MRPCA/에디터] vs [MRPCA/실기].
//    PassthroughCameraAccess.Update()에는 에디터 전용 경로(CameraAcquireLatestCpuImage)가
//    따로 있고 IsSupported도 에디터에서는 Link 헤드셋을 통과시킨다. 두 경로의 결과가
//    다를 수 있으므로 로그만 보고 어느 쪽인지 알 수 있어야 한다.
//
// 이 컴포넌트가 하지 않는 것
// ------------------------
// - PassthroughCameraAccess를 AddComponent 하지 않는다. §4-42/§4-46이 보여준 대로
//   폴백 생성은 "정식 경로가 망가진 것"을 무증상으로 만든다. 없으면 없다고 보고만 한다.
// - 설정을 고치지 않는다. 프로젝트 설정 점검·수정은 Tools → MR → 11 (에디터 전용)이 맡는다.
//
// 사용
// ----
// 아무 GameObject에나 붙이고 Play. 결과는 Console 3~4줄.
// 실기에서는: adb logcat -c && adb logcat -v time -s Unity:V  (Kickoff §6)

using System;
using System.Collections;
using Meta.XR;
using UnityEngine;

public class MRPassthroughCameraProbe : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("비우면 씬에서 찾는다. 비활성 오브젝트도 포함해 찾는다(§4-50). " +
             "어느 경로로 찾았는지는 로그에 남는다.")]
    [SerializeField] private PassthroughCameraAccess cameraAccess;

    [Header("대기")]
    [Tooltip("IsPlaying이 될 때까지 기다리는 최대 시간(초). 권한 팝업을 사람이 누를 시간을 포함한다.")]
    [SerializeField] private float waitTimeoutSeconds = 20f;

    [Tooltip("대기하는 동안 1초마다 중간 상태를 찍는다. 권한 팝업 흐름을 볼 때 켠다.")]
    [SerializeField] private bool logWhileWaiting;

    [Header("검증")]
    [Tooltip("뷰포트↔월드 변환을 왕복시켜 오차를 잰다. 손 프레임 4점 투영(§4-4)의 선행 검증이다.")]
    [SerializeField] private bool runProjectionCheck = true;

    [Tooltip("GetColors()를 딱 한 번 불러 배열 길이를 확인한다. " +
             "SDK가 x*y*4 칸을 잡으므로 .Length를 픽셀 수로 믿으면 안 된다(설계서 §5). " +
             "블로킹 호출이라 기본은 꺼둔다.")]
    [SerializeField] private bool checkColorsOnce;

    [Tooltip("카메라 텍스처를 프로젝트 루트에 PNG로 한 번 저장한다(MRPCA_dump.png). " +
             "화면에 이상한 무늬가 뜰 때 '데이터가 없다'인지 '해석이 틀렸다'인지 가르는 용도. " +
             "Assets 밖에 저장하므로 Unity가 임포트하지 않는다.")]
    [SerializeField] private bool dumpTextureOnce;

    // 에디터 경로와 실기 경로를 로그에서 즉시 가르기 위한 태그 (§7-1 D)
    private string _tag;

    private void Start()
    {
        if (Application.isEditor)
        {
            _tag = "[MRPCA/에디터]";
        }
        else
        {
            _tag = "[MRPCA/실기]";
        }

        LogEnvironment();
        StartCoroutine(WaitAndReport());
    }

    // ---------------------------------------------------------
    // 1행: 환경 — 지원 여부 / 헤드셋 / 권한 / 컴포넌트 배선
    // ---------------------------------------------------------
    private void LogEnvironment()
    {
        string wiring = ResolveCameraAccess();

        string permission = OVRPermissionsRequester.PassthroughCameraAccessPermission;
        bool granted = UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission);

        // 에디터에서는 이 API가 항상 true를 준다. 실기 결과와 헷갈리지 않도록 명시한다.
        string grantedNote = "";
        if (Application.isEditor)
        {
            grantedNote = " (에디터는 항상 True — 실기에서 다시 볼 것)";
        }

        string headset = OVRPlugin.GetSystemHeadsetType().ToString();

        // IsSupported는 에디터/실기의 판정 조건이 다르다 (설계서 §3-1).
        //   에디터: Meta_Link_Quest_3 / Meta_Link_Quest_3S / None  ← 버전을 전혀 안 본다
        //   실기  : Meta_Quest_3 / Meta_Quest_3S + Horizon OS v74 이상
        //
        // ⚠ IsSupported=True 여도 Link 경로가 제대로 돈다는 보장이 아니다.
        //    Play() 실패 시 SDK가 내는 메시지가 조건을 밝힌다 —
        //    "Requires: 'Meta Horizon Link' v85+ or 'Meta XR Simulator' v85+,
        //     and Quest 3 or Quest 3S headset running HzOS v85+"
        //    즉 Link 경유는 v74가 아니라 **v85 이상**이 필요하다. IsSupported는 이걸 확인하지 않는다.
        bool supported = PassthroughCameraAccess.IsSupported;
        string supportedExpect = "기대 True (실기 Quest3/3S + HzOS v74+ / Link는 v85+ 필요, 단 IsSupported는 버전 미검사)";

        Debug.Log($"{_tag} 환경 | IsSupported={supported} ({supportedExpect})" +
                  $" | 헤드셋={headset}" +
                  $" | 권한({permission})={granted}{grantedNote}" +
                  $" | 컴포넌트={wiring}");
    }

    // 인스펙터 배선을 우선하고, 없으면 자동 탐색한다.
    // §4-50: 자동 탐색은 기본적으로 비활성 오브젝트를 반환하지 않으므로 Include를 명시한다.
    // 어느 경로로 찾았는지 로그에 남기는 것이 핵심이다 — 배선 누락이 무증상으로 지나가지 않게(§4-51).
    private string ResolveCameraAccess()
    {
        if (cameraAccess != null)
        {
            return $"{cameraAccess.gameObject.name} (인스펙터 배선)";
        }

        PassthroughCameraAccess[] found =
            FindObjectsByType<PassthroughCameraAccess>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (found.Length == 0)
        {
            return "❌ 씬에 없음 → Meta → Tools → Building Blocks → 'Passthrough Camera Access' 추가";
        }

        cameraAccess = found[0];

        string note = "";
        if (found.Length > 1)
        {
            // 눈마다 인스턴스를 따로 두는 것이 정상 사용법이므로 오류는 아니다. 다만 어느 쪽을 잡았는지 밝힌다.
            note = $" ⚠ {found.Length}개 발견 — 첫 번째를 쓴다";
        }

        string activeNote = "";
        if (!cameraAccess.gameObject.activeInHierarchy)
        {
            activeNote = " ⚠ GameObject 비활성";
        }
        else if (!cameraAccess.enabled)
        {
            activeNote = " ⚠ 컴포넌트 disabled";
        }

        return $"{cameraAccess.gameObject.name} (자동 탐색){note}{activeNote}";
    }

    // ---------------------------------------------------------
    // IsPlaying을 기다렸다가 스펙·투영 검증을 찍는다
    // ---------------------------------------------------------
    private IEnumerator WaitAndReport()
    {
        if (cameraAccess == null)
        {
            Debug.LogWarning($"{_tag} 컴포넌트가 없어 여기서 중단한다. " +
                             "설정 상태는 Tools → MR → 11 로 먼저 확인할 것.");
            yield break;
        }

        float elapsed = 0f;
        float nextTick = 1f;

        while (!cameraAccess.IsPlaying && elapsed < waitTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            if (logWhileWaiting && elapsed >= nextTick)
            {
                nextTick += 1f;
                bool g = UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                    OVRPermissionsRequester.PassthroughCameraAccessPermission);
                Debug.Log($"{_tag} 대기 {elapsed:F1}s / {waitTimeoutSeconds:F0}s | 권한={g} | IsPlaying=False");
            }

            yield return null;
        }

        if (!cameraAccess.IsPlaying)
        {
            ReportFailure(elapsed);
            yield break;
        }

        // 텍스처가 실제로 한 번 갱신될 때까지 한 프레임 더 기다린다.
        // IsPlaying만 보고 Intrinsics를 읽으면 첫 프레임 값이 비어 있을 수 있다.
        while (!cameraAccess.IsUpdatedThisFrame && elapsed < waitTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        LogCameraSpec(elapsed);

        if (runProjectionCheck)
        {
            LogProjectionCheck();
        }

        if (checkColorsOnce)
        {
            LogColorsCheck();
        }

        if (dumpTextureOnce)
        {
            DumpTexture();
        }
    }

    // ---------------------------------------------------------
    // 텍스처를 PNG로 저장 + 픽셀 샘플 로그
    // ---------------------------------------------------------
    // "화면에 이상한 무늬가 뜬다"를 세 갈래로 가르기 위한 도구다.
    //   ① 전부 같은 색 / 검정   → 데이터가 아예 안 온다
    //   ② 컬러바 같은 규칙적 패턴 → 링크·시뮬레이터가 주는 테스트 신호다 (실제 카메라 아님)
    //   ③ 사선으로 흐르는 잡음   → 데이터는 오는데 stride/포맷 해석이 어긋난 것이다
    // 눈으로 보는 것과 별개로 픽셀 값을 숫자로 남긴다 — 스크린샷 없이도 ①/②/③을 가를 수 있다.
    private void DumpTexture()
    {
        Texture src = cameraAccess.GetTexture();
        if (src == null)
        {
            Debug.LogWarning($"{_tag} 덤프 실패 — GetTexture()가 null이다.");
            return;
        }

        Vector2Int res = cameraAccess.CurrentResolution;
        Texture2D readable = null;
        string route;

        // 에디터 경로에서는 _texture가 Texture2D(RGBA32)다. LoadRawTextureData가 넣은 값을
        // 그대로 볼 수 있으므로 중간 변환 없이 이쪽이 가장 정직한 증거다.
        Texture2D asTex2D = src as Texture2D;
        if (asTex2D != null)
        {
            readable = asTex2D;
            route = "Texture2D 직접";
        }
        else
        {
            // 실기 경로는 RenderTexture다. GetColors()로 CPU에 내린다.
            var colors = cameraAccess.GetColors();
            if (colors.Length < res.x * res.y)
            {
                Debug.LogWarning($"{_tag} 덤프 실패 — 색 버퍼가 {colors.Length}칸으로 " +
                                 $"필요한 {res.x * res.y}칸보다 작다.");
                return;
            }

            readable = new Texture2D(res.x, res.y, TextureFormat.RGBA32, false);
            readable.SetPixelData(colors.GetSubArray(0, res.x * res.y), 0);
            readable.Apply(false, false);
            route = "GetColors()";
        }

        // 픽셀 샘플 — 가로로 균등하게 8점, 세로는 세 줄. 사선 패턴이면 줄마다 값이 밀린다.
        string rowTop = SampleRow(readable, res, res.y - 2);
        string rowMid = SampleRow(readable, res, res.y / 2);
        string rowBottom = SampleRow(readable, res, 1);

        Debug.Log($"{_tag} 픽셀샘플 ({route}, 좌→우 8점, RGB)" +
                  $"\n  위   y={res.y - 2,4} | {rowTop}" +
                  $"\n  중앙 y={res.y / 2,4} | {rowMid}" +
                  $"\n  아래 y={1,4} | {rowBottom}" +
                  "\n  판독: 전부 같은 값 → 데이터 없음" +
                  " / **값이 0 아니면 255 + 세 행이 동일 → 합성 컬러바** (Link가 주는 테스트 신호, 실제 카메라 아님)" +
                  " / 행마다 조금씩 밀림 → stride 어긋남" +
                  " / 값이 지저분하고 행마다 다름 → 진짜 영상일 가능성");

        string path = System.IO.Path.Combine(Application.dataPath, "..", "MRPCA_dump.png");
        path = System.IO.Path.GetFullPath(path);

        try
        {
            System.IO.File.WriteAllBytes(path, readable.EncodeToPNG());
            Debug.Log($"{_tag} 덤프 저장 → {path}" +
                      "  (Unity 텍스처는 아래에서 위로 저장되므로 PNG가 상하 반전돼 보일 수 있다 — 그건 정상)");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{_tag} 덤프 저장 실패: {e.Message}");
        }

        if (readable != asTex2D)
        {
            Destroy(readable);
        }
    }

    private string SampleRow(Texture2D tex, Vector2Int res, int y)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 8; i++)
        {
            int x = Mathf.Clamp(i * (res.x - 1) / 7, 0, res.x - 1);
            Color32 c = tex.GetPixel(x, y);
            if (i > 0)
            {
                sb.Append(' ');
            }
            sb.Append($"({c.r,3},{c.g,3},{c.b,3})");
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------
    // 2행: 카메라 스펙 — 해상도 협상 결과와 Intrinsics
    // ---------------------------------------------------------
    private void LogCameraSpec(float elapsed)
    {
        Vector2Int actual = cameraAccess.CurrentResolution;
        Vector2Int requested = cameraAccess.RequestedResolution;
        PassthroughCameraAccess.CameraIntrinsics k = cameraAccess.Intrinsics;

        // 요청과 실제가 다르면 SDK가 센서 크롭 보정을 넣는다(CalcSensorCropRegion).
        // 그래서 Intrinsics로 직접 투영하면 안 되고 WorldToViewportPoint를 써야 한다 — 설계서 §4-4.
        string resNote = "일치";
        if (actual != requested)
        {
            resNote = "⚠ 불일치 → 센서 크롭 보정이 개입한다. 직접 투영 금지, WorldToViewportPoint를 쓸 것";
        }

        double freshnessMs = (DateTime.UtcNow - cameraAccess.Timestamp).TotalMilliseconds;

        string texInfo = "없음";
        Texture tex = cameraAccess.GetTexture();
        if (tex != null)
        {
            texInfo = $"{tex.width}x{tex.height} {tex.graphicsFormat}";
        }

        Debug.Log($"{_tag} 카메라 | 눈={cameraAccess.CameraPosition}" +
                  $" | 해상도 {actual.x}x{actual.y} (요청 {requested.x}x{requested.y}, {resNote})" +
                  $" | 센서 {k.SensorResolution.x}x{k.SensorResolution.y}" +
                  $" | f=({k.FocalLength.x:F1}, {k.FocalLength.y:F1})" +
                  $" pp=({k.PrincipalPoint.x:F1}, {k.PrincipalPoint.y:F1})" +
                  $" | lensOffset pos={k.LensOffset.position}" +
                  $" | MaxFramerate={cameraAccess.MaxFramerate}" +
                  $" | 프레임신선도 {freshnessMs:F0}ms (기대 100ms 이내)" +
                  $" | 텍스처 {texInfo}" +
                  $" | 기동까지 {elapsed:F1}s");
    }

    // ---------------------------------------------------------
    // 3행: 투영 왕복 검증 — 손 프레임 4점 투영(§4-4)의 선행 확인
    // ---------------------------------------------------------
    // 뷰포트 점 → 월드 레이 → 그 레이 위의 점 → 다시 뷰포트.
    // 같은 카메라 포즈를 명시로 넘기므로 오차는 0에 수렴해야 한다.
    // 여기서 어긋나면 손 관절 투영도 어긋난다 — 크롭을 짜기 전에 여기서 잡는다.
    private void LogProjectionCheck()
    {
        Pose camPose = cameraAccess.GetCameraPose();

        Vector2 center = RoundTrip(new Vector2(0.5f, 0.5f), camPose, out float centerError);
        Vector2 corner = RoundTrip(new Vector2(0.25f, 0.75f), camPose, out float cornerError);

        float worst = Mathf.Max(centerError, cornerError);

        // 뷰포트 단위 오차다. 1280px 폭에서 0.001 = 1.28px.
        string verdict = "통과";
        if (worst > 0.002f)
        {
            verdict = "❌ 실패 — 카메라 포즈나 Intrinsics가 이상하다. 크롭 구현 전에 원인을 볼 것";
        }

        Debug.Log($"{_tag} 투영검증 | (0.500,0.500)→({center.x:F4},{center.y:F4}) 오차 {centerError:F4}" +
                  $" | (0.250,0.750)→({corner.x:F4},{corner.y:F4}) 오차 {cornerError:F4}" +
                  $" | 최대 {worst:F4} (임계 0.0020 = 1280px에서 약 2.6px) → {verdict}" +
                  $" | 카메라포즈 pos={camPose.position} fwd={camPose.rotation * Vector3.forward}");
    }

    private Vector2 RoundTrip(Vector2 viewport, Pose camPose, out float error)
    {
        Ray ray = cameraAccess.ViewportPointToRay(viewport, camPose);
        Vector3 worldPoint = ray.origin + ray.direction.normalized * 1.5f;   // 1.5m 앞의 가상 지점
        Vector2 back = cameraAccess.WorldToViewportPoint(worldPoint, camPose);
        error = Vector2.Distance(viewport, back);
        return back;
    }

    // ---------------------------------------------------------
    // 4행(선택): GetColors()의 배열 길이 확인
    // ---------------------------------------------------------
    // SDK가 x*y*4 칸을 잡는다. .Length를 픽셀 수로 믿으면 크롭이 조용히 어긋난다(설계서 §5-2).
    // 블로킹 호출이므로 기본은 꺼둔다.
    private void LogColorsCheck()
    {
        Vector2Int res = cameraAccess.CurrentResolution;
        int pixels = res.x * res.y;

        float t0 = Time.realtimeSinceStartup;
        var colors = cameraAccess.GetColors();
        float costMs = (Time.realtimeSinceStartup - t0) * 1000f;

        string ratio = "?";
        if (pixels > 0)
        {
            ratio = $"{(float)colors.Length / pixels:F1}배";
        }

        Debug.Log($"{_tag} GetColors | 배열 {colors.Length}칸 | 실제 픽셀 {pixels}칸 ({ratio})" +
                  $" | 인덱싱은 y*{res.x}+x 로 할 것, .Length를 픽셀 수로 쓰지 말 것" +
                  $" | 호출 비용 {costMs:F1}ms (블로킹 — 셔터 순간 1회만)");
    }

    // ---------------------------------------------------------
    // 실패 보고 — 원인 후보를 값과 함께 한 줄로
    // ---------------------------------------------------------
    private void ReportFailure(float elapsed)
    {
        string permission = OVRPermissionsRequester.PassthroughCameraAccessPermission;
        bool granted = UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission);
        bool supported = PassthroughCameraAccess.IsSupported;
        bool objActive = cameraAccess.gameObject.activeInHierarchy;
        bool compEnabled = cameraAccess.enabled;

        Debug.LogError($"{_tag} ❌ {elapsed:F1}초 대기했지만 IsPlaying=False" +
                       $" | 권한={granted} (기대 True)" +
                       $" | IsSupported={supported} (기대 True)" +
                       $" | GameObject 활성={objActive} (기대 True)" +
                       $" | 컴포넌트 enabled={compEnabled} (기대 True)" +
                       $" | 헤드셋={OVRPlugin.GetSystemHeadsetType()}" +
                       "\n다음 순서로 볼 것: " +
                       "① Tools → MR → 11 로 프로젝트 설정 점검 " +
                       "② isPassthroughCameraAccessEnabled 켜고 " +
                       "Meta → Tools → Android Manifest Tool → 'Update AndroidManifest.xml for Store Compatibility' " +
                       "③ 매니페스트에 horizonos.permission.HEADSET_CAMERA 가 실제로 들어갔는지 파일을 열어 확인 " +
                       "④ 권한이 False라면: 시작 시 자동 요청(OVRManager)도 직접 요청 코드도 없는 상태다 " +
                       "⑤ 실기라면 Horizon OS v74 이상인지 확인");
    }
}
