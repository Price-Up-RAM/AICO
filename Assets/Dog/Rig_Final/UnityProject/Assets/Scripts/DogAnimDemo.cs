using UnityEngine;

/// <summary>
/// 쿠키 애니메이션 확인용 런타임 UI.
/// Canvas / 프리팹 배선 없이 OnGUI 로만 그린다 - 씬에 이 스크립트 하나만 있으면 동작한다.
/// 실제 제품에 넣을 때는 이 스크립트를 지우고 AICO 의 DogAnimationController 를 붙이면 된다.
/// (파라미터 이름이 동일하게 맞춰져 있다)
/// </summary>
public class DogAnimDemo : MonoBehaviour
{
    [Tooltip("비워두면 자식에서 Animator 를 자동으로 찾는다")]
    public Animator animator;

    [Tooltip("강아지 Y 회전 (기본 방향은 엉덩이가 카메라를 향한다)")]
    public float yaw = 100f;

    // bool 로 켜고 끄는 반복 상태
    static readonly string[] LOOPS = { "bWalk", "bWalk2" };
    static readonly string[] LOOP_DESC = {
        "Walk  - Latte 원본 78프레임 (앞뒤 정지구간 포함)",
        "Walk2 - 보행 1주기만 잘라낸 무한 루프 (권장)",
    };

    // 트리거로 1회 재생하는 상태
    static readonly string[] SHOTS = { "tDefault", "tJump", "tSit1", "tSit2",
                                       "tUnique1", "tUnique4", "tUnique5" };

    GUIStyle head, dim;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        ApplyYaw();
    }

    void ApplyYaw()
    {
        if (animator != null)
            animator.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void EnsureStyles()
    {
        if (head != null) return;
        head = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
        head.normal.textColor = Color.white;
        dim = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        dim.normal.textColor = new Color(0.75f, 0.78f, 0.85f);
        GUI.skin.button.fontSize = 15;
        GUI.skin.label.fontSize = 15;
        GUI.skin.toggle.fontSize = 15;
    }

    static bool Has(Animator a, string n)
    {
        foreach (var p in a.parameters) if (p.name == n) return true;
        return false;
    }

    void ClearLoops()
    {
        foreach (var q in LOOPS) if (Has(animator, q)) animator.SetBool(q, false);
    }

    void OnGUI()
    {
        EnsureStyles();
        GUILayout.BeginArea(new Rect(12, 12, 340, Screen.height - 24), GUI.skin.box);

        if (animator == null)
        {
            GUILayout.Label("Animator 를 찾을 수 없습니다.", head);
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label("반복 재생", head);
        for (int i = 0; i < LOOPS.Length; i++)
        {
            if (!Has(animator, LOOPS[i])) continue;
            bool cur = animator.GetBool(LOOPS[i]);
            bool nv = GUILayout.Toggle(cur, "  " + LOOPS[i], GUILayout.Height(26));
            GUILayout.Label("     " + LOOP_DESC[i], dim);
            if (nv != cur) { ClearLoops(); animator.SetBool(LOOPS[i], nv); }
        }

        GUILayout.Space(8);
        GUILayout.Label("1회 재생", head);
        foreach (var t in SHOTS)
        {
            if (!Has(animator, t)) continue;
            if (GUILayout.Button(t, GUILayout.Height(27))) { ClearLoops(); animator.SetTrigger(t); }
        }

        GUILayout.Space(10);
        GUILayout.Label("보기", head);
        GUILayout.Label("회전 Y = " + yaw.ToString("F0") + "°");
        float ny = GUILayout.HorizontalSlider(yaw, -180f, 180f);
        if (!Mathf.Approximately(ny, yaw)) { yaw = ny; ApplyYaw(); }

        GUILayout.Label("재생속도 = " + animator.speed.ToString("F2") + "x");
        animator.speed = GUILayout.HorizontalSlider(animator.speed, 0.1f, 2f);

        GUILayout.Space(8);
        var clips = animator.GetCurrentAnimatorClipInfo(0);
        string cn = (clips != null && clips.Length > 0 && clips[0].clip != null)
                    ? clips[0].clip.name.Replace("Bip001|cookie_", "") : "?";
        GUILayout.Label("현재 클립: " + cn, dim);

        GUILayout.EndArea();
    }
}
