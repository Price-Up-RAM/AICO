using System;
using System.Collections.Generic;
using System.IO;
using Cleverous.VaultInventory;
using UnityEngine;

// JSON 직렬화를 위한 리스트 래퍼 (캐릭터별 부위/악세서리 데이터를 통째로 저장)
[Serializable]
public class AccessorySaveDataList
{
    public List<CharacterAccessoryProfile> characterProfiles = new List<CharacterAccessoryProfile>(); // 캐릭터별 부위 오프셋 + 악세서리 아이템
}

// 슬롯 하나에 현재 장착된 악세서리 (저장/복원용)
[Serializable]
public class EquippedSlotState
{
    public string slotName;
    public string accessoryName;
}

// 캐릭터 인벤토리 저장 파일에 함께 담을 장착 상태 목록 (CharInventoryOwner가 직렬화)
[Serializable]
public class EquippedStateList
{
    public List<EquippedSlotState> slots = new List<EquippedSlotState>();
}

// 악세서리 JSON IO 및 장착/해제를 관리하는 매니저 클래스
public class AccessoryManager : MonoBehaviour
{
    private static AccessoryManager instance; // 싱글톤 인스턴스
    public static AccessoryManager Instance
    {
        get
        {
            if (instance == null)
            {
                // 인스턴스가 없으면 찾아서 할당
                instance = FindObjectOfType<AccessoryManager>();
            }
            return instance;
        }
    }

    [SerializeField] private List<AccessoryItem> allItems = new List<AccessoryItem>(); // accessoryName -> 프리팹 조회용 아이템 목록 (인스펙터에 직접 등록)

    // 캐릭터별 프로필 캐시 (키: characterCode, 빈 문자열이면 공통 기본값)
    private Dictionary<string, CharacterAccessoryProfile> profileDict = new Dictionary<string, CharacterAccessoryProfile>();

    // 캐릭터별 현재 장착 상태 (키: charcode, 값: slotName -> accessoryName). 저장/복원(CharInventoryOwner)에 사용
    private Dictionary<string, Dictionary<string, string>> equippedState = new Dictionary<string, Dictionary<string, string>>();

    // 시작 시 JSON 데이터 로드
    private void Start()
    {
        LoadAccessoryData();
    }

    // accessoryName으로 등록된 AccessoryItem(Vault SO)을 찾아 그 ArtPrefab을 반환
    private GameObject FindAccessoryPrefab(string accessoryName)
    {
        foreach (AccessoryItem item in allItems)
        {
            if (item != null && item.accessoryName == accessoryName)
            {
                return item.ArtPrefab;
            }
        }

        return null;
    }

    /* JSON 구조 샘플 (accessory_data.json):
    {
        "characterProfiles": [
            {
                "characterCode": "aico",
                "slots": [
                    {
                        "slotName": "hairpin",
                        "target1": "Slot_HairPin_R",
                        "localPosition": {"x":0.0, "y":0.0, "z":0.0},
                        "localRotation": {"x":0.0, "y":0.0, "z":0.0},
                        "items": [
                            {
                                "accessoryName": "hairpin_placeholder",
                                "localScale": {"x":1.0, "y":1.0, "z":1.0}
                            }
                        ]
                    }
                ]
            }
        ]
    }
    // characterCode가 빈 문자열이면 해당 부위/악세서리를 쓰는 모든 캐릭터의 공통 기본값(fallback)으로 쓰임.
    // localScale이 (0,0,0)이면 (1,1,1)로 처리됨 (캐릭터별 크기 보정용).
    // 실제 프리팹은 accessoryName으로 AccessoryManager.allItems에서 AccessoryItem(Vault SO)을 찾아 그 ArtPrefab을 사용함.
    */
    // JSON 데이터 파일 로드 및 인스펙터 기본 데이터 병합
    public void LoadAccessoryData()
    {
        profileDict.Clear();

        // 1. 인스펙터 기본 데이터 병합 (AccessoryData에서 제공)
        AddProfilesToDict(AccessoryData.Instance.GetAllCharacterProfiles());

        string filePath = Path.Combine(Application.persistentDataPath, "accessory_data.json"); // 저장 경로 설정

        // 2. JSON 데이터 파일 로드 및 우선 적용
        if (File.Exists(filePath))
        {
            // 파일이 존재하면 읽어서 딕셔너리에 캐싱
            string json = File.ReadAllText(filePath);
            AccessorySaveDataList dataList = JsonUtility.FromJson<AccessorySaveDataList>(json);

            AddProfilesToDict(dataList.characterProfiles); // JSON 데이터가 우선이므로 덮어쓰기
        }
    }

    // 프로필 리스트를 딕셔너리에 채움
    private void AddProfilesToDict(List<CharacterAccessoryProfile> profiles)
    {
        foreach (CharacterAccessoryProfile profile in profiles)
        {
            profileDict[profile.characterCode ?? ""] = profile; // 같은 키면 덮어쓰기 (JSON 우선 적용에 사용)
        }
    }

    // 캐릭터 코드 + 악세서리 이름으로 슬롯 아이템 찾기 (캐릭터 우선, 없으면 공통(빈 문자열)으로 fallback). 찾은 아이템이 속한 슬롯(오프셋)도 함께 반환
    private AccessorySlotItem FindSlotItem(string characterCode, string accessoryName, out AccessorySlotOffset ownerSlot)
    {
        ownerSlot = null;

        if (string.IsNullOrEmpty(characterCode) == false && profileDict.ContainsKey(characterCode))
        {
            AccessorySlotItem item = FindItemInProfile(profileDict[characterCode], accessoryName, out ownerSlot);
            if (item != null)
            {
                return item;
            }
        }

        if (profileDict.ContainsKey(""))
        {
            return FindItemInProfile(profileDict[""], accessoryName, out ownerSlot);
        }

        return null;
    }

    // 프로필 내 모든 슬롯을 순회하며 악세서리 이름으로 아이템 찾기
    private AccessorySlotItem FindItemInProfile(CharacterAccessoryProfile profile, string accessoryName, out AccessorySlotOffset ownerSlot)
    {
        ownerSlot = null;

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

    // 현재 캐싱된 데이터를 JSON 파일로 저장
    public void SaveAccessoryData()
    {
        string filePath = Path.Combine(Application.persistentDataPath, "accessory_data.json"); // 저장 경로 설정

        AccessorySaveDataList dataList = new AccessorySaveDataList(); // 저장할 리스트 래퍼 생성
        dataList.characterProfiles = new List<CharacterAccessoryProfile>(profileDict.Values);

        string json = JsonUtility.ToJson(dataList, true); // JSON 문자열로 변환 (보기 좋게)
        File.WriteAllText(filePath, json);
    }

    // 악세서리 장착 및 기존 악세서리 해제
    public void Equip(GameObject target, string accessoryName, string slotName = null)
    {
        // 캐릭터 코드 우선, 없으면 공통 기본값으로 오프셋/아이템 조회 (캐릭터마다 크기/비율이 달라 오프셋이 다를 수 있음)
        string characterCode = target.GetComponent<CharAttributes>()?.charcode;

        AccessorySlotOffset ownerSlot;
        AccessorySlotItem slotItem = FindSlotItem(characterCode, accessoryName, out ownerSlot);

        // 부위(slotName)가 명시적으로 주어지면 그쪽 오프셋을 우선 사용 (아이템 검색으로 못 찾은 슬롯을 직접 지정하는 경우)
        AccessorySlotOffset savedOffset = ownerSlot;
        string finalSlotName = string.IsNullOrEmpty(slotName) == false ? slotName : (ownerSlot != null ? ownerSlot.slotName : null);
        if (string.IsNullOrEmpty(slotName) == false)
        {
            savedOffset = AccessoryData.Instance.GetSlotOffset(characterCode, slotName);
        }

        // 캐릭터별 크기 보정용 스케일 (저장된 값이 없거나 (0,0,0)이면 (1,1,1) 사용)
        Vector3 finalScale = (slotItem != null && slotItem.localScale != Vector3.zero) ? slotItem.localScale : Vector3.one;

        // Slot 자체에 적용할 위치/회전 보정값 (같은 Slot이라도 장착하는 아이템에 따라 캡처된 값이 다를 수 있음)
        Vector3 finalPos = savedOffset != null ? savedOffset.localPosition : Vector3.zero;
        Vector3 finalRot = savedOffset != null ? savedOffset.localRotation : Vector3.zero;

        Transform finalTargetBone = null; // 최종 부모가 될 Transform

        if (savedOffset != null && string.IsNullOrEmpty(savedOffset.target1) == false)
        {
            finalTargetBone = getSlotTransformFromName(target, savedOffset.target1);
        }
        else if (string.IsNullOrEmpty(finalSlotName) == false)
        {
            // 저장된 오프셋이 없으면 부위 이름 자체를 본 이름으로 탐색 (예: 직접 본 이름을 slotName으로 넘긴 경우)
            finalTargetBone = getSlotTransformFromName(target, finalSlotName);
        }

        if (finalTargetBone == null)
        {
            // 최종적으로 타겟을 찾지 못했으면 중단
            return;
        }

        if (HasEquippedAccessory(finalTargetBone))
        {
            // 해당 부위에 이미 악세서리가 있으면 파괴 (Unequip 대체)
            RemoveEquippedAccessory(finalTargetBone);
        }

        if (string.IsNullOrEmpty(accessoryName))
        {
            // 장착할 악세서리 이름이 없거나 빈 문자열이면 기존 것을 벗기는 것으로 종료
            SetEquippedState(characterCode, finalSlotName, null);
            return;
        }

        GameObject accessoryPrefab = FindAccessoryPrefab(accessoryName);
        if (accessoryPrefab == null)
        {
            // 등록된 프리팹이 없으면 새로 장착하지 않고 종료
            return;
        }

        // Slot 자체를 캡처된 위치/회전으로 보정 (같은 Slot이라도 장착하는 아이템에 맞춰 미세 조정된 값)
        finalTargetBone.localPosition = finalPos;
        finalTargetBone.localRotation = Quaternion.Euler(finalRot);

        // 새로운 악세서리 인스턴스화 후 부모 설정. 악세서리는 Slot의 로컬 원점에 얹고 스케일만 적용한다.
        GameObject newAccessory = Instantiate(accessoryPrefab, finalTargetBone);
        newAccessory.transform.localPosition = Vector3.zero;
        newAccessory.transform.localRotation = Quaternion.identity;
        newAccessory.transform.localScale = finalScale;

        SetEquippedState(characterCode, finalSlotName, accessoryName);
    }

    // 캐릭터의 슬롯별 장착 상태를 갱신 (accessoryName이 null/빈 문자열이면 해당 슬롯 기록 제거)
    private void SetEquippedState(string characterCode, string slotName, string accessoryName)
    {
        if (string.IsNullOrEmpty(characterCode) || string.IsNullOrEmpty(slotName))
        {
            // 상태를 캐릭터/슬롯 단위로 구분할 수 없으면 추적하지 않음
            return;
        }

        if (equippedState.ContainsKey(characterCode) == false)
        {
            equippedState[characterCode] = new Dictionary<string, string>();
        }

        if (string.IsNullOrEmpty(accessoryName))
        {
            equippedState[characterCode].Remove(slotName);
        }
        else
        {
            equippedState[characterCode][slotName] = accessoryName;
        }
    }

    // 캐릭터의 특정 악세서리가 현재 장착 중인지 확인 (장착 중이면 어느 슬롯인지도 반환). 인벤토리 클릭 토글(장착<->해제)에 사용
    public bool IsEquipped(string characterCode, string accessoryName, out string equippedSlotName)
    {
        equippedSlotName = null;

        if (string.IsNullOrEmpty(characterCode) || equippedState.ContainsKey(characterCode) == false)
        {
            return false;
        }

        foreach (KeyValuePair<string, string> pair in equippedState[characterCode])
        {
            if (pair.Value == accessoryName)
            {
                equippedSlotName = pair.Key;
                return true;
            }
        }

        return false;
    }

    // 캐릭터의 현재 장착 상태를 저장용 리스트로 반환 (CharInventoryOwner가 JSON에 함께 저장)
    public EquippedStateList GetEquippedState(string characterCode)
    {
        EquippedStateList result = new EquippedStateList();

        if (string.IsNullOrEmpty(characterCode) || equippedState.ContainsKey(characterCode) == false)
        {
            return result;
        }

        foreach (KeyValuePair<string, string> pair in equippedState[characterCode])
        {
            result.slots.Add(new EquippedSlotState { slotName = pair.Key, accessoryName = pair.Value });
        }

        return result;
    }

    // 저장된 장착 상태를 캐릭터에 재적용 (CharInventoryOwner가 로드 시 호출)
    public void RestoreEquippedState(GameObject target, EquippedStateList state)
    {
        if (target == null || state == null)
        {
            return;
        }

        foreach (EquippedSlotState slot in state.slots)
        {
            Equip(target, slot.accessoryName, slot.slotName);
        }
    }

    // 지정된 대상(부위 이름 또는 본 이름)의 악세서리를 명시적으로 해제
    public void UnEquip(GameObject target, string targetName)
    {
        if (target == null || string.IsNullOrEmpty(targetName))
        {
            // 대상이나 대상 이름이 없으면 중단
            return;
        }

        string characterCode = target.GetComponent<CharAttributes>()?.charcode;

        // targetName이 논리 부위 이름(slotName)이면 캡처된 target1(본 이름)으로 변환, 아니면 본 이름 그대로 사용
        AccessorySlotOffset savedOffset = AccessoryData.Instance.GetSlotOffset(characterCode, targetName);
        string boneName = (savedOffset != null && string.IsNullOrEmpty(savedOffset.target1) == false) ? savedOffset.target1 : targetName;

        Transform targetBone = getSlotTransformFromName(target, boneName); // 타겟 검색

        if (targetBone != null)
        {
            if (HasEquippedAccessory(targetBone))
            {
                // 악세서리가 장착되어 있으면 파괴
                RemoveEquippedAccessory(targetBone);
                SetEquippedState(characterCode, targetName, null);
            }
        }
    }

    // 대상 오브젝트 하위에서 이름으로 슬롯(Transform)을 찾아 반환
    public Transform getSlotTransformFromName(GameObject target, string slotName)
    {
        if (target == null)
        {
            // 타겟이 없으면 null 반환
            return null;
        }

        // 재귀 탐색을 통해 뼈대 찾기
        return FindBoneRecursive(target.transform, slotName);
    }

    // 이름으로 계층 구조를 재귀 탐색하여 Transform 반환
    private Transform FindBoneRecursive(Transform current, string name)
    {
        if (current.name == name)
        {
            // 현재 노드 이름이 일치하면 반환
            return current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i); // 자식 노드 가져오기
            Transform result = FindBoneRecursive(child, name); // 재귀 호출

            if (result != null)
            {
                // 하위에서 찾았으면 반환
                return result;
            }
        }

        // 모두 탐색해도 없으면 null 반환
        return null;
    }

    // 해당 슬롯(본) 하위에 장착된 악세서리가 있는지 확인
    private bool HasEquippedAccessory(Transform slot)
    {
        if (slot.childCount > 0)
        {
            // 자식이 존재하면 악세서리가 있다고 판단
            return true;
        }
        else
        {
            // 자식이 없으면 없다고 판단
            return false;
        }
    }

    // 해당 슬롯(본) 하위의 모든 악세서리 게임오브젝트 파괴
    private void RemoveEquippedAccessory(Transform slot)
    {
        for (int i = slot.childCount - 1; i >= 0; i--)
        {
            // 리스트의 역순으로 오브젝트 파괴
            Destroy(slot.GetChild(i).gameObject);
        }
    }
}
