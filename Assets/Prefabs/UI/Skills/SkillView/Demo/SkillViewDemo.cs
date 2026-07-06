using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SkillView 데모 컨트롤러 (데모 씬 전용). 서버 없이 기능을 검증하기 위한 스캐폴딩이다.
/// 시작 시 실서버 연동(SkillCatalogClient)을 끄고, 가짜 스킬 카탈로그를 SkillView에 주입한다.
///
/// 검증 포인트:
///  - CrudRow 좌측 스위치형 on/off 토글
///  - Header 우측 "목록" 버튼 → 전체 리스트(이름 + on/off 스위치) 오버레이, 헤더/창 불변
/// </summary>
public class SkillViewDemo : MonoBehaviour
{
    [SerializeField] private SkillView view;

    private void Start()
    {
        if (view == null)
        {
            view = FindObjectOfType<SkillView>();
        }
        if (view == null)
        {
            Debug.LogWarning("[SkillViewDemo] SkillView를 찾지 못했습니다.");
            return;
        }

        // 데모에선 실서버 연동을 꺼서 Connection Error 자동표시를 막는다.
        SkillCatalogClient client = view.GetComponent<SkillCatalogClient>();
        if (client != null)
        {
            client.enabled = false;
        }

        view.Show();
        view.SetSkills(BuildFakeCatalog());
    }

    // 서버 /skills/list 응답을 흉내낸 가짜 카탈로그(다양한 source/on-off/카테고리).
    private static List<SkillView.SkillEntry> BuildFakeCatalog()
    {
        return new List<SkillView.SkillEntry>
        {
            Registry("physical_click", "mouse", "실제 마우스를 이동시켜 물리 클릭을 수행합니다.", true,
                Param("winX", "int", true, "X 좌표"), Param("winY", "int", true, "Y 좌표")),
            Registry("type_text", "keyboard", "지정한 문자열을 키보드로 입력합니다.", true,
                Param("text", "string", true, "입력할 문자열")),
            Registry("run_process", "system", "시스템에서 프로세스를 실행합니다.", false,
                Param("path", "string", true, "실행 파일 경로")),
            Registry("take_screenshot", "system", "현재 화면을 캡처합니다.", true),
            Official("summarize_clipboard", "Skill", "클립보드 내용을 요약합니다.", true),
            Official("translate_selection", "Skill", "선택 영역을 번역합니다.", false),
            Custom("skill_skip_story", "Skill", "스토리를 스킵하는 커스텀 스킬",
                "# 절차\n1. 스킵 버튼을 찾는다.\n2. 클릭한다.\n3. 확인 팝업이 뜨면 예를 누른다.", true, false),
            Custom("skill_greeting", "Skill", "인사 스킬 (official + custom 오버라이드)",
                "# 인사\n사용자에게 상황에 맞는 인사를 건넨다.", true, true),
        };
    }

    private static SkillView.SkillParam Param(string name, string type, bool required, string desc)
    {
        return new SkillView.SkillParam { name = name, type = type, required = required, description = desc };
    }

    private static SkillView.SkillEntry Registry(string id, string category, string desc, bool enabled, params SkillView.SkillParam[] parameters)
    {
        return new SkillView.SkillEntry
        {
            id = id,
            displayName = id,
            source = "unity",
            category = category,
            description = desc,
            isOfficial = true,
            isEnabled = enabled,
            parameters = new List<SkillView.SkillParam>(parameters),
        };
    }

    private static SkillView.SkillEntry Official(string id, string category, string desc, bool enabled)
    {
        return new SkillView.SkillEntry
        {
            id = id,
            displayName = id,
            source = "official",
            category = category,
            description = desc,
            content = desc,
            isOfficial = true,
            isEnabled = enabled,
        };
    }

    private static SkillView.SkillEntry Custom(string id, string category, string desc, string content, bool enabled, bool official)
    {
        return new SkillView.SkillEntry
        {
            id = id,
            displayName = id,
            source = "custom",
            category = category,
            description = desc,
            content = content,
            isCustom = true,
            isOfficial = official,
            isEnabled = enabled,
        };
    }

#if UNITY_EDITOR
    public void EditorSet(SkillView v)
    {
        view = v;
    }
#endif
}
