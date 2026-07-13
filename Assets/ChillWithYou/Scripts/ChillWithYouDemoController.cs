using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ChillWithYouSample 데모 전용 컨트롤러 — 캐릭터 교체 + 자동 착석만 담당.
/// 착석 오프셋 튜닝 UI는 공용 SitSupport 패널(SitSupportScript)이 맡는다
/// (캐릭터 교체는 SitSupport가 charcode 변경을 폴링해 자동으로 슬라이더를 리로드).
/// 참조는 씬 베이크 시(ChillWithYouSampleBuilder) 주입된다.
/// </summary>
public class ChillWithYouDemoController : MonoBehaviour
{
    [Header("씬 참조")]
    public ChillModeManager chillManager;
    public RectTransform charParent;     // 캐릭터 스폰 부모 (Canvas_Char)
    public GameObject currentCharacter;

    [Header("캐릭터 프리팹 (버튼 순서와 동일)")]
    public GameObject[] characterPrefabs;
    public Button[] characterButtons;

    private const int CharLayer = 3;
    private const float EnterDelaySeconds = 0.5f; // MagicaCloth 등 초기화 후 착석

    private void Start()
    {
        if (chillManager != null && currentCharacter != null)
        {
            chillManager.overrideCharacter = currentCharacter;
        }

        if (characterButtons != null)
        {
            for (int i = 0; i < characterButtons.Length; i++)
            {
                int index = i; // 클로저 캡처용 복사
                if (characterButtons[i] != null)
                {
                    characterButtons[i].onClick.AddListener(() => SwapCharacter(index));
                }
            }
        }

        StartCoroutine(AutoEnter());
    }

    private IEnumerator AutoEnter()
    {
        yield return new WaitForSeconds(EnterDelaySeconds);
        if (chillManager != null && !chillManager.IsChillMode)
        {
            chillManager.EnterChillMode();
        }
    }

    /// <summary>일어나기 → 캐릭터 교체 → 재착석. 책상/의자는 ChillModeManager가 관리하므로 그대로.</summary>
    public void SwapCharacter(int index)
    {
        if (characterPrefabs == null || index < 0 || index >= characterPrefabs.Length) return;
        GameObject prefab = characterPrefabs[index];
        if (prefab == null || chillManager == null) return;

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
        if (rt != null) rt.anchoredPosition3D = new Vector3(0f, -450f, 0f);
        SetLayerRecursive(next, CharLayer); // Main Camera 컬링(3|6) 대응

        currentCharacter = next;
        chillManager.overrideCharacter = next;

        StartCoroutine(AutoEnter());
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
