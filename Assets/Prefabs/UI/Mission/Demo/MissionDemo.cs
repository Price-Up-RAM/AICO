using UnityEngine;

/// <summary>
/// Mission 데모. 씬 시작 시 미션 패널을 열고, 보기 좋게 샘플 진행도를 주입한다.
/// (진행/인벤토리는 persistentDataPath에 저장되므로, ReportFlag/조건부 Report로 멱등 처리)
/// </summary>
public class MissionDemo : MonoBehaviour
{
    [SerializeField] private MissionView view;
    [SerializeField] private bool seedSampleProgress = true;
    [Tooltip("주언어 (ko/en/ja)")]
    [SerializeField] private string language = "ko";

    private void Start()
    {
        if (view == null)
        {
            view = FindFirstObjectByType<MissionView>();
        }

        if (view == null)
        {
            Debug.LogWarning("[MissionDemo] MissionView를 찾지 못했습니다.");
            return;
        }

        if (MissionList.Instance != null)
        {
            MissionList.Instance.Language = language;
        }

        if (seedSampleProgress)
        {
            SeedSamples();
        }

        view.Show();
    }

    // 데모용 샘플 상태: 수령 가능 카드 + 진행 중 게이지를 섞어 보여준다. 멱등.
    private void SeedSamples()
    {
        MissionList m = MissionList.Instance;
        if (m == null)
        {
            return;
        }

        // 수령 가능(받기 + 도장 체험)
        m.ReportFlag("OB0001");
        m.ReportFlag("OB0002");

        // 진행 중(부분 게이지) — 최초 1회만 채워 누적 방지
        MissionInfo cv = m.GetById("CV0007");
        if (cv != null && cv.current == 0)
        {
            m.Report("CV0007", 6);   // Tiered 10/50/100 → 6/10
        }

        MissionInfo af = m.GetById("AF0001");
        if (af != null && af.current == 0)
        {
            m.Report("AF0001", 7);   // Increment 10N → 7/10
        }
    }

#if UNITY_EDITOR
    public void EditorSet(MissionView missionView, string lang)
    {
        view = missionView;
        language = lang;
    }
#endif
}
