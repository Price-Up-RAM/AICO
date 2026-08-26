using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 주크박스 곡에 한글 태그를 붙인다 (Phase 5).
//
// 왜 필요한가: 씬 playlist의 곡 이름이 전부 영문(campfire/Lofi1/Lofi2/rain)인데
// MR의 1급 입력인 STT는 한 언어만 인식한다. 한국어로 말하면 이름 매칭도 태그 매칭도
// 걸리지 않아 "모닥불 틀어줘"가 영원히 실패한다 (2026-08-25 실측).
//
// MRJukebox.PlayByTag는 태그 부분 일치 + 매칭 곡 중 랜덤 선택이라,
// 태그에 한글을 넣어두면 기존 경로가 그대로 동작한다.
//
// 손으로 인스펙터를 고치지 않고 스크립트로 만든 이유는 재현 가능해야 하기 때문이다
// (Kickoff Guide 7-1 F — 결정이 가장 잘 새는 곳이 씬이다).
public static class MRJukeboxKoreanTags
{
    // 곡 이름(소문자 비교) → 추가할 한글 태그.
    // 기존 태그는 지우지 않고 없는 것만 더한다.
    private static readonly Dictionary<string, string[]> KoreanTags = new Dictionary<string, string[]>
    {
        { "campfire", new string[] { "모닥불", "캠프파이어", "장작" } },
        { "lofi1",    new string[] { "로파이", "공부", "집중" } },
        { "lofi2",    new string[] { "로파이", "공부", "집중" } },
        { "rain",     new string[] { "비", "빗소리", "빗물" } }
    };

    [MenuItem("Tools/MR/주크박스 한글 태그 부여")]
    public static void ApplyKoreanTags()
    {
        MRJukebox jukebox = FindJukeboxInScene();
        if (jukebox == null)
        {
            Debug.LogError("[MRJukeboxKoreanTags] 씬에서 MRJukebox를 찾지 못했다. MR 씬을 열고 다시 실행할 것.");
            return;
        }

        SerializedObject so = new SerializedObject(jukebox);
        SerializedProperty playlist = so.FindProperty("playlist");
        if (playlist == null || !playlist.isArray)
        {
            Debug.LogError("[MRJukeboxKoreanTags] playlist 프로퍼티를 찾지 못했다.");
            return;
        }

        int touched = 0;
        for (int i = 0; i < playlist.arraySize; i++)
        {
            SerializedProperty track = playlist.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = track.FindPropertyRelative("trackName");
            SerializedProperty tagsProp = track.FindPropertyRelative("tags");
            if (nameProp == null || tagsProp == null)
            {
                continue;
            }

            string key = (nameProp.stringValue ?? "").ToLower();
            if (!KoreanTags.ContainsKey(key))
            {
                Debug.Log($"[MRJukeboxKoreanTags] '{nameProp.stringValue}' — 매핑 없음, 건너뜀");
                continue;
            }

            List<string> existing = new List<string>();
            for (int t = 0; t < tagsProp.arraySize; t++)
            {
                existing.Add(tagsProp.GetArrayElementAtIndex(t).stringValue);
            }

            List<string> added = new List<string>();
            string[] wanted = KoreanTags[key];
            for (int w = 0; w < wanted.Length; w++)
            {
                if (existing.Contains(wanted[w]))
                {
                    continue;
                }

                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = wanted[w];
                existing.Add(wanted[w]);
                added.Add(wanted[w]);
            }

            if (added.Count > 0)
            {
                touched++;
                Debug.Log($"[MRJukeboxKoreanTags] '{nameProp.stringValue}' ← 태그 추가 [{string.Join(", ", added.ToArray())}] | 최종 [{string.Join(", ", existing.ToArray())}]");
            }
            else
            {
                Debug.Log($"[MRJukeboxKoreanTags] '{nameProp.stringValue}' — 이미 전부 있음");
            }
        }

        if (touched == 0)
        {
            Debug.Log("[MRJukeboxKoreanTags] 변경 없음. 씬을 저장할 필요 없다.");
            return;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(jukebox);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(jukebox.gameObject.scene);
        Debug.Log($"[MRJukeboxKoreanTags] {touched}곡 갱신 완료. **씬을 저장할 것(Ctrl+S)** — 저장하지 않으면 반영되지 않는다.");
    }

    // 비활성 오브젝트에 붙어 있어도 찾는다.
    private static MRJukebox FindJukeboxInScene()
    {
        MRJukebox[] found = Resources.FindObjectsOfTypeAll<MRJukebox>();
        for (int i = 0; i < found.Length; i++)
        {
            MRJukebox item = found[i];
            if (item == null || item.gameObject == null)
            {
                continue;
            }
            if (!item.gameObject.scene.IsValid())
            {
                continue;
            }
            return item;
        }
        return null;
    }
}
