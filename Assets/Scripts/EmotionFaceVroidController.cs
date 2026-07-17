using System.Collections.Generic;
using UnityEngine;
using UniVRM10;

// VRoid 계열 VRM10 캐릭터(AICO, V_Miku 등)용 얼굴 컨트롤러.
// 다른 EmotionFace 컨트롤러와 달리 SkinnedMeshRenderer에 직접 쓰지 않는다 —
// Vrm10Instance가 LateUpdate(Runtime.Process)에서 Expression에 바인딩된 blendshape 전부를
// 매 프레임 덮어쓰기 때문. 대신 Expression 입력값(SetWeight, 0~1)만 주입하고 적용은 UniVRM에 맡긴다.
// 입=aa, 눈=blink, 감정=happy/angry/sad/relaxed/surprised. 이 컨트롤러가 해당 입력들의 유일한 작성자여야 한다.
public class EmotionFaceVroidController : EmotionFaceController
{
    public string charType = "";  // 캐릭터타입을 줘서 소환시 + 분기에 사용 : ""(Sub), Operator, Main

    [Header("눈 깜빡임 설정")]
    public float blinkIntervalMin = 2f;          // 다음 깜빡임까지 대기 시간 최소(초)
    public float blinkIntervalMax = 6f;          // 다음 깜빡임까지 대기 시간 최대(초)
    public float blinkCloseTime = 0.07f;         // 눈 감는 데 걸리는 시간(초)
    public float blinkOpenTime = 0.1f;           // 눈 뜨는 데 걸리는 시간(초)

    private Vrm10Instance vrmInstance;
    private bool initialized = false;

    // 모델에서 해석된 Expression 키 (없으면 has*=false로 해당 기능만 비활성)
    private ExpressionKey mouthKey;
    private bool hasMouthKey = false;
    private ExpressionKey blinkKey;
    private bool hasBlinkKey = false;

    // 감정 상태: 현재 켜 둔 감정 키 목록 — 상태 전환 시 0으로 되돌리기 위해 기록
    private readonly List<ExpressionKey> activeEmotionKeys = new List<ExpressionKey>();
    private string currentBlendType = "idle";
    private float lastFaceStateChangeTime = 0f;  // 상태 전환 직후 깜빡임 유예용

    // 깜빡임 상태 (기존 컨트롤러들과 동일한 LateUpdate 상태머신, weight만 0~1 스케일)
    private float nextBlinkTime = 0f;
    private float blinkPhase = -1f;              // 0 이상이면 깜빡임 진행 중(경과 시간)
    private bool blinkClosedShown = false;       // 저FPS에서도 완전 감김 1프레임 보장용

    private List<string> animationList = new List<string>   // Test(NextAnimation)용
    {
        "idle", "happy", "relaxed", "angry", "sad", "surprised", "><", "default"
    };

    void LateUpdate()
    {
        if (!EnsureInitialized()) return;

        UpdateMouth();
        UpdateBlink();
    }

    // --- 초기화 ---

    private bool EnsureInitialized()
    {
        if (initialized) return vrmInstance != null;
        initialized = true;

        vrmInstance = GetComponent<Vrm10Instance>();
        if (vrmInstance == null) vrmInstance = GetComponentInChildren<Vrm10Instance>();
        if (vrmInstance == null)
        {
            Debug.LogWarning($"[{GetType().Name}] Vrm10Instance가 없어 얼굴 제어 비활성: {gameObject.name}");
            return false;
        }

        hasMouthKey = TryFindPresetKey(ExpressionPreset.aa, out mouthKey);
        hasBlinkKey = TryFindPresetKey(ExpressionPreset.blink, out blinkKey);
        if (!hasMouthKey) Debug.LogWarning($"[{GetType().Name}] aa(입) Expression이 없어 립싱크 비활성: {gameObject.name}");
        if (!hasBlinkKey) Debug.LogWarning($"[{GetType().Name}] blink(눈) Expression이 없어 깜빡임 비활성: {gameObject.name}");
        nextBlinkTime = Time.time + Random.Range(blinkIntervalMin, blinkIntervalMax);
        return true;
    }

    // 모델에 실제 존재하는 Expression 키에서 preset 일치 항목 탐색
    private bool TryFindPresetKey(ExpressionPreset preset, out ExpressionKey key)
    {
        foreach (var k in vrmInstance.Runtime.Expression.GetWeights().Keys)
        {
            if (k.Preset == preset)
            {
                key = k;
                return true;
            }
        }
        key = default;
        return false;
    }

    // 커스텀 Expression 키 탐색 (VRoid의 Surprised처럼 preset 없이 내보내지는 클립 대응)
    private bool TryFindCustomKey(string name, out ExpressionKey key)
    {
        foreach (var k in vrmInstance.Runtime.Expression.GetWeights().Keys)
        {
            if (k.Preset == ExpressionPreset.custom
                && string.Equals(k.Name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                key = k;
                return true;
            }
        }
        key = default;
        return false;
    }

    // --- 립싱크 (진폭 기반, StatusManager.mouthLevel 사용) ---

    private void UpdateMouth()
    {
        if (!hasMouthKey) return;

        float level = 0f;
        if (ChatModeManager.Instance != null && ChatModeManager.Instance.IsAroplaMode())
        {
            if (StatusManager.Instance.IsSpeaking(this.gameObject))
            {
                // Aropla 서브 캐릭터(현재 메인이 아닌 캐릭터)는 SubVoiceManager 경로로 재생됨
                bool isMain = CharManager.Instance != null && CharManager.Instance.GetCurrentCharacter() == this.gameObject;
                level = isMain ? StatusManager.Instance.mouthLevel : StatusManager.Instance.mouthLevelSub;
            }
        }
        else if (charType == "Operator")
        {
            if (StatusManager.Instance.IsAnsweringPortrait) level = StatusManager.Instance.mouthLevel;
        }
        else if (charType == "Main")
        {
            if (StatusManager.Instance.isMouthActive) level = StatusManager.Instance.mouthLevel;
        }
        // Sub("") 등은 level 0 = 입 다묾

        vrmInstance.Runtime.Expression.SetWeight(mouthKey, level);
    }

    // --- 자동 눈 깜빡임 ---

    // 눈을 연출에 쓰지 않는 상태에서만 깜빡임. 상태 전환 직후 0.7초 유예
    private bool CanBlink()
    {
        return (currentBlendType == "idle" || currentBlendType == "default")
            && Time.time - lastFaceStateChangeTime >= 0.7f;
    }

    private void UpdateBlink()
    {
        if (!hasBlinkKey) return;

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

        // 진행 중 감정이 개입하면 중단. blink 입력은 이 컨트롤러가 유일한 작성자이므로 0으로 정리해도 안전
        if (!CanBlink())
        {
            SetBlinkWeight(0f);
            blinkPhase = -1f;
            nextBlinkTime = Time.time + Random.Range(blinkIntervalMin, blinkIntervalMax);
            return;
        }

        blinkPhase += Time.deltaTime;
        if (blinkPhase < blinkCloseTime)
        {
            SetBlinkWeight(Mathf.Lerp(0f, 1f, blinkPhase / blinkCloseTime));
        }
        else if (!blinkClosedShown)
        {
            // 저FPS에서 감기 구간을 통째로 건너뛰어도 완전히 감긴 프레임이 최소 1번은 렌더되게 보장
            SetBlinkWeight(1f);
            blinkClosedShown = true;
        }
        else if (blinkPhase < blinkCloseTime + blinkOpenTime)
        {
            SetBlinkWeight(Mathf.Lerp(1f, 0f, (blinkPhase - blinkCloseTime) / blinkOpenTime));
        }
        else
        {
            SetBlinkWeight(0f);
            blinkPhase = -1f;
            nextBlinkTime = Time.time + Random.Range(blinkIntervalMin, blinkIntervalMax);
        }
    }

    private void SetBlinkWeight(float weight01)
    {
        vrmInstance.Runtime.Expression.SetWeight(blinkKey, weight01);
    }

    // --- 감정 표현 ---

    // 얼굴 감정 변경 통합 함수
    public override void ShowEmotion(string emotion)
    {
        Debug.Log("Show Emotion Face : " + emotion);
        ApplyEmotionState(NormalizeState(emotion));
    }

    // joy, anger, confusion, sadness, surprise, neutral을 각각 표정변환
    public override void ShowEmotionFromEmotion(string emotion)
    {
        string selectedState;
        switch (emotion.ToLower())
        {
            case "joy":
                selectedState = Random.value < 0.5f ? "happy" : "relaxed";
                break;
            case "anger":
                selectedState = "angry";
                break;
            case "confusion":
                selectedState = "confused";
                break;
            case "sadness":
                selectedState = "sad";
                break;
            case "surprise":
                selectedState = "surprised";
                break;
            case "neutral":
            default:
                selectedState = "default";
                break;
        }

        ApplyEmotionState(selectedState);
        Debug.Log($"VROID : [Emotion Input] {emotion} → [State] {selectedState}");
    }

    // listen등의 행동시 표정 변환
    public override void ShowEmotionFromAction(string action)
    {
        string selectedState;
        switch (action.ToLower())
        {
            case "listen":
                selectedState = "listen";
                break;
            default:
                selectedState = "default";
                break;
        }

        ApplyEmotionState(selectedState);
        Debug.Log($"VROID : [Action Input] {action} → [State] {selectedState}");
    }

    // 다른 컨트롤러 어휘(relax 등)와 미지의 상태를 이 컨트롤러의 정규 상태로 흡수.
    // 미지 상태를 default로 접는 이유: 알 수 없는 문자열이 상태로 남으면 깜빡임 게이트가 영구 차단된다
    private string NormalizeState(string emotion)
    {
        switch (emotion)
        {
            case "happy":
            case "><":
            case "relaxed":
            case "listen":
            case "angry":
            case "sad":
            case "confused":
            case "surprised":
            case "idle":
            case "default":
                return emotion;
            case "joy":
                return "happy";
            case "fun":
            case "relax":
                return "relaxed";
            case "sorrow":
                return "sad";
            default:
                return "default";
        }
    }

    private void ApplyEmotionState(string state)
    {
        if (!EnsureInitialized()) return;
        if (state == currentBlendType) return;  // 변경 시에만 반영 (깜빡임 유예 리셋 방지)

        // 이전 감정 끄기
        foreach (var key in activeEmotionKeys)
        {
            vrmInstance.Runtime.Expression.SetWeight(key, 0f);
        }
        activeEmotionKeys.Clear();

        switch (state)
        {
            case "happy":
            case "><":  // VRoid의 happy(구 joy)가 >< 형태의 활짝 웃는 얼굴
                AddEmotion(ExpressionPreset.happy, null, 1f);
                break;
            case "relaxed":
                AddEmotion(ExpressionPreset.relaxed, null, 1f);
                break;
            case "listen":
                AddEmotion(ExpressionPreset.relaxed, null, 0.5f);
                break;
            case "angry":
                AddEmotion(ExpressionPreset.angry, null, 1f);
                break;
            case "sad":
                AddEmotion(ExpressionPreset.sad, null, 1f);
                break;
            case "confused":
                AddEmotion(ExpressionPreset.sad, null, 0.5f);
                break;
            case "surprised":
                // VRoid 내보내기는 surprised가 preset 없는 커스텀 클립("Surprised")인 경우가 많다
                if (!AddEmotion(ExpressionPreset.surprised, null, 1f))
                {
                    AddEmotion(ExpressionPreset.custom, "Surprised", 1f);
                }
                break;
            case "idle":
            case "default":
            default:
                // 감정 없음 (모든 감정 키 0 = 기본 얼굴)
                break;
        }

        currentBlendType = state;
        lastFaceStateChangeTime = Time.time;
    }

    // 모델에 존재하는 키만 적용하고 적용 여부를 반환
    private bool AddEmotion(ExpressionPreset preset, string customName, float weight)
    {
        ExpressionKey key;
        bool found = customName == null
            ? TryFindPresetKey(preset, out key)
            : TryFindCustomKey(customName, out key);
        if (!found) return false;

        vrmInstance.Runtime.Expression.SetWeight(key, weight);
        activeEmotionKeys.Add(key);
        return true;
    }

    // --- 공통 인터페이스 ---

    // Test용 코드
    private int currentAnimationIndex = 0;
    public override void NextAnimation()
    {
        currentAnimationIndex = (currentAnimationIndex + 1) % animationList.Count;
        ShowEmotion(animationList[currentAnimationIndex]);
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
