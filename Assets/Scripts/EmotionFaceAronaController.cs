using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EmotionFaceAronaController : EmotionFaceController
{
    public GameObject faceNormal, faceRelax, faceListen, faceWink;
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private Coroutine talkCoroutine;
    private bool lastMouthState = false;  // update에서 변경될때만 처리되게 flag 관리
    public string charType = "";  // 캐릭터타입을 줘서 소환시 + 분기에 사용 : ""(Sub), Operator, Main

    // 자동 눈 깜빡임: FaceChange/FaceNormalBlendShape가 기록하는 현재 상태로 허용 여부 판단
    [Header("눈 깜빡임 설정")]
    public float blinkIntervalMin = 2f;          // 다음 깜빡임까지 대기 시간 최소(초)
    public float blinkIntervalMax = 6f;          // 다음 깜빡임까지 대기 시간 최대(초)
    public float blinkCloseTime = 0.07f;         // 눈 감는 데 걸리는 시간(초)
    public float blinkOpenTime = 0.1f;           // 눈 뜨는 데 걸리는 시간(초)
    private string currentFace = "normal";
    private string currentBlendType = "idle";
    private float lastFaceStateChangeTime = 0f;  // 상태 전환 직후 깜빡임 유예용
    private int[] blinkShapeIndices;             // 눈 감기 셰이프 인덱스 (LateUpdate에서 1회 탐색)
    private bool blinkShapeSearchDone = false;
    private float nextBlinkTime = 0f;
    private float blinkPhase = -1f;              // 0 이상이면 깜빡임 진행 중(경과 시간)
    private bool blinkClosedShown = false;       // 저FPS에서도 완전 감김 1프레임 보장용

    void Start()
    {
        // 기본 얼굴 설정
        FaceChange("normal");
        skinnedMeshRenderer = faceNormal.GetComponent<SkinnedMeshRenderer>();
    }

    void Update()
    {
        bool current = false;

        // Aropla 모드일 때는 IsSpeaking(this.gameObject) 체크
        if (ChatModeManager.Instance.IsAroplaMode())
        {
            current = StatusManager.Instance.IsSpeaking(this.gameObject);
        }
        else if (charType == "Operator")
        {
            current = StatusManager.Instance.IsAnsweringPortrait;
        }
        else if (charType == "Main")
        {
            current = StatusManager.Instance.isMouthActive;
        }
        // Sub 또는 그 외: current = false (입 모션 없음)

        if (current != lastMouthState)
        {
            lastMouthState = current;

            if (current)
            {
                if (talkCoroutine == null)
                    talkCoroutine = StartCoroutine(TalkAnimation());
            }
            else
            {
                if (talkCoroutine != null)
                {
                    StopCoroutine(talkCoroutine);
                    talkCoroutine = null;
                }
            }
        }
    }

    private List<string> faceEmotion = new List<string> { "normal", "relax", "listen", "><" };
    private List<string> animationStates = new List<string> { "idle", "talk", "slant", "surprise", "confused", "star", "default" };
    private List<string> animationList = new List<string>   // Test(NextAnimation)용
    {
        "normal", "relax", "listen", "><",
        "idle", "talk", "slant", "surprise", "confused", "star", "default"
    };

    // 얼굴 감정 변경 통합 함수
    public override void ShowEmotion(string emotion)
    {
        Debug.Log("Show Emotion Face : " + emotion);
        if (faceEmotion.Contains(emotion))
        {
            FaceChange(emotion);
        }
        else
        {
            FaceNormalBlendShape(emotion);
        }
    }

    // listen등의 행동시 표정 변환
    public override void ShowEmotionFromAction(string action)
    {
        string selectedAnimation = "";

        switch (action.ToLower())
        {
            case "listen":
                {
                    float rand = Random.value;
                    if (rand < 0.8f)
                        selectedAnimation = "relax";
                    else
                        selectedAnimation = "listen";
                }
                break;
            default:
                {
                    selectedAnimation = "default";
                }
                break;
        }

        ShowEmotion(selectedAnimation);
        Debug.Log($"ARONA : [Action Input] {action} → [Animation] {selectedAnimation}");
    }

    // joy, anger, confusion, sadness, surprise, neutral을 각각 표정변환
    public override void ShowEmotionFromEmotion(string emotion)
    {
        string selectedAnimation = "";

        switch (emotion.ToLower())
        {
            case "joy":
                {
                    float rand = Random.value;
                    if (rand < 0.5f)
                        selectedAnimation = "><";
                    else
                        selectedAnimation = "star";
                }
                break;
            case "anger":
                selectedAnimation = "slant";
                break;
            case "confusion":
                selectedAnimation = "confused";
                break;
            case "sadness":
                selectedAnimation = "listen";
                break;
            case "surprise":
                {
                    float rand = Random.value;
                    if (rand < 0.8f)
                        selectedAnimation = "surprise";
                    else
                        selectedAnimation = "star";
                }
                break;
            case "neutral":
            default:
                {
                    selectedAnimation = "default";
                }
                break;
        }

        ShowEmotion(selectedAnimation);
        Debug.Log($"[Emotion Input] {emotion} → [Animation] {selectedAnimation}");
    }

    // Test용 코드
    private int currentAnimationIndex = 0;
    public override void NextAnimation()
    {
        currentAnimationIndex = (currentAnimationIndex + 1) % animationList.Count;
        ShowEmotion(animationList[currentAnimationIndex]);
    }

    // gameObject용 얼굴 바꾸기
    public void FaceChange(string faceName = "normal")
    {
        faceNormal.SetActive(faceName == "normal");
        faceRelax.SetActive(faceName == "relax");
        faceListen.SetActive(faceName == "listen");
        faceWink.SetActive(faceName == "><");
        if (currentFace != faceName)
        {
            currentFace = faceName;
            lastFaceStateChangeTime = Time.time;
        }
    }

    // normalFace의 blendType으로 바꾸기
    public void FaceNormalBlendShape(string blendType)
    {
        if (skinnedMeshRenderer == null) return;

        if (currentBlendType != blendType)
        {
            currentBlendType = blendType;
            lastFaceStateChangeTime = Time.time;
        }

        // 항상 faceNormal을 활성화해야 함
        FaceChange("normal");
        ResetBlendShapes();

        switch (blendType)
        {
            case "idle":
                // reset 후 작업 없음
                break;
            case "slant":
                skinnedMeshRenderer.SetBlendShapeWeight(GetBlendShapeIndex("eyes-slant"), 100f);
                break;
            case "surprise":
                skinnedMeshRenderer.SetBlendShapeWeight(GetBlendShapeIndex("eyes-vanish"), 100f);
                skinnedMeshRenderer.SetBlendShapeWeight(GetBlendShapeIndex("eyes-00"), 100f);
                break;
            case "confused":
                skinnedMeshRenderer.SetBlendShapeWeight(GetBlendShapeIndex("eyes-vanish"), 100f);
                skinnedMeshRenderer.SetBlendShapeWeight(GetBlendShapeIndex("eyes-confused"), 100f);
                break;
            case "star":
                skinnedMeshRenderer.SetBlendShapeWeight(GetBlendShapeIndex("misc-star-eyes"), 100f);
                break;
            default:
                ResetBlendShapes();
                break;
        }
    }

    private IEnumerator TalkAnimation()
    {
        int blendShapeIndex = GetBlendShapeIndex("mouth-a");
        if (blendShapeIndex == -1) yield break;

        // Aropla 모드일 때는 IsSpeaking 체크, 그 외에는 기존 로직
        while ((ChatModeManager.Instance.IsAroplaMode() && StatusManager.Instance.IsSpeaking(this.gameObject)) ||
               ((ChatModeManager.Instance == null || !ChatModeManager.Instance.IsAroplaMode()) &&
                ((charType == "Operator" && StatusManager.Instance.IsAnsweringPortrait) ||
                 (charType == "Main" && StatusManager.Instance.isMouthActive))))
        {
            // 과거 로직(랜덤 뻐끔거림): 소리와 무관하게 0.1~0.3초마다 랜덤 개폐
            // float randomValue = Random.Range(10f, 100f);
            // skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, randomValue);
            // yield return new WaitForSeconds(Random.Range(0.1f, 0.3f));
            skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, GetMouthWeight());
            yield return null;
        }

        // 값 초기화
        skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, 0f);
    }

    // 재생 중 음성의 진폭 레벨(StatusManager)에 따라 입 벌림 가중치(0~100) 결정
    private float GetMouthWeight()
    {
        float level = StatusManager.Instance.mouthLevel;
        // Aropla 모드의 서브 캐릭터는 SubVoiceManager 경로로 재생되므로 서브 레벨 사용
        if (ChatModeManager.Instance != null && ChatModeManager.Instance.IsAroplaMode()
            && CharManager.Instance != null && CharManager.Instance.GetCurrentCharacter() != this.gameObject)
        {
            level = StatusManager.Instance.mouthLevelSub;
        }
        return level * 100f;
    }

    // --- 자동 눈 깜빡임 ---

    // 모델에 따라 셰이프 이름이 다를 수 있어 후보를 순서대로 탐색 (세트 전부 있어야 채택)
    private static readonly string[][] blinkShapeCandidates =
    {
        // "Blink"(통합) 셰이프가 변형량 0인 빈 셰이프인 모델이 있어 좌우 쌍을 우선 사용
        new[] { "eye-blink-left", "eye-blink-right" },
        new[] { "Blink" },
        new[] { "blink" },
    };

    // 눈을 연출에 쓰지 않는 상태에서만 깜빡임. 상태 전환 직후 0.7초 유예
    private bool CanBlink()
    {
        return currentFace == "normal"
            && (currentBlendType == "idle" || currentBlendType == "default")
            && Time.time - lastFaceStateChangeTime >= 0.7f;
    }

    private int[] FindBlinkShapeIndices()
    {
        foreach (string[] candidate in blinkShapeCandidates)
        {
            int[] indices = new int[candidate.Length];
            bool allFound = true;
            for (int i = 0; i < candidate.Length; i++)
            {
                indices[i] = GetBlendShapeIndex(candidate[i]);
                if (indices[i] == -1)
                {
                    allFound = false;
                    break;
                }
            }
            if (allFound) return indices;
        }

        // 폴백: 이름에 blink가 포함된 셰이프 전탐색 (감정용 변형인 happy/wink 계열 제외)
        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        List<int> found = new List<int>();
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string shapeName = mesh.GetBlendShapeName(i).ToLower();
            if (shapeName.Contains("blink") && !shapeName.Contains("happy") && !shapeName.Contains("wink"))
            {
                found.Add(i);
            }
        }
        if (found.Count > 0) return found.ToArray();

        Debug.LogWarning($"[{GetType().Name}] 눈 감기 blendshape 없음 → 깜빡임 비활성 (mesh: {mesh.name})");
        return null;
    }

    // Animator/PlayableGraph가 애니메이션 평가 단계(Update 이후)에서 blendshape을 덮어쓸 수 있어
    // 깜빡임은 그보다 늦은 LateUpdate에서 적용한다
    void LateUpdate()
    {
        if (!blinkShapeSearchDone)
        {
            if (skinnedMeshRenderer == null) return;  // Start 이전
            blinkShapeIndices = FindBlinkShapeIndices();
            blinkShapeSearchDone = true;
            nextBlinkTime = Time.time + Random.Range(blinkIntervalMin, blinkIntervalMax);
        }
        if (blinkShapeIndices == null) return;

        if (blinkPhase < 0f)
        {
            // 대기: 주기가 됐고 깜빡여도 되는 상태면 다음 프레임부터 진행
            if (Time.time >= nextBlinkTime && CanBlink())
            {
                blinkPhase = 0f;
                blinkClosedShown = false;
            }
            return;
        }

        // 진행 중 감정/얼굴 전환이 개입하면 중단. 감정 전환(FaceNormalBlendShape)은 ResetBlendShapes가
        // 이미 정리했고 blink 셰이프를 감정에 쓸 수도 있어 덮어쓰지 않고, 얼굴 스왑 중단만 0으로 복원
        if (!CanBlink())
        {
            if (currentFace != "normal") ApplyBlinkWeight(0f);
            blinkPhase = -1f;
            nextBlinkTime = Time.time + Random.Range(blinkIntervalMin, blinkIntervalMax);
            return;
        }

        blinkPhase += Time.deltaTime;
        if (blinkPhase < blinkCloseTime)
        {
            ApplyBlinkWeight(Mathf.Lerp(0f, 100f, blinkPhase / blinkCloseTime));
        }
        else if (!blinkClosedShown)
        {
            // 저FPS에서 감기 구간을 통째로 건너뛰어도 완전히 감긴 프레임이 최소 1번은 렌더되게 보장
            ApplyBlinkWeight(100f);
            blinkClosedShown = true;
        }
        else if (blinkPhase < blinkCloseTime + blinkOpenTime)
        {
            ApplyBlinkWeight(Mathf.Lerp(100f, 0f, (blinkPhase - blinkCloseTime) / blinkOpenTime));
        }
        else
        {
            ApplyBlinkWeight(0f);
            blinkPhase = -1f;
            nextBlinkTime = Time.time + Random.Range(blinkIntervalMin, blinkIntervalMax);
        }
    }

    private void ApplyBlinkWeight(float weight)
    {
        foreach (int idx in blinkShapeIndices)
        {
            skinnedMeshRenderer.SetBlendShapeWeight(idx, weight);
        }
    }

    private void ResetBlendShapes()
    {
        for (int i = 0; i < skinnedMeshRenderer.sharedMesh.blendShapeCount; i++)
        {
            skinnedMeshRenderer.SetBlendShapeWeight(i, 0);
        }
    }

    // 없을 경우 고려해서, skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(name); 말고 Loop
    private int GetBlendShapeIndex(string blendShapeName)
    {
        return skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(blendShapeName);

        // -1일 경우의 예외 처리를 위해 일단 keep
        // Mesh mesh = skinnedMeshRenderer.sharedMesh;
        // for (int i = 0; i < mesh.blendShapeCount; i++)
        // {
        //     if (mesh.GetBlendShapeName(i) == blendShapeName)
        //         return i;
        // }
        // return -1; // 없으면 -1 반환
    }

    public override void SetCharType(string newCharType)
    {
        charType = newCharType;
    }

    public override string GetCharType()
    {
        return charType;
    }
}
