using System;
using System.IO;
using UnityEngine;
using Cleverous.VaultInventory;

// 캐릭터 1명의 인벤토리 상태 + 악세서리 장착 상태를 한 파일에 묶어 저장하기 위한 래퍼
[Serializable]
public class CharSaveData
{
    public InventoryState inventoryState;
    public EquippedStateList equippedState;
}

// 캐릭터를 Vault 인벤토리 시스템의 소유자(IUseInventory)로 만들어주는 컴포넌트.
// Inventory 컴포넌트와 같은 GameObject(캐릭터 프리팹 루트)에 붙인다.
[RequireComponent(typeof(Inventory))]
public class CharInventoryOwner : MonoBehaviour, IUseInventory
{
    public Inventory Inventory { get => m_inventory; set => m_inventory = value; }
    [SerializeField] private Inventory m_inventory;

    [Header("[Starting Items]")]
    [SerializeField] private RootItem[] startingItems; // 저장된 파일이 없을 때(첫 실행)만 사용되는 기본 아이템들
    [SerializeField] private int[] startingAmounts; // startingItems와 같은 인덱스로 매칭되는 개수

    public Transform MyTransform => transform;

    private string savePath;
    private string charcode;

    private void Start()
    {
        if (m_inventory == null)
        {
            m_inventory = GetComponent<Inventory>();
        }

        charcode = GetComponent<CharAttributes>()?.charcode;
        savePath = string.IsNullOrEmpty(charcode) == false
            ? Path.Combine(Application.persistentDataPath, $"inventory_{charcode}.json")
            : null;

        if (string.IsNullOrEmpty(savePath) == false && File.Exists(savePath))
        {
            // 저장된 상태가 있으면 인벤토리 + 장착 상태를 그대로 복원
            string json = File.ReadAllText(savePath);
            CharSaveData saveData = JsonUtility.FromJson<CharSaveData>(json);

            Inventory.Initialize(this, saveData.inventoryState);
            AccessoryManager.Instance.RestoreEquippedState(gameObject, saveData.equippedState);
        }
        else
        {
            // 저장된 상태가 없으면(첫 실행) 인스펙터의 시작 아이템으로 초기화
            Inventory.Initialize(this, true);

            for (int i = 0; i < startingItems.Length; i++)
            {
                if (startingItems[i] == null)
                {
                    continue;
                }

                int amount = (startingAmounts != null && i < startingAmounts.Length) ? startingAmounts[i] : 1;
                Inventory.DoAdd(new RootItemStack(startingItems[i], amount));
            }
        }

        // InventoryUi(BindOnPlayerSpawn 체크된 UI)가 이 이벤트를 구독해서 자동으로 이 캐릭터의 인벤토리를 표시한다.
        VaultInventory.OnPlayerSpawn?.Invoke(this);
    }

    private void OnDestroy()
    {
        SaveInventory();
    }

    // 현재 인벤토리 + 장착 상태를 캐릭터별 JSON 파일로 저장
    public void SaveInventory()
    {
        if (string.IsNullOrEmpty(savePath) || Inventory == null || Inventory.IsInitialized == false)
        {
            return;
        }

        CharSaveData saveData = new CharSaveData();
        saveData.inventoryState = Inventory.ToState();
        saveData.equippedState = AccessoryManager.Instance.GetEquippedState(charcode);

        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(savePath, json);
    }
}
