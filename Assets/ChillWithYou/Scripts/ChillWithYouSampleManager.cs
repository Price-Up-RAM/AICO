using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ChillWithYouSample 씬 전용 싱글톤 매니저.
/// 데모에 하드코딩돼 있던 값(캐릭터 3종/스폰 위치/착석 지연)을 가변 데이터로 승격한다:
/// - characters: 인스펙터에서 편집하거나 런타임 API(Add/Remove/SetCharacters)로 갈아끼우는 캐릭터 목록.
///   목록이 바뀌면 OnCharactersChanged로 데모 UI(ChillWithYouDemoController)가 버튼을 다시 그린다.
/// - SwitchCharacter: 일어나기 → 교체 → 재착석 (구 ChillWithYouDemoController.SwapCharacter 이관).
///   목록에 없는 임의 프리팹도 SwitchCharacter(prefab)로 즉시 투입 가능.
/// 캐릭터 카탈로그 SO가 생기면 SetCharacters/AddCharacter로 흘려 넣는 것을 전제로 한 API 구성.
/// 참조는 씬 베이크 시(ChillWithYouSampleBuilder) 주입된다. 본편 SampleScene에는 존재하지 않는다.
/// </summary>
public class ChillWithYouSampleManager : MonoBehaviour
{
    private static ChillWithYouSampleManager instance;
    public static ChillWithYouSampleManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ChillWithYouSampleManager>();
            }
            return instance;
        }
    }

    [System.Serializable]
    public class CharacterEntry
    {
        public string label;      // 버튼 표기 (비우면 프리팹 이름)
        public GameObject prefab; // 본편 착석 게이트와 동일 요건: RectTransform 루트 + CharAttributes(charcode) + Animator
    }

    [Header("데모 캐릭터 목록 (데모 패널 버튼이 이 목록으로 자동 생성)")]
    public List<CharacterEntry> characters = new List<CharacterEntry>();

    [Header("스폰/착석 설정")]
    public Vector3 spawnPosition = new Vector3(0f, -450f, 0f);  // 교체 직후 캐릭터 anchoredPosition3D
    public float enterDelaySeconds = 0.5f;                      // MagicaCloth 등 초기화 후 착석까지 지연

    [Header("씬 참조 (빌더 주입)")]
    public ChillModeManager chillManager;
    public RectTransform charParent;      // 캐릭터 스폰 부모 (Canvas_Char)
    public GameObject currentCharacter;   // 현재 데모 캐릭터 인스턴스

    /// <summary>characters 목록이 바뀔 때 발화 — 데모 UI가 버튼을 재생성한다.</summary>
    public event System.Action OnCharactersChanged;

    private const int CharLayer = 3; // 본편 Canvas_Char/캐릭터 레이어 (Main Camera 컬링 3|6)

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Start()
    {
        if (chillManager != null && currentCharacter != null)
        {
            chillManager.overrideCharacter = currentCharacter;
        }
        if (currentCharacter != null)
        {
            StripDemoUnsafeComponents(currentCharacter);
        }
        StartCoroutine(AutoEnter());
    }

    // 데모 씬에는 본편 매니저/위젯(ContextMenu, RadialMenuAction, StatusManager 등)이 없어
    // 원본 캐릭터 프리팹의 마스코트 스크립트가 매 프레임 NRE를 뿜는다(예: menutrigger.cs:88).
    // 스폰 시점에 제거해 POC 사전 복사본 없이도 임의 프리팹을 목록에 등록할 수 있게 한다.
    // (제거 목록은 빌더 ProcessPocPrefab과 동일 — 원본 프리팹 에셋은 불변, 데모 인스턴스만 처리)
    private static void StripDemoUnsafeComponents(GameObject character)
    {
        StripAll<FallingObject>(character);
        StripAll<MenuTrigger>(character);
        StripAll<ClickHandler>(character);
        StripAll<DragHandler>(character);
        StripAll<DragHandler2D>(character);
        StripAll<WheelHandler>(character);
        StripAll<AnimationController>(character);
        StripAll<EmotionFaceAronaController>(character);
        StripAll<EmotionFaceAronaNewController>(character);
    }

    private static void StripAll<T>(GameObject root) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            Behaviour behaviour = component as Behaviour;
            if (behaviour != null)
            {
                behaviour.enabled = false; // Destroy는 프레임 끝 처리라, 그 사이 Update 1회 NRE 방지
            }
            Destroy(component);
        }
    }

    // ---------------------------------------------------------------- 캐릭터 목록 관리

    public int Count { get { return characters.Count; } }

    public string GetLabel(int index)
    {
        if (index < 0 || index >= characters.Count) return "";
        CharacterEntry entry = characters[index];
        if (!string.IsNullOrEmpty(entry.label)) return entry.label;
        return entry.prefab != null ? entry.prefab.name : "(빈 슬롯)";
    }

    /// <summary>목록에 캐릭터 추가 (label 생략 시 프리팹 이름). UI 자동 갱신.</summary>
    public void AddCharacter(GameObject prefab, string label = null)
    {
        if (prefab == null) return;
        characters.Add(new CharacterEntry { label = label, prefab = prefab });
        RaiseCharactersChanged();
    }

    public void RemoveCharacterAt(int index)
    {
        if (index < 0 || index >= characters.Count) return;
        characters.RemoveAt(index);
        RaiseCharactersChanged();
    }

    /// <summary>목록 전체 교체 — 캐릭터 카탈로그(SO 등) 연동 지점.</summary>
    public void SetCharacters(List<CharacterEntry> entries)
    {
        characters = entries != null ? entries : new List<CharacterEntry>();
        RaiseCharactersChanged();
    }

    private void RaiseCharactersChanged()
    {
        if (OnCharactersChanged != null) OnCharactersChanged();
    }

    // ---------------------------------------------------------------- 캐릭터 교체

    public void SwitchCharacter(int index)
    {
        if (index < 0 || index >= characters.Count) return;
        SwitchCharacter(characters[index].prefab);
    }

    /// <summary>일어나기 → 캐릭터 교체 → 재착석. 책상/의자는 ChillModeManager가 관리하므로 그대로.
    /// 목록에 등록되지 않은 프리팹도 직접 투입할 수 있다.</summary>
    public void SwitchCharacter(GameObject prefab)
    {
        if (prefab == null || chillManager == null || charParent == null) return;

        if (chillManager.IsChillMode)
        {
            chillManager.ExitChillMode(); // 원상 복구 후 교체 (착석 중 파괴 방지)
        }

        if (currentCharacter != null)
        {
            Destroy(currentCharacter);
        }

        GameObject next = Instantiate(prefab, charParent);
        next.name = prefab.name;
        RectTransform rt = next.transform as RectTransform;
        if (rt != null) rt.anchoredPosition3D = spawnPosition;
        SetLayerRecursive(next, CharLayer);
        StripDemoUnsafeComponents(next);

        currentCharacter = next;
        chillManager.overrideCharacter = next;

        StartCoroutine(AutoEnter());
    }

    private IEnumerator AutoEnter()
    {
        yield return new WaitForSeconds(enterDelaySeconds);
        if (chillManager != null && !chillManager.IsChillMode)
        {
            chillManager.EnterChillMode();
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }
}
