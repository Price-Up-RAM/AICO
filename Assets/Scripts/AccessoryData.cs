using System;
using System.Collections.Generic;
using UnityEngine;

// 슬롯(부위)에 장착 가능한 악세서리 아이템 1개 (악세서리 식별 이름 + 캐릭터별 크기 보정을 한 묶음으로 관리)
// 실제 프리팹은 여기서 들고 있지 않고, accessoryName으로 AccessoryItem(Vault SO)을 찾아 그 ArtPrefab을 사용한다.
[Serializable]
public class AccessorySlotItem
{
    public string accessoryName; // 악세서리 식별 이름 (AccessoryItem.accessoryName과 매칭)
    public Vector3 localScale; // 이 캐릭터에서 장착 시 적용할 로컬 스케일 ((0,0,0)이면 (1,1,1)로 처리)
}

// 캐릭터의 부위(슬롯) 하나에 대한 위치/회전 오프셋 + 이 부위에 끼울 수 있는 악세서리 아이템들
[Serializable]
public class AccessorySlotOffset
{
    public string slotName; // 부위 식별자: "hairpin", "neckitem", "ring" 등
    public string target1; // 본/슬롯 이름
    public Vector3 localPosition; // 장착 로컬 위치
    public Vector3 localRotation; // 장착 로컬 회전값 (오일러 각도)
    public List<AccessorySlotItem> items = new List<AccessorySlotItem>(); // 이 부위에 장착 가능한 악세서리들 (이름 + 스케일)
}

// 캐릭터 1명의 부위별 오프셋을 모아놓은 프로필
[Serializable]
public class CharacterAccessoryProfile
{
    public string characterCode; // 캐릭터 식별자 (CharAttributes.charcode). 빈 문자열이면 모든 캐릭터 공통 기본값
    public List<AccessorySlotOffset> slots = new List<AccessorySlotOffset>(); // 이 캐릭터의 부위별 위치/회전 + 악세서리
}

// 악세서리 데이터를 보관하고 Get/Set 하는 전역 컨테이너
public class AccessoryData : MonoBehaviour
{
    private static AccessoryData instance; // 싱글톤 인스턴스
    public static AccessoryData Instance
    {
        get
        {
            if (instance == null)
            {
                // 인스턴스가 없으면 찾아서 할당
                instance = FindObjectOfType<AccessoryData>();
            }
            return instance;
        }
    }

    [SerializeField] private List<CharacterAccessoryProfile> characterProfiles = new List<CharacterAccessoryProfile>(); // 캐릭터별 부위/악세서리 (인스펙터 기본값)

    // 인스펙터에 등록된 캐릭터 프로필 전체 반환
    public List<CharacterAccessoryProfile> GetAllCharacterProfiles()
    {
        return characterProfiles;
    }

    // 캐릭터 코드로 프로필 찾기 (없으면 null)
    public CharacterAccessoryProfile GetCharacterProfile(string characterCode)
    {
        foreach (CharacterAccessoryProfile profile in characterProfiles)
        {
            if (profile.characterCode == characterCode)
            {
                // 캐릭터 코드가 일치하면 반환
                return profile;
            }
        }

        return null;
    }

    // 캐릭터 코드 + 부위 이름으로 슬롯 오프셋 찾기 (캐릭터 우선, 없으면 공통(빈 문자열) 프로필로 fallback)
    public AccessorySlotOffset GetSlotOffset(string characterCode, string slotName)
    {
        CharacterAccessoryProfile profile = GetCharacterProfile(characterCode);
        AccessorySlotOffset offset = FindSlotOffset(profile, slotName);

        if (offset != null)
        {
            // 캐릭터 전용 오프셋을 찾았으면 반환
            return offset;
        }

        // 캐릭터 전용 오프셋이 없으면 공통(빈 문자열) 프로필로 fallback
        CharacterAccessoryProfile commonProfile = GetCharacterProfile("");
        return FindSlotOffset(commonProfile, slotName);
    }

    // 프로필 내에서 부위 이름으로 오프셋 찾기
    private AccessorySlotOffset FindSlotOffset(CharacterAccessoryProfile profile, string slotName)
    {
        if (profile == null)
        {
            // 프로필이 없으면 null 반환
            return null;
        }

        foreach (AccessorySlotOffset slot in profile.slots)
        {
            if (slot.slotName == slotName)
            {
                // 부위 이름이 일치하면 반환
                return slot;
            }
        }

        return null;
    }

    // 캐릭터 코드 + 악세서리 이름으로 슬롯 아이템 찾기 (캐릭터 우선, 없으면 공통(빈 문자열)으로 fallback). 찾은 아이템이 속한 슬롯(오프셋)도 함께 반환
    public AccessorySlotItem FindSlotItem(string characterCode, string accessoryName, out AccessorySlotOffset ownerSlot)
    {
        CharacterAccessoryProfile profile = GetCharacterProfile(characterCode);
        AccessorySlotItem item = FindItemInProfile(profile, accessoryName, out ownerSlot);

        if (item != null)
        {
            // 캐릭터 전용 아이템을 찾았으면 반환
            return item;
        }

        // 캐릭터 전용 아이템이 없으면 공통(빈 문자열) 프로필로 fallback
        CharacterAccessoryProfile commonProfile = GetCharacterProfile("");
        return FindItemInProfile(commonProfile, accessoryName, out ownerSlot);
    }

    // 프로필 내 모든 슬롯을 순회하며 악세서리 이름으로 아이템 찾기
    private AccessorySlotItem FindItemInProfile(CharacterAccessoryProfile profile, string accessoryName, out AccessorySlotOffset ownerSlot)
    {
        ownerSlot = null;

        if (profile == null)
        {
            // 프로필이 없으면 null 반환
            return null;
        }

        foreach (AccessorySlotOffset slot in profile.slots)
        {
            foreach (AccessorySlotItem item in slot.items)
            {
                if (item.accessoryName == accessoryName)
                {
                    // 악세서리 이름이 일치하면 반환
                    ownerSlot = slot;
                    return item;
                }
            }
        }

        return null;
    }

#if UNITY_EDITOR
    // 에디터 캡처 툴에서 씬의 Slot/악세서리 Transform 값을 골라 인스펙터 기본값에 기록
    public void CaptureSlotTransform(string characterCode, string slotName, string accessoryName, Transform slot, Transform placeholder)
    {
        CharacterAccessoryProfile profile = GetCharacterProfile(characterCode);
        if (profile == null)
        {
            profile = new CharacterAccessoryProfile();
            profile.characterCode = characterCode;
            characterProfiles.Add(profile);
        }

        AccessorySlotOffset offset = FindSlotOffset(profile, slotName);
        if (offset == null)
        {
            offset = new AccessorySlotOffset();
            offset.slotName = slotName;
            offset.target1 = slot.name;
            profile.slots.Add(offset);
        }

        offset.localPosition = slot.localPosition;
        offset.localRotation = slot.localRotation.eulerAngles;

        if (string.IsNullOrEmpty(accessoryName))
        {
            // 악세서리 이름이 없으면 아이템(스케일)은 기록하지 않음
            return;
        }

        AccessorySlotItem item = null;
        foreach (AccessorySlotItem existing in offset.items)
        {
            if (existing.accessoryName == accessoryName)
            {
                item = existing;
                break;
            }
        }

        if (item == null)
        {
            item = new AccessorySlotItem();
            item.accessoryName = accessoryName;
            offset.items.Add(item);
        }

        item.localScale = placeholder != null ? placeholder.localScale : Vector3.one;
    }
#endif
}
