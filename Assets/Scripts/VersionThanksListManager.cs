using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class VersionThanksListManager : MonoBehaviour
{
    [SerializeField] private Transform contentParent; 
    [SerializeField] private GameObject sampleSlot;   
    [SerializeField] private TMP_Text leftTextRef;    
    [SerializeField] private TMP_Text rightTextRef;   

    [SerializeField] private List<string> thanksList = new List<string>();

    private void Awake()
    {
        // 1. 명단 추가 
        AddNamesToList();

        if (sampleSlot == null || thanksList.Count == 0) return;
        
        sampleSlot.SetActive(false);

        // 2. 2명씩 묶어서 처리
        for (int i = 0; i < thanksList.Count; i += 2)
        {
            GameObject clone = Instantiate(sampleSlot, contentParent);
            clone.SetActive(true);

            TMP_Text[] texts = clone.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in texts)
            {
                if (t.name == leftTextRef.name)
                {
                    t.text = thanksList[i];
                }
                else if (t.name == rightTextRef.name)
                {
                    t.text = (i + 1 < thanksList.Count) ? thanksList[i + 1] : "";
                }
            }
        }
    }

    private void AddNamesToList()
    {
        // Github - 총 개월 수 기준 : 실수로 핸들 쓰지 말것!
        thanksList.Add("釉薬_ゆうやく");
        thanksList.Add("いいあか");
        thanksList.Add("Zn Hey");
        thanksList.Add("Copper Brass");
        thanksList.Add("96mochi.");
        thanksList.Add("スターフルーツ・カブ");
        thanksList.Add("fu");
        thanksList.Add("飛.");
        thanksList.Add("しぐなす");
        thanksList.Add("NOT FOUND 404");

        // TODO : 링크도 걸고할겸 둘로 나누기. Patreon - Github 밑에 둬도 되나...?
        thanksList.Add("Tybs");
    }
}