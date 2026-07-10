using System;
using System.Collections.Generic;
using UnityEngine;

// 키 → 악세서리 키 매핑 (데모에서 숫자키로 교체)
[Serializable]
public class EquipBinding
{
    public KeyCode key;         // 입력 키
    public string accessoryKey; // 카탈로그 키
}

// EquipSystem 데모 컨트롤러: 대상 캐릭터에 키 입력으로 악세서리 장착·교체·해제 (데모씬 테스트용)
public class EquipDemoController : MonoBehaviour
{
    public GameObject target;  // 장착 대상 캐릭터
    public List<EquipBinding> bindings = new List<EquipBinding>();  // 숫자키 → 악세서리 교체
    public KeyCode unequipKey = KeyCode.J;  // 해제 키
    public string unequipSlotId = "head1";  // 해제할 슬롯

    public bool equipOnStart = false;  // 시작 시 자동 장착
    public string accessoryKey = "hairpin_placeholder";  // 시작/기본 장착 키
    public string slotId = "hairpin";  // (호환용) 기본 슬롯

    private string lastMessage = "";  // 마지막 장착/해제 결과 (OnGUI 표시용)

    // 시작 시: 타겟 자동 탐색(미지정 시 씬에서 EquipSocket 보유 캐릭터) + 자동 장착
    private void Start()
    {
        if (target == null)
        {
            EquipSocket anySocket = FindObjectOfType<EquipSocket>(true);
            if (anySocket != null)
            {
                Animator anim = anySocket.GetComponentInParent<Animator>();
                if (anim != null)
                {
                    target = anim.gameObject;
                }
                else
                {
                    target = anySocket.transform.root.gameObject;
                }
                Debug.Log($"[EquipDemoController] 자동 타겟: {target.name}");
            }
        }

        if (equipOnStart)
        {
            DoEquip(accessoryKey);
        }
    }

    // 키 입력 처리 (바인딩 교체 + 해제)
    private void Update()
    {
        foreach (EquipBinding binding in bindings)
        {
            if (binding != null && Input.GetKeyDown(binding.key))
            {
                DoEquip(binding.accessoryKey);
            }
        }

        if (Input.GetKeyDown(unequipKey))
        {
            DoUnequip(unequipSlotId);
        }
    }

    // 장착 실행 — 실패해도 침묵하지 않고 사유를 남긴다
    private void DoEquip(string key)
    {
        if (target == null)
        {
            lastMessage = "target 미지정 — 인스펙터에서 캐릭터를 지정하거나, 씬에 EquipSocket 있는 캐릭터가 필요합니다";
            Debug.LogWarning("[EquipDemoController] " + lastMessage);
            return;
        }

        if (EquipManager.Instance == null)
        {
            lastMessage = "씬에 EquipManager 없음 — 빈 GameObject에 EquipManager를 추가하세요";
            Debug.LogWarning("[EquipDemoController] " + lastMessage);
            return;
        }

        string reason;
        bool ok = EquipManager.Instance.Equip(target, key, out reason);
        if (ok)
        {
            lastMessage = $"장착: {key}";
        }
        else
        {
            lastMessage = $"장착 실패: {key} — {reason}";
        }
        Debug.Log($"[EquipDemoController] {lastMessage} on {target.name}");
    }

    // 해제 실행
    private void DoUnequip(string slot)
    {
        if (target == null)
        {
            lastMessage = "target 미지정 — 해제 불가";
            Debug.LogWarning("[EquipDemoController] " + lastMessage);
            return;
        }

        if (EquipManager.Instance == null)
        {
            lastMessage = "씬에 EquipManager 없음";
            Debug.LogWarning("[EquipDemoController] " + lastMessage);
            return;
        }

        EquipManager.Instance.Unequip(target, slot);
        lastMessage = $"해제: {slot}";
        Debug.Log($"[EquipDemoController] {lastMessage} on {target.name}");
    }

    // 런타임 상태 오버레이 — 실제 타겟/바인딩/마지막 결과(거부 사유 포함)를 표시.
    // (씬에 박힌 정적 안내 Text는 런타임 상태가 아니므로 이것이 진실)
    private void OnGUI()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        string targetName = "(없음 — 씬에 소켓 있는 캐릭터 필요)";
        if (target != null)
        {
            targetName = target.name;
        }
        sb.AppendLine($"EquipDemo target: {targetName}");

        foreach (EquipBinding binding in bindings)
        {
            if (binding != null)
            {
                sb.AppendLine($"{binding.key}: {binding.accessoryKey}");
            }
        }
        sb.AppendLine($"{unequipKey}: 해제 ({unequipSlotId})");

        if (string.IsNullOrEmpty(lastMessage) == false)
        {
            sb.AppendLine("▶ " + lastMessage);
        }

        GUI.Label(new Rect(10f, Screen.height - 170f, 700f, 160f), sb.ToString());
    }
}
