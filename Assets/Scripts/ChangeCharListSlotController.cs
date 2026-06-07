using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChangeCharListSlotController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image characterIcon;     // 캐릭터 아이콘 (현재 입고 있는 의상 기준)
    [SerializeField] private TextMeshProUGUI nameText; // 캐릭터 이름 텍스트
    
    [Header("Favorite")]
    [SerializeField] private Button favoriteBtn;
    [SerializeField] private Image favoriteImage;     // 별 이미지

    // 현재 슬롯에 할당된 데이터 참조
    private ChangeCharInfo charData;
    public ChangeCharInfo CharData => charData;

    // 슬롯 초기화 및 데이터 주입
    public void InitSlot(ChangeCharInfo data)
    {
        charData = data;

        // 이름 셋팅
        nameText.text = charData.name;

        // 선택 가능 여부 확인
        bool isSelectable = false;
        if (charData.clothesList.Count > 0)
        {
            isSelectable = charData.clothesList[0].isSelectable;
        }

        // 아이콘 표시 및 상호작용 여부 제어
        if (isSelectable)
        {
            // 선택 가능 시 아이콘 활성화 및 이미지 로드
            if (characterIcon != null)
            {
                characterIcon.gameObject.SetActive(true);
            }

            if (charData.clothesList.Count > 0)
            {
                LoadSpriteForClothes(charData.clothesList[0]);
            }

            // 버튼 컴포넌트 활성화
            Button slotButton = GetComponent<Button>();
            if (slotButton != null)
            {
                slotButton.interactable = true;
            }
        }
        else
        {
            // 선택 불가능 시 아이콘 비활성화
            if (characterIcon != null)
            {
                characterIcon.gameObject.SetActive(false);
            }

            // 버튼 컴포넌트 비활성화
            Button slotButton = GetComponent<Button>();
            if (slotButton != null)
            {
                slotButton.interactable = false;
            }
        }

        UpdateFavoriteUI();
    }

    // 의상 스프라이트 로드
    private async void LoadSpriteForClothes(ChangeCharClothesInfo clothes)
    {
        if (clothes == null)
        {
            ApplyFallbackSprite();
            return;
        }

        if (clothes.isLocal)
        {
            Sprite localSprite = ChangeCharManager.Instance.GetLocalSprite(clothes.spriteAddress);
            if (localSprite != null)
            {
                characterIcon.sprite = localSprite;
            }
            else
            {
                ApplyFallbackSprite();
            }
            return;
        }

        if (string.IsNullOrEmpty(clothes.spriteAddress))
        {
            ApplyFallbackSprite();
            return;
        }

        // 다운로드된 경우만 로드, 미다운로드면 null → fallback
        Sprite sprite = await AddressableManager.Instance.LoadIfExist<Sprite>(clothes.spriteAddress);
        if (sprite != null)
        {
            characterIcon.sprite = sprite;
        }
        else
        {
            ApplyFallbackSprite();
        }
    }

    // Fallback 스프라이트 적용
    private void ApplyFallbackSprite()
    {
        if (ChangeCharManager.Instance.fallbackSprite != null)
        {
            characterIcon.sprite = ChangeCharManager.Instance.fallbackSprite;
        }
        else
        {
            Debug.LogError("CRITICAL: Fallback sprite is missing in ChangeCharManager!");
        }
    }

    // UI 버튼 - 즐겨찾기 별모양
    public void OnClickFavorite()
    {
        // 데이터 상태 반전
        charData.isFavorite = !charData.isFavorite;

        // 저장 후 UI 갱신 (Manager 측에 위임)
        ChangeCharManager.Instance.SaveFavorites();
        ChangeCharManager.Instance.RefreshAllSlotsFavoriteUI();
    }

    // 즐겨찾기 데이터 상태에 맞춰서 별 이미지 업데이트
    public void UpdateFavoriteUI()
    {
        // 널 체크
        if (charData == null)
        {
            return;
        }

        // 인스펙터에 별 이미지가 잘 연결되어 있을 경우 UI 갱신
        if (favoriteImage != null)
        {
            if (charData.isFavorite)
            {
                // on 일때
                favoriteImage.color = new Color32(255, 255, 0, 255);
            }
            else
            {
                // off 일때
                favoriteImage.color = new Color32(180, 180, 180, 255);
            }
        }
    }

    // 캐릭터 최종 변경 적용 (버튼 클릭 시 연결됨)
    public void ChangeChar()
    {
        // 리스트 슬롯에서는 의상 변경 기능 없이 기본(첫 번째) 의상만 사용하므로 Index 0으로 고정
        if (charData != null)
        {
            if (charData.clothesList.Count > 0)
            {
                ChangeCharClothesInfo defaultClothes = charData.clothesList[0];
                
                // 선택 가능 여부 확인
                if (defaultClothes.isSelectable)
                {
                    // 선택 가능 시 캐릭터 변경 적용
                    LoadAndChangeCharacter(defaultClothes);
                }
            }
        }
    }

    // 캐릭터 프리팹 로드 및 변경
    private async void LoadAndChangeCharacter(ChangeCharClothesInfo clothes)
    {
        if (string.IsNullOrEmpty(clothes.prefabAddress))
        {
            return;
        }

        // 공용 2d_general
        if (clothes.prefabAddress == "2d_general")
        {
            // 2d_general DLC 에셋(애니메이터)이 미다운로드 상태면 먼저 다운로드
            if (!clothes.isLocal && !string.IsNullOrEmpty(clothes.animatorControllerAddress))
            {
                var ac = await AddressableManager.Instance.LoadWithDownloadableAsync<RuntimeAnimatorController>(clothes.animatorControllerAddress);
                if (ac == null)
                {
                    Debug.LogWarning($"[DLC] 2d_general 에셋 다운로드 취소: {clothes.animatorControllerAddress}");
                    return; // 다운로드 취소 시 변경 중단
                }
            }

            await CharManager.Instance.ChangeCharacter2DGeneral(clothes);
            LoadSpriteForClothes(clothes);
            return;
        }

        if (clothes.isLocal)
        {
            GameObject localPrefab = ChangeCharManager.Instance.GetLocalPrefab(clothes.prefabAddress);
            if (localPrefab == null)
            {
                Debug.LogWarning($"[LocalChar] 변경 실패: {clothes.prefabAddress}");
                return;
            }

            CharManager.Instance.ChangeCharacterFromGameObject(localPrefab);
            LoadSpriteForClothes(clothes);
            return;
        }

        // 없으면 다운로드, 있으면 바로 로드
        AddressableManager.Instance.LoadWithDownloadable<GameObject>(clothes.prefabAddress, (success, prefab) =>
        {
            if (success)
            {
                CharManager.Instance.ChangeCharacterFromDLC(prefab);
                LoadSpriteForClothes(clothes); // 다운로드 완료 후 스프라이트 갱신
            }
            else
            {
                Debug.LogWarning($"[DLC] 다운로드 취소 또는 실패: {clothes.prefabAddress}");
            }
        });
    }
}
