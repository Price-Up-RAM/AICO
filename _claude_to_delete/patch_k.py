# -*- coding: utf-8 -*-
# 키보드 — MRTMPVirtualKeyboardBinder 자동 등록 + 진단
import sys, os

ROOT = os.path.expanduser("~/mnt/UnityProject--AICO/Assets/Scripts")
ok = True


def eol_of(p):
    b = open(p, "rb").read()
    crlf = b.count(b"\r\n")
    if crlf > (b.count(b"\n") - crlf):
        return "\r\n"
    return "\n"


def patch(path, old, new, label):
    global ok
    p = os.path.join(ROOT, path)
    e = eol_of(p)
    data = open(p, "rb").read()
    o = old.replace("\n", e).encode("utf-8")
    n = new.replace("\n", e).encode("utf-8")
    c = data.count(o)
    if c != 1:
        print("FAIL %s : 앵커 %d회 매치" % (label, c))
        ok = False
        return
    open(p, "wb").write(data.replace(o, n))
    print("OK   %s" % label)


# ===== K1. 자동 등록 + 시작 진단 =====
patch("MR/MRTMPVirtualKeyboardBinder.cs",
"""    private void Awake()
    {
        // 인스펙터에 등록된 모든 필드 자동 후킹
        foreach (var f in fields)
        {
            if (f != null) HookField(f);
        }
    }""",
"""    [Header("자동 등록")]
    [Tooltip("켜면 fields가 비어 있을 때 씬 전체(비활성 포함)에서 TMP_InputField를 찾아 등록한다. " +
             "수동 배선 없이 동작시키기 위한 옵션이다.")]
    [SerializeField] private bool autoRegisterAllFields = true;

    [Tooltip("런타임에 생성되는 InputField를 주기적으로 다시 찾는 간격(초). 0이면 재탐색하지 않는다.")]
    [SerializeField] private float rescanInterval = 3f;

    private float _nextRescanTime;

    private void Awake()
    {
        // 인스펙터에 등록된 것이 없으면 씬에서 자동으로 찾는다.
        // 이 컴포넌트가 씬에 아예 없어서 키보드가 안 떴던 것이 2026-08-25에 실측됐고,
        // 붙이더라도 fields 수동 배선을 잊으면 같은 증상이라 자동 등록을 기본으로 뒀다.
        if (autoRegisterAllFields && (fields == null || fields.Count == 0))
        {
            RegisterAllFieldsInScene();
        }

        // 인스펙터/자동 등록된 모든 필드 후킹
        foreach (var f in fields)
        {
            if (f != null) HookField(f);
        }

        Debug.Log($"[MRKeyboardBinder] 초기화 | 등록 {fields.Count}개 | TouchScreenKeyboard.isSupported={TouchScreenKeyboard.isSupported} | 자동등록={autoRegisterAllFields}");
        if (!TouchScreenKeyboard.isSupported)
        {
            Debug.Log("[MRKeyboardBinder] 이 플랫폼은 시스템 키보드를 지원하지 않는다(Editor 등). 물리 키보드로 InputField에 직접 입력해야 한다.");
        }
    }

    // 씬 전체(비활성 포함)에서 TMP_InputField를 찾아 등록한다.
    private void RegisterAllFieldsInScene()
    {
        TMP_InputField[] found = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            TMP_InputField f = found[i];
            if (f == null || fields.Contains(f))
            {
                continue;
            }
            fields.Add(f);
            Debug.Log($"[MRKeyboardBinder] 자동 등록: '{GetFieldPath(f)}'");
        }
    }

    private static string GetFieldPath(TMP_InputField f)
    {
        string path = f.gameObject.name;
        Transform t = f.transform.parent;
        int depth = 0;
        while (t != null && depth < 3)
        {
            path = t.name + "/" + path;
            t = t.parent;
            depth++;
        }
        return path;
    }""",
"K1. 자동 등록 + 시작 진단")

# ===== K2. 선택 진단 =====
patch("MR/MRTMPVirtualKeyboardBinder.cs",
"""        Debug.Log($"[MRKeyboardBinder] 시스템 키보드 호출: {field.name}");
        KeyboardOpened?.Invoke(field);""",
"""        // 호출했다는 것과 실제로 떴다는 것은 다른 사실이다 (Kickoff Guide 4-58).
        string status = "(null)";
        if (_keyboard != null)
        {
            status = _keyboard.status.ToString();
        }
        Debug.Log($"[MRKeyboardBinder] 시스템 키보드 호출: '{field.name}' | keyboard={( _keyboard == null ? "null" : "생성됨")} status={status} isSupported={TouchScreenKeyboard.isSupported} active={TouchScreenKeyboard.visible}");
        KeyboardOpened?.Invoke(field);""",
"K2. 키보드 호출 진단")

# ===== K3. 런타임 재탐색 =====
patch("MR/MRTMPVirtualKeyboardBinder.cs",
"""    private void Update()
    {
        if (_keyboard == null || _activeField == null) return;""",
"""    private void Update()
    {
        // 런타임에 생성된 InputField(패널이 나중에 열리는 경우 등)를 주기적으로 잡는다.
        if (autoRegisterAllFields && rescanInterval > 0f && Time.unscaledTime >= _nextRescanTime)
        {
            _nextRescanTime = Time.unscaledTime + rescanInterval;
            int before = fields.Count;
            TMP_InputField[] found = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                TMP_InputField f = found[i];
                if (f == null || fields.Contains(f))
                {
                    continue;
                }
                fields.Add(f);
                HookField(f);
                Debug.Log($"[MRKeyboardBinder] 런타임 등록: '{GetFieldPath(f)}'");
            }
            if (fields.Count != before)
            {
                Debug.Log($"[MRKeyboardBinder] 재탐색: {before}개 → {fields.Count}개");
            }
        }

        if (_keyboard == null || _activeField == null) return;""",
"K3. 런타임 재탐색")

sys.exit(0 if ok else 1)
