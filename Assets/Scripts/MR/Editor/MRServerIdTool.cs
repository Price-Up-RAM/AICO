using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// 서버 ID를 바꾼다 (Tools → MR → 서버 ID 바꾸기).
//
// 왜 필요한가 (2026-08-26 실측):
//   MR 씬에서는 설정 창의 InputField에 타이핑할 방법이 없다.
//   TouchScreenKeyboard는 안드로이드 런타임 전용이라 Editor + Quest Link에서 뜨지 않고,
//   STT는 한 언어만 인식해서 'arona655' 같은 영숫자 ID를 부를 수 없다.
//
//   그리고 SettingManager.cs의 settings.server_id = "arona655" 는 기본값 블록이라
//   저장 파일이 이미 있으면 절대 안 먹는다 (Kickoff Guide 4-60 계열).
//   실제로 2026-08-26에 실기가 server_id="temp"로 남아 test-url2.com을 두드리고 있었고,
//   라우터·TTS 통신이 통째로 죽어 있었다.
//
//   기존 ForceServerSettings는 arona655로 하드코딩돼 있어 서버가 바뀌면 코드를 고쳐야 했다.
//   이 창은 값을 직접 넣을 수 있고, 바꾸기 전에 현재값을 보여준다 (7-1 C).
//
// 한계: 이 도구는 에디터가 쓰는 파일만 고친다.
//   퀘스트 실기는 /storage/emulated/0/Android/data/<pkg>/files/config/settings.json 을 따로 갖는다.
//   그쪽은 창 아래에 나오는 adb 명령으로 밀어 넣어야 한다 (4-67).
public class MRServerIdTool : EditorWindow
{
    // ServerManager의 판정 규칙을 그대로 옮겼다.
    // TryNormalizeServerId / IsLegacyPublishedServerId 가 private이라 호출할 수 없어서다.
    // 원본이 바뀌면 여기도 같이 고칠 것 — ServerManager.cs 338~360행.
    private const string TunnelDomain = "60000123.xyz";
    private const int RemoteServerTypeIdx = 10;   // Dropdown[4] → Server

    private static readonly string[] Presets = { "arona655", "arona614sd", "temp" };
    private const string AndroidPackage = "com.UnityTechnologies.com.unity.template.urpblank";

    private string _newId = "";
    private string _currentId = "(아직 안 읽음)";
    private int _currentTypeIdx = -1;
    private bool _fileExists;
    private string _filePath = "";

    [MenuItem("Tools/MR/서버 ID 바꾸기")]
    public static void Open()
    {
        MRServerIdTool w = GetWindow<MRServerIdTool>("MR 서버 ID");
        w.minSize = new Vector2(430f, 380f);
        w.ReadCurrent();
        w.Show();
    }

    private void OnFocus()
    {
        ReadCurrent();
    }

    // =========================================================
    // 현재값 읽기
    // =========================================================
    private void ReadCurrent()
    {
        _filePath = Path.Combine(Application.persistentDataPath, "config", "settings.json");
        _fileExists = File.Exists(_filePath);

        if (!_fileExists)
        {
            _currentId = "(파일 없음)";
            _currentTypeIdx = -1;
            return;
        }

        string json = File.ReadAllText(_filePath);

        Match idMatch = Regex.Match(json, "\"server_id\"\\s*:\\s*\"(.*?)\"");
        if (idMatch.Success)
        {
            _currentId = idMatch.Groups[1].Value;
        }
        else
        {
            _currentId = "(server_id 키 없음)";
        }

        Match typeMatch = Regex.Match(json, "\"server_type_idx\"\\s*:\\s*(\\d+)");
        if (typeMatch.Success)
        {
            _currentTypeIdx = int.Parse(typeMatch.Groups[1].Value);
        }
        else
        {
            _currentTypeIdx = -1;
        }

        if (string.IsNullOrEmpty(_newId))
        {
            _newId = _currentId;
        }
    }

    // =========================================================
    // 화면
    // =========================================================
    private void OnGUI()
    {
        EditorGUILayout.LabelField("서버 ID 변경", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "MR 씬에서는 설정 창에 타이핑할 방법이 없어(시스템 키보드 미지원 + STT 단일 언어) " +
            "이 창으로 바꾼다. 에디터가 쓰는 저장 파일을 직접 고친다.",
            MessageType.Info);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("현재 상태", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.LabelField("파일", _filePath);
            if (!_fileExists)
            {
                EditorGUILayout.HelpBox(
                    "settings.json이 아직 없다. 한 번 Play해서 껐다가 다시 눌러야 파일이 생긴다.",
                    MessageType.Warning);
            }
            EditorGUILayout.LabelField("server_id", _currentId);
            EditorGUILayout.LabelField("server_type_idx", DescribeTypeIdx(_currentTypeIdx));
            EditorGUILayout.LabelField("접속 예상 URL", DescribeUrl(_currentId, _currentTypeIdx));
        }

        if (GUILayout.Button("현재값 다시 읽기"))
        {
            ReadCurrent();
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("새 server_id", EditorStyles.boldLabel);
        _newId = EditorGUILayout.TextField(_newId);

        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < Presets.Length; i++)
            {
                if (GUILayout.Button(Presets[i]))
                {
                    _newId = Presets[i];
                    GUI.FocusControl(null);
                }
            }
        }

        string reason;
        bool valid = Validate(_newId, out reason);
        if (!valid)
        {
            EditorGUILayout.HelpBox(reason, MessageType.Error);
        }
        else
        {
            EditorGUILayout.LabelField("바뀐 뒤 URL", DescribeUrl(Normalize(_newId), RemoteServerTypeIdx));
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!valid || !_fileExists))
        {
            if (GUILayout.Button("적용 (server_type_idx도 10으로)", GUILayout.Height(30f)))
            {
                Apply();
            }
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play 중이다. 적용하면 실행 중인 SettingManager에도 바로 반영한다 " +
                "(ServerManager가 주기적으로 baseUrl을 다시 잡으므로 재시작 없이 붙는다).",
                MessageType.Info);
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("퀘스트 실기는 별도 파일이다", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "이 창은 에디터가 쓰는 파일만 고친다. 실기에 반영하려면 아래 명령을 PowerShell에서 실행할 것. " +
            "앱이 켜져 있으면 종료할 때 덮어쓰므로 먼저 force-stop 한다.",
            MessageType.Warning);

        string adb = BuildAdbCommand();
        EditorGUILayout.SelectableLabel(adb, EditorStyles.textArea, GUILayout.Height(76f));
        if (GUILayout.Button("adb 명령 복사"))
        {
            EditorGUIUtility.systemCopyBuffer = adb;
            Debug.Log("[MRServerId] adb 명령을 클립보드에 복사했다.");
        }
    }

    // =========================================================
    // 적용
    // =========================================================
    private void Apply()
    {
        string normalized = Normalize(_newId);

        if (!File.Exists(_filePath))
        {
            Debug.LogError($"[MRServerId] 파일이 사라졌다: {_filePath}");
            ReadCurrent();
            return;
        }

        string before = File.ReadAllText(_filePath);
        string after = Regex.Replace(before, "\"server_id\"\\s*:\\s*\".*?\"", "\"server_id\": \"" + normalized + "\"");
        after = Regex.Replace(after, "\"server_type_idx\"\\s*:\\s*\\d+", "\"server_type_idx\": " + RemoteServerTypeIdx);

        if (after == before)
        {
            Debug.LogWarning($"[MRServerId] 파일 내용이 그대로다. server_id/server_type_idx 키를 못 찾았을 수 있다: {_filePath}");
        }
        else
        {
            File.WriteAllText(_filePath, after);
        }

        // '썼다'와 '그렇게 읽힌다'는 다른 사실이다 (Kickoff Guide 4-58). 다시 읽어서 확인한다.
        string oldId = _currentId;
        int oldType = _currentTypeIdx;
        ReadCurrent();

        Debug.Log($"[MRServerId] 저장 파일 | server_id 현재값='{oldId}' → 새값='{_currentId}' | " +
                  $"server_type_idx 현재값={oldType} → 새값={_currentTypeIdx} | {_filePath}");

        if (_currentId != normalized)
        {
            Debug.LogError($"[MRServerId] 다시 읽으니 '{_currentId}'다. 기대값 '{normalized}'과 다르다 — 적용 실패.");
            return;
        }

        // Play 중이면 실행 중인 설정도 같이 바꾼다. 안 그러면 껐다 켤 때까지 옛 주소로 간다.
        if (!Application.isPlaying)
        {
            Debug.Log("[MRServerId] Play 중이 아니다. 다음 실행부터 적용된다.");
            return;
        }

        if (SettingManager.Instance == null)
        {
            Debug.LogWarning("[MRServerId] SettingManager.Instance가 null이라 실행 중 설정은 못 바꿨다. 재시작하면 적용된다.");
            return;
        }

        // SetServerID가 내부에서 SaveSettings까지 한다.
        SettingManager.Instance.SetServerID(normalized);
        Debug.Log($"[MRServerId] 실행 중 설정에도 반영 | settings.server_id='{SettingManager.Instance.settings.server_id}' " +
                  "| ServerManager가 다음 갱신 주기에 baseUrl을 다시 잡는다");
    }

    // =========================================================
    // 검증 — ServerManager.TryNormalizeServerId 와 같은 규칙
    // =========================================================
    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }
        return value.Trim().ToLowerInvariant();
    }

    private static bool IsLegacy(string serverId)
    {
        if (string.Equals(serverId, "temp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return string.Equals(serverId, "dev_voice", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLowerAlphaNumeric(char c)
    {
        if (c >= 'a' && c <= 'z')
        {
            return true;
        }
        return c >= '0' && c <= '9';
    }

    private static bool Validate(string raw, out string reason)
    {
        string id = Normalize(raw);
        reason = "";

        if (id.Length == 0)
        {
            reason = "비어 있다.";
            return false;
        }

        // temp / dev_voice 는 터널 URL을 안 쓰고 ngrok 게시 URL로 가는 예전 경로다.
        // 규칙 검사를 통과하지 못하지만 유효한 값이라 따로 허용한다.
        if (IsLegacy(id))
        {
            return true;
        }

        if (id.Length < 3 || id.Length > 32)
        {
            reason = $"길이가 {id.Length}자다. 3~32자여야 한다.";
            return false;
        }

        if (!IsLowerAlphaNumeric(id[0]) || !IsLowerAlphaNumeric(id[id.Length - 1]))
        {
            reason = "첫 글자와 끝 글자는 영소문자 또는 숫자여야 한다.";
            return false;
        }

        for (int i = 1; i < id.Length - 1; i++)
        {
            char c = id[i];
            if (IsLowerAlphaNumeric(c))
            {
                continue;
            }
            if (c == '-')
            {
                continue;
            }
            reason = $"{i + 1}번째 글자 '{c}'는 쓸 수 없다. 영소문자·숫자·하이픈(-)만 된다.";
            return false;
        }

        return true;
    }

    // =========================================================
    // 표시용
    // =========================================================
    private static string DescribeTypeIdx(int idx)
    {
        if (idx < 0)
        {
            return "(없음)";
        }
        if (idx == RemoteServerTypeIdx)
        {
            return $"{idx} (Server — server_id로 접속)";
        }
        return $"{idx} ⚠ Server(10)가 아니다. 적용하면 10으로 바꾼다";
    }

    private static string DescribeUrl(string id, int typeIdx)
    {
        if (string.IsNullOrEmpty(id) || id.StartsWith("("))
        {
            return "(알 수 없음)";
        }
        if (typeIdx != RemoteServerTypeIdx)
        {
            return "(server_type_idx가 10이 아니라 다른 경로로 간다)";
        }
        if (IsLegacy(id))
        {
            return "ngrok 게시 URL (터널 도메인을 쓰지 않는 예전 경로)";
        }
        return $"https://{id}.{TunnelDomain}";
    }

    private string BuildAdbCommand()
    {
        string adbPath = "C:\\Program Files\\Unity\\Hub\\Editor\\6000.3.15f1\\Editor\\Data\\PlaybackEngines\\AndroidPlayer\\SDK\\platform-tools\\adb.exe";
        string devicePath = "/storage/emulated/0/Android/data/" + AndroidPackage + "/files/config/settings.json";

        return "$adb = \"" + adbPath + "\"\n" +
               "& $adb shell am force-stop " + AndroidPackage + "\n" +
               "& $adb push \"" + _filePath + "\" \"" + devicePath + "\"\n" +
               "& $adb shell cat \"" + devicePath + "\" | Select-String server_id";
    }
}
