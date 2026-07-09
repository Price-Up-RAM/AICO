using System;
using System.Collections.Generic;
using UnityEngine;

// 슬롯 정의 1개 (템플릿의 원소). 위치/회전은 캐릭터 루트 프레임 기준 + 캐릭터 키로 정규화 —
// 리그마다 본 로컬 축이 달라(Bip001 vs mixamorig) 본-로컬 오프셋은 크로스 캐릭터에서 방향이 틀어지기 때문.
[Serializable]
public class EquipSlotDef
{
    public string slotId;                 // 슬롯 식별자 ("head", "chest", "back", "overhead", "origin")
    public string socketName;             // 생성할 소켓 GO 이름 (예: "Socket_head")
    public bool attachToRoot;             // true면 본 탐색 없이 캐릭터 루트에 부착 (origin용)

    public string boneName;               // 골든 캐릭터에서의 정확 본 이름 (사다리 1순위)
    public int humanoidBone = -1;         // HumanBodyBones 값 (사다리 2순위, 없으면 -1)
    public List<string> boneAliases = new List<string>();  // 토큰 별칭 (사다리 3순위)

    public Vector3 rootDirFromBone;       // 본 원점→소켓 오프셋 (루트 프레임 방향, 캐릭터 키로 정규화)
    public Vector3 rootFrameEuler;        // 소켓 회전 (루트 프레임 상대, 오일러)
    public float capsuleHeightRatio;      // 캡슐 월드 길이 / 캐릭터 키
    public int capsuleDirection = 1;      // 캡슐 축

    public Vector3 normalizedBoundsPos;   // 바운드 비율 위치 (NEAREST 폴백의 목표점 계산용)
}

// 슬롯 템플릿 = "원본". 골든 캐릭터에서 Capture로 만들고, 다른 캐릭터에 스탬프로 복사한다.
// Editor 폴더 전용 에셋 (Resources 금지 — 빌드에 실리면 missing script).
// CreateAssetMenu는 두지 않는다 — 사용자가 Editor 폴더 밖(Resources 등)에 만들면 빌드 풋건이 되므로
// 생성은 EquipAuthoringUtil.GetOrCreateDefaultTemplate 경로로 일원화.
public class EquipSlotTemplate : ScriptableObject
{
    // 표준 슬롯 5종 (사용자 확정: chest=앞가슴, back=등, head=헤어핀, overhead=모자/천사링, origin=오오라)
    public static readonly string[] StandardSlotIds = { "chest", "back", "head", "overhead", "origin" };

    public List<EquipSlotDef> slots = new List<EquipSlotDef>();  // 슬롯 정의 목록

    // 표준 슬롯 여부
    public static bool IsStandardSlot(string slotId)
    {
        foreach (string id in StandardSlotIds)
        {
            if (id == slotId)
            {
                return true;
            }
        }
        return false;
    }

    // slotId로 정의 찾기 (없으면 null)
    public EquipSlotDef Find(string slotId)
    {
        foreach (EquipSlotDef def in slots)
        {
            if (def != null && def.slotId == slotId)
            {
                return def;
            }
        }
        return null;
    }

    // slotId별 기본 별칭 테이블 (표준 5종 슬롯: 대상 본이 머리/상체척추/루트로 수렴)
    public static List<string> DefaultAliases(string slotId)
    {
        List<string> aliases = new List<string>();

        if (slotId == "head" || slotId == "overhead")
        {
            aliases.Add("head");
            aliases.Add("頭");
            aliases.Add("atama");
        }
        else if (slotId == "chest" || slotId == "back")
        {
            aliases.Add("spine2");
            aliases.Add("chest");
            aliases.Add("upperchest");
            aliases.Add("上半身2");
        }

        return aliases;
    }

    // slotId별 기본 Humanoid 본 (없으면 -1)
    public static int DefaultHumanoidBone(string slotId)
    {
        if (slotId == "head" || slotId == "overhead")
        {
            return (int)HumanBodyBones.Head;
        }
        if (slotId == "chest" || slotId == "back")
        {
            return (int)HumanBodyBones.Chest;
        }
        return -1;
    }
}
