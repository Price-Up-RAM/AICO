using System.Collections;
using UnityEngine;

// ApiAgentFunction 기능 테스트용 UI 클래스 (디버깅 목적)
public class ApiAgentFunctionTester : MonoBehaviour
{
    private string xInput = "500";
    private string yInput = "500";
    private string skillKeyInput = "test_skill";

    [Header("Click Effect")]
    [SerializeField] private ParticleSystem fx_click; // 클릭 이펙트
    [SerializeField] private Canvas canvas; // 좌표 변환용 Canvas
    
    private void Start()
    {
        // 테스트에 필요한 매니저들이 씬에 없으면 현재 GameObject에 부착합니다.
        if (FindObjectOfType<ApiAgentFunctionMouseAction>() == null)
        {
            gameObject.AddComponent<ApiAgentFunctionMouseAction>();
        }
        
        if (FindObjectOfType<ApiAgentFunctionProxyMouseAction>() == null)
        {
            gameObject.AddComponent<ApiAgentFunctionProxyMouseAction>();
        }
        
        if (FindObjectOfType<ApiAgentFunctionKeyboardAction>() == null)
        {
            gameObject.AddComponent<ApiAgentFunctionKeyboardAction>();
        }
        
        if (FindObjectOfType<ApiAgentFunctionScreenshotAction>() == null)
        {
            gameObject.AddComponent<ApiAgentFunctionScreenshotAction>();
        }
        
        if (FindObjectOfType<ApiAgentFunctionSkillManager>() == null)
        {
            gameObject.AddComponent<ApiAgentFunctionSkillManager>();
        }
        
        if (FindObjectOfType<ApiAgentFunctionSystemAction>() == null)
        {
            gameObject.AddComponent<ApiAgentFunctionSystemAction>();
        }
        
        if (FindObjectOfType<ApiAgentFunctionManager>() == null)
        {
            gameObject.AddComponent<ApiAgentFunctionManager>();
        }
        
        if (FindObjectOfType<ApiAgentFunctionSfx>() == null)
        {
            gameObject.AddComponent<ApiAgentFunctionSfx>();
        }
        
        if (FindObjectOfType<ApiAgentFunctionChatMode>() == null)
        {
            gameObject.AddComponent<ApiAgentFunctionChatMode>();
        }
        
        if (FindObjectOfType<ApiAgentFunctionAction>() == null)
        {
            gameObject.AddComponent<ApiAgentFunctionAction>();
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 600));
        GUILayout.Box("ApiAgentFunction Tester");
        
        GUILayout.Label("X 좌표:");
        xInput = GUILayout.TextField(xInput);
        
        GUILayout.Label("Y 좌표:");
        yInput = GUILayout.TextField(yInput);
        
        int x = 0;
        int y = 0;
        int.TryParse(xInput, out x);
        int.TryParse(yInput, out y);

        if (GUILayout.Button("Physical Click"))
        {
            // 물리 클릭 후 이펙트 재생
            System.Collections.Generic.Dictionary<string, object> clickParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "winX", x },
                { "winY", y },
                { "isMouseMove", false }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("physical_click", clickParams, (success, message) => 
            {
                Debug.Log($"[Tester] Physical Click 결과: {success}");
            });
            PlayClickEffect(x, y);
        }
        
        if (GUILayout.Button("Proxy Click"))
        {
            // 프록시 클릭 후 이펙트 재생
            System.Collections.Generic.Dictionary<string, object> clickParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "winX", x },
                { "winY", y }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("proxy_click", clickParams, (success, message) => 
            {
                Debug.Log($"[Tester] Proxy Click 결과: {success}");
            });
            PlayClickEffect(x, y);
        }

        if (GUILayout.Button("Type 'HELLO'"))
        {
            // 현재 포커스된 창에 바로 타이핑
            System.Collections.Generic.Dictionary<string, object> keyboardParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "text", "HELLO" }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("type_text", keyboardParams, (success, message) => 
            {
                Debug.Log($"[Tester] Type Text 결과: {success}");
            });
        }

        if (GUILayout.Button("메모장 열고 HELLO 입력"))
        {
            // 메모장 실행 후 포커스 → 타이핑 순서로 실행
            StartCoroutine(OpenAndTypeCoroutine("notepad.exe"));
        }

        if (GUILayout.Button("워드패드 열고 HELLO 입력"))
        {
            // 워드패드 실행 후 포커스 → 타이핑 순서로 실행
            StartCoroutine(OpenAndTypeCoroutine("write.exe"));
        }

        if (GUILayout.Button("Capture Screenshot"))
        {
            System.Collections.Generic.Dictionary<string, object> ssParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "path", Application.persistentDataPath + "/test_screenshot.png" }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("capture_screenshot", ssParams, (success, message) => 
            {
                Debug.Log($"[Tester] Capture Screenshot 결과: {success}");
            });
        }

        GUILayout.Space(20);
        GUILayout.Label("Skill Key:");
        skillKeyInput = GUILayout.TextField(skillKeyInput);

        if (GUILayout.Button("Save Dummy Skill"))
        {
            System.Collections.Generic.Dictionary<string, object> skillParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "key", skillKeyInput },
                { "frontmatter", "name: Dummy Skill\ndescription: Test" },
                { "body", "This is a dummy skill body." }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("save_skill", skillParams, (success, message) => 
            {
                Debug.Log($"[Tester] Save Dummy Skill 결과: {success}");
            });
        }

        if (GUILayout.Button("Read Skill"))
        {
            System.Collections.Generic.Dictionary<string, object> readParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "key", skillKeyInput }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("read_skill_body", readParams, (success, message) => 
            {
                Debug.Log($"[Tester] Read Skill: {message}");
            });
        }

        GUILayout.Space(20);

        if (GUILayout.Button("Read Clipboard"))
        {
            ApiAgentFunctionManager.Instance.ExecuteAction("read_clipboard", null, (success, message) => 
            {
                Debug.Log($"[Tester] Clipboard Read: {message}");
            });
        }

        if (GUILayout.Button("Write Clipboard"))
        {
            System.Collections.Generic.Dictionary<string, object> clipParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "text", "HELLO JARVIS CLIPBOARD TEST" }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("write_clipboard", clipParams, (success, message) => 
            {
                Debug.Log($"[Tester] Clipboard Write 결과: {success}");
            });
        }

        if (GUILayout.Button("Play Arona Voice"))
        {
            System.Collections.Generic.Dictionary<string, object> sfxParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "path", "Sound/arona/Arona_Academy_Talk_4.ogg" }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("play_sfx", sfxParams, (success, message) => 
            {
                Debug.Log($"[Tester] Play Arona Voice 결과: {success}, 메시지: {message}");
            });
        }

        GUILayout.Space(20);

        if (GUILayout.Button("CRUD Test (Top Level)"))
        {
            System.Collections.Generic.Dictionary<string, object> saveParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "path", "test_data.txt" },
                { "content", "Top Level Data" }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("save_data", saveParams, (success, msg) => 
            {
                System.Collections.Generic.Dictionary<string, object> readParams = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "path", "test_data.txt" }
                };
                ApiAgentFunctionManager.Instance.ExecuteAction("read_data", readParams, (readSuccess, readMsg) => 
                {
                    Debug.Log($"[Tester] Read Top Level: {readMsg}");
                    System.Collections.Generic.Dictionary<string, object> delParams = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "path", "test_data.txt" }
                    };
                    ApiAgentFunctionManager.Instance.ExecuteAction("delete_data", delParams, null);
                });
            });
        }

        if (GUILayout.Button("CRUD Test (Skill Folder)"))
        {
            System.Collections.Generic.Dictionary<string, object> saveParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "path", "skill/test_skill_data.txt" },
                { "content", "Skill Folder Data" }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("save_data", saveParams, (success, msg) => 
            {
                System.Collections.Generic.Dictionary<string, object> readParams = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "path", "skill/test_skill_data.txt" }
                };
                ApiAgentFunctionManager.Instance.ExecuteAction("read_data", readParams, (readSuccess, readMsg) => 
                {
                    Debug.Log($"[Tester] Read Skill Folder: {readMsg}");
                    System.Collections.Generic.Dictionary<string, object> delParams = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "path", "skill/test_skill_data.txt" }
                    };
                    ApiAgentFunctionManager.Instance.ExecuteAction("delete_data", delParams, null);
                });
            });
        }

        GUILayout.Space(20);
        GUILayout.Label("Chat Mode 제어:");

        if (GUILayout.Button("Get Current Chat Mode"))
        {
            ApiAgentFunctionManager.Instance.ExecuteAction("get_chat_mode", null, (success, msg) => 
            {
                Debug.Log($"[Tester] 현재 대화 모드: {msg}");
            });
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Chat"))
        {
            System.Collections.Generic.Dictionary<string, object> modeParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "mode", "chat" }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("set_chat_mode", modeParams, null);
        }
        if (GUILayout.Button("Set Aropla"))
        {
            System.Collections.Generic.Dictionary<string, object> modeParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "mode", "aropla" }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("set_chat_mode", modeParams, null);
        }
        if (GUILayout.Button("Set Operator"))
        {
            System.Collections.Generic.Dictionary<string, object> modeParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "mode", "operator" }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("set_chat_mode", modeParams, null);
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Toggle Operator Mode"))
        {
            System.Collections.Generic.Dictionary<string, object> modeParams = new System.Collections.Generic.Dictionary<string, object>
            {
                { "mode", "operator" }
            };
            ApiAgentFunctionManager.Instance.ExecuteAction("toggle_chat_mode", modeParams, null);
        }

        GUILayout.Space(20);
        GUILayout.Label("캐릭터 액션 제어:");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Dance"))
        {
            ApiAgentFunctionManager.Instance.ExecuteAction("character_dance", null, null);
        }
        if (GUILayout.Button("Walk Left"))
        {
            ApiAgentFunctionManager.Instance.ExecuteAction("character_walk_left", null, null);
        }
        if (GUILayout.Button("Walk Right"))
        {
            ApiAgentFunctionManager.Instance.ExecuteAction("character_walk_right", null, null);
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Stop Character Action"))
        {
            ApiAgentFunctionManager.Instance.ExecuteAction("character_stop", null, null);
        }
        
        GUILayout.EndArea();
    }

    // 프로세스를 실행하고 핸들이 생길 때까지 폴링 후 포커스 → 타이핑 (언어 무관)
    private IEnumerator OpenAndTypeCoroutine(string processName)
    {
        // 프로세스 실행
        int pid = -1;
        bool hasRunResult = false;
        System.Collections.Generic.Dictionary<string, object> runParams = new System.Collections.Generic.Dictionary<string, object>
        {
            { "fileName", processName }
        };
        
        ApiAgentFunctionManager.Instance.ExecuteAction("run_process", runParams, (success, result) => 
        {
            if (success)
            {
                int.TryParse(result, out pid);
            }
            hasRunResult = true;
        });

        yield return new WaitUntil(() => hasRunResult);

        if (pid == -1)
        {
            Debug.LogError($"[Tester] 프로세스 실행 실패: {processName}");
            yield break;
        }

        Debug.Log($"[Tester] 프로세스 시작됨 PID={pid}, MainWindowHandle 대기 중...");

        System.Diagnostics.Process proc = null;
        try
        {
            proc = System.Diagnostics.Process.GetProcessById(pid);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Tester] 프로세스 획득 실패: {e.Message}");
            yield break;
        }

        // 1초 간격으로 최대 10회 MainWindowHandle 확인
        int maxTry = 10;
        bool handleFound = false;

        for (int i = 0; i < maxTry; i++)
        {
            yield return new WaitForSeconds(1f);

            proc.Refresh();

            if (proc.MainWindowHandle != System.IntPtr.Zero)
            {
                // 핸들 확보 성공
                Debug.Log($"[Tester] MainWindowHandle 확보 성공 (시도 {i + 1}/{maxTry}): {proc.MainWindowHandle}");
                handleFound = true;
                break;
            }
            else
            {
                // 아직 핸들 없음
                Debug.LogWarning($"[Tester] MainWindowHandle 아직 없음 (시도 {i + 1}/{maxTry})");
            }
        }

        if (!handleFound)
        {
            // 최대 시도 초과 시 중단
            Debug.LogError($"[Tester] MainWindowHandle을 {maxTry}초 내에 얻지 못함. 포커스 중단.");
            yield break;
        }

        // 핸들 기반 포커스
        bool hasFocusResult = false;
        bool focused = false;
        System.Collections.Generic.Dictionary<string, object> focusParams = new System.Collections.Generic.Dictionary<string, object>
        {
            { "pid", pid }
        };

        ApiAgentFunctionManager.Instance.ExecuteAction("focus_process", focusParams, (success, result) => 
        {
            focused = success;
            hasFocusResult = true;
        });

        yield return new WaitUntil(() => hasFocusResult);

        if (!focused)
        {
            // 포커스 실패 시 1초 추가 대기 후 재시도
            Debug.LogWarning("[Tester] 첫 포커스 실패, 1초 후 재시도...");
            yield return new WaitForSeconds(1f);

            hasFocusResult = false;
            ApiAgentFunctionManager.Instance.ExecuteAction("focus_process", focusParams, (success, result) => 
            {
                focused = success;
                hasFocusResult = true;
            });

            yield return new WaitUntil(() => hasFocusResult);

            if (!focused)
            {
                Debug.LogError("[Tester] 포커스 재시도도 실패. 타이핑 중단.");
                yield break;
            }
        }

        Debug.Log("[Tester] 포커스 완료. 0.3초 후 타이핑 시작...");
        yield return new WaitForSeconds(0.3f);

        System.Collections.Generic.Dictionary<string, object> keyboardParams = new System.Collections.Generic.Dictionary<string, object>
        {
            { "text", "HELLO" }
        };
        ApiAgentFunctionManager.Instance.ExecuteAction("type_text", keyboardParams, null);
        Debug.Log("[Tester] 타이핑 완료.");
    }

    // Windows 스크린 좌표를 기반으로 클릭 이펙트 재생
    private void PlayClickEffect(int winX, int winY)
    {
        if (fx_click == null)
        {
            // fx_click이 Inspector에 할당되지 않은 경우 경고
            Debug.LogWarning("[Tester] fx_click이 할당되지 않음. Inspector에서 ParticleSystem을 연결해주세요.");
            return;
        }

        // Windows 좌표는 좌상단 원점 → Unity 스크린 좌표는 좌하단 원점이므로 Y 반전
        float unityScreenX = winX;
        float unityScreenY = Screen.height - winY;

        if (canvas != null)
        {
            // Canvas 기반 좌표 변환 (RectTransform 이용)
            RectTransform canvasRect = canvas.transform as RectTransform;
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                new Vector2(unityScreenX, unityScreenY),
                canvas.worldCamera,
                out localPoint
            );

            // 로컬 좌표를 월드 좌표로 변환
            Vector3 worldPos = canvas.transform.TransformPoint(new Vector3(localPoint.x, localPoint.y, 0));
            fx_click.transform.position = worldPos;
            fx_click.Play();
            Debug.Log($"[Tester] 클릭 이펙트 재생 (Canvas 기반): Win({winX}, {winY}) → World({worldPos.x:F1}, {worldPos.y:F1})");
        }
        else
        {
            // Canvas가 없을 때는 스크린→월드 직접 변환 (Camera.main 사용)
            Vector3 screenPos = new Vector3(unityScreenX, unityScreenY, 1f);
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            fx_click.transform.position = worldPos;
            fx_click.Play();
            Debug.Log($"[Tester] 클릭 이펙트 재생 (Camera 기반): Win({winX}, {winY}) → World({worldPos.x:F1}, {worldPos.y:F1})");
        }
    }
}
