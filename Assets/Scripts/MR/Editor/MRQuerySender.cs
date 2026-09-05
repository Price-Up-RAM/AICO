using UnityEditor;
using UnityEngine;

// Play 중에 에디터에서 직접 질의를 보낸다 (Phase 5 개발 편의).
//
// 왜 필요한가: 개발 환경이 Editor + Quest Link인데
//  - TouchScreenKeyboard(Quest 시스템 키보드)는 Android 런타임 전용이라 Editor에서 뜨지 않는다
//  - STT는 한 언어만 인식해서 영문 곡 이름 같은 걸 못 부른다
// 그래서 "campfire 틀어줘" 같은 정확한 문장을 넣을 방법이 없었다.
//
// 이 창은 STT와 InputField를 모두 우회해 ApiVlRouterManager로 바로 보낸다.
// 실기 UX와는 무관한 개발 도구다.
public class MRQuerySender : EditorWindow
{
    private string _query = "";
    private Vector2 _scroll;
    // 시연용 프리셋 (2026-08-26).
    //
    // 손 프레임 사진 질문은 VL이 '확실히 답하는 것'만 둔다.
    // 실측: "사진 속 인물이 누구야"는 VL이 구조적으로 못 답한다 —
    // 모르는 개인은 학습에 없고, 서버 파이프라인이 '키워드 생성 → 웹검색'이라
    // 이미지 자체로는 역검색을 못 한다(백엔드 확인). 그래서 인물 식별 질문은 뺐다.
    // 웹검색은 settings.ai_web_search=off라 이번 시연에서는 쓰지 않는다.
    private static readonly string[] Presets =
    {
        // --- 사진 질문 (손 프레임으로 찍은 뒤) ---
        "이게 뭐야?",
        "여기 뭐가 보여?",
        "뭐라고 적혀 있어?",
        "이 화면에 뭐가 떠 있어?",
        "책상 위에 뭐가 있어?",
        "무슨 색이야?",
        // --- 스킬 ---
        "campfire 틀어줘",
        "모닥불 틀어줘",
        "무슨 곡 있어?",
        "음악 정지해줘",
        "다음 곡 틀어줘",
        "5분 타이머 설정해줘",
        "춤춰줘",
        "오늘 일정 알려줘",
        // --- 일반 대화 ---
        "안녕? 오늘 기분 어때?"
    };

    [MenuItem("Tools/MR/질문 보내기 창")]
    public static void Open()
    {
        MRQuerySender w = GetWindow<MRQuerySender>("MR 질문 보내기");
        w.minSize = new Vector2(320f, 260f);
        w.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Play 중에 라우터로 직접 질의를 보낸다", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "STT와 InputField를 우회해 /router/job/run으로 바로 보낸다.\n" +
            "Editor + Quest Link 환경에서 시스템 키보드가 뜨지 않는 것을 우회하기 위한 개발 도구다.",
            MessageType.Info);

        EditorGUILayout.Space();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 중에만 보낼 수 있다.", MessageType.Warning);
        }

        EditorGUILayout.LabelField("질의");
        _query = EditorGUILayout.TextArea(_query, GUILayout.Height(52f));

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!Application.isPlaying || string.IsNullOrWhiteSpace(_query)))
        {
            if (GUILayout.Button("보내기 (라우터 + 스킬)", GUILayout.Height(30f)))
            {
                Send(_query);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("자주 쓰는 문장", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < Presets.Length; i++)
        {
            string preset = Presets[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("보내기", GUILayout.Width(60f)))
                {
                    if (Application.isPlaying)
                    {
                        Send(preset);
                    }
                    else
                    {
                        Debug.LogWarning("[MRQuerySender] Play 중이 아니다.");
                    }
                }
                if (GUILayout.Button(preset, EditorStyles.label))
                {
                    _query = preset;
                    Repaint();
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private static void Send(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        ApiVlRouterManager router = FindRouter();
        if (router == null)
        {
            Debug.LogError("[MRQuerySender] ApiVlRouterManager를 씬에서 찾지 못했다. Play 중인지 확인할 것.");
            return;
        }

        // chatIdx는 호출부가 올린다 (ChatHandler / STT 경로와 같은 규칙).
        if (GameManager.Instance != null)
        {
            GameManager.Instance.chatIdx += 1;
            GameManager.Instance.chatIdxRegenerateCount = 0;
        }

        // 이전 발화의 TTS가 남아 있으면 끊는다.
        if (TTSManager.Instance != null)
        {
            TTSManager.Instance.CancelTtsSession();
        }

        int chatIdx = GameManager.Instance != null ? GameManager.Instance.chatIdx : -1;
        Debug.Log($"[MRQuerySender] 전송: '{query}' (chatIdx={chatIdx})");
        router.ExecuteVlRouterRun(query);
    }

    private static ApiVlRouterManager FindRouter()
    {
        ApiVlRouterManager[] found = Resources.FindObjectsOfTypeAll<ApiVlRouterManager>();
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].gameObject != null && found[i].gameObject.scene.IsValid())
            {
                return found[i];
            }
        }
        return null;
    }
}
