using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Jukebox 프리팹 2차 정적 패치(에디터 1회성, 런타임 그리기 없음).
///
/// JukeboxView.prefab:
///  1) playInfo 좌측에 NowPlaying(현재 곡명) 복원 — 우측엔 볼륨 아이콘+슬라이더 유지
///  2) 재생/중지/정지 버튼을 정사각형으로 축소
///  3) ShuffleButton/RepeatButton 을 정사각형으로 만들고 아이콘 이미지(ShuffleIcon/RepeatIcon) 추가
///     (스프라이트는 Inspector에서 등록 → JukeboxView가 모드에 맞춰 스왑, 미등록 시 텍스트 폴백)
///
/// JukeboxEnvironmentView.prefab:
///  4) 각 SFX 카테고리 행(Row_*) 우측에 정사각형 Sample 버튼(▶) 추가 — 카테고리 내 랜덤 재생
///
/// 실행: 메뉴 Tools/Jukebox/Patch Layout v2 또는 -executeMethod JukeboxLayoutPatch2.Apply
/// </summary>
public static class JukeboxLayoutPatch2
{
    private const string MainPath = "Assets/Prefabs/UI/Jukebox/JukeboxView/Prefabs/JukeboxView.prefab";
    private const string SfxPath = "Assets/Prefabs/UI/Jukebox/JukeboxView/Prefabs/JukeboxEnvironmentView.prefab";

    private const float Sq = 34f; // 트랜스포트/모드 버튼 정사각 크기

    [MenuItem("Tools/Jukebox/Patch Layout v2")]
    public static void Apply()
    {
        PatchMain();
        PatchSfx();
    }

    // ── JukeboxView ───────────────────────────────────────────────────────────
    private static void PatchMain()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MainPath);
        if (root == null) { Debug.LogError($"[Patch2] 로드 실패: {MainPath}"); return; }
        try
        {
            GetStyle(root, out Sprite sprite, out TMP_FontAsset font);

            // 1) playInfo 좌측 NowPlaying 복원 (우측 VolumeIcon+MasterSlider는 그대로)
            Transform playInfo = FindDeep(root.transform, "playInfo");
            if (playInfo != null && FindChild(playInfo, "NowPlaying") == null)
            {
                TextMeshProUGUI np = JukeboxUi.Text("NowPlaying", playInfo, string.Empty, 16,
                    JukeboxUi.TextWhite, TextAlignmentOptions.MidlineLeft, font);
                JukeboxUi.Layout(np.gameObject, flexW: 1f, minW: 60f);
                np.transform.SetSiblingIndex(0); // 맨 왼쪽
            }

            // 2) 재생/중지/정지 정사각형
            MakeSquare(root, "PlayButton");
            MakeSquare(root, "PauseButton");
            MakeSquare(root, "StopButton");
            Transform left = FindDeep(root.transform, "TransportLeft");
            if (left != null)
            {
                HorizontalLayoutGroup hl = left.GetComponent<HorizontalLayoutGroup>();
                if (hl != null) hl.spacing = 6f;
            }

            // 3) 모드 버튼 정사각형 + 아이콘
            SetupModeButton(root, "ShuffleButton", "ShuffleIcon", "ShuffleLabel", "순차", sprite);
            SetupModeButton(root, "RepeatButton", "RepeatIcon", "RepeatLabel", "전곡", sprite);

            PrefabUtility.SaveAsPrefabAsset(root, MainPath);
            Debug.Log("[Patch2] JukeboxView 패치 완료: NowPlaying 복원 + 정사각 트랜스포트/모드 + 모드 아이콘.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void MakeSquare(GameObject root, string name)
    {
        Transform t = FindDeep(root.transform, name);
        if (t != null) JukeboxUi.Layout(t.gameObject, minW: Sq, prefW: Sq, minH: Sq, prefH: Sq, flexW: 0f);
    }

    private static void SetupModeButton(GameObject root, string btnName, string iconName, string labelName, string shortText, Sprite sprite)
    {
        Transform btn = FindDeep(root.transform, btnName);
        if (btn == null) return;
        JukeboxUi.Layout(btn.gameObject, minW: Sq, prefW: Sq, minH: Sq, prefH: Sq, flexW: 0f);

        // 짧은 폴백 텍스트(정사각에 맞게). 런타임에 JukeboxView가 갱신.
        Transform label = FindChild(btn, labelName);
        if (label != null)
        {
            TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = shortText;
        }

        // 아이콘 이미지(초기 비활성 — 스프라이트 등록 시 런타임에 켜짐)
        if (FindChild(btn, iconName) == null)
        {
            GameObject icon = JukeboxUi.Panel(iconName, btn, null, JukeboxUi.TextWhite);
            JukeboxUi.Stretch(icon, new Vector4(6f, 6f, 6f, 6f));
            Image img = icon.GetComponent<Image>();
            if (img != null) { img.raycastTarget = false; img.enabled = false; }
        }
    }

    // ── JukeboxEnvironmentView ──────────────────────────────────────────────────
    private static void PatchSfx()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SfxPath);
        if (root == null) { Debug.LogError($"[Patch2] 로드 실패: {SfxPath}"); return; }
        try
        {
            GetStyle(root, out Sprite sprite, out TMP_FontAsset font);

            Transform content = FindDeep(root.transform, "SfxContent");
            if (content == null) { Debug.LogError("[Patch2] SfxContent 없음"); return; }

            int added = 0;
            for (int i = 0; i < content.childCount; i++)
            {
                Transform row = content.GetChild(i);
                if (!row.name.StartsWith("Row_")) continue;
                if (FindChild(row, "Sample") != null) continue;

                Button sample = JukeboxUi.MakeButton("Sample", row, "▶", JukeboxUi.ButtonBg, 14, sprite, font);
                JukeboxUi.Layout(sample.gameObject, minW: 30f, prefW: 30f, minH: 30f, prefH: 30f, flexW: 0f);
                added++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, SfxPath);
            Debug.Log($"[Patch2] JukeboxEnvironmentView 패치 완료: Sample 버튼 {added}개 추가.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ── 공통 헬퍼 ────────────────────────────────────────────────────────────────
    private static void GetStyle(GameObject root, out Sprite sprite, out TMP_FontAsset font)
    {
        sprite = null;
        font = null;
        // 아무 버튼에서나 둥근 스프라이트/폰트 확보
        Transform anyBtn = FindFirstButton(root.transform);
        if (anyBtn != null)
        {
            Image img = anyBtn.GetComponent<Image>();
            if (img != null) sprite = img.sprite;
            TMP_Text txt = anyBtn.GetComponentInChildren<TMP_Text>(true);
            if (txt != null) font = txt.font;
        }
        if (font == null) font = TMP_Settings.defaultFontAsset;
    }

    private static Transform FindFirstButton(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (c.GetComponent<Button>() != null) return c;
            Transform found = FindFirstButton(c);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i).name == name) return parent.GetChild(i);
        }
        return null;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
