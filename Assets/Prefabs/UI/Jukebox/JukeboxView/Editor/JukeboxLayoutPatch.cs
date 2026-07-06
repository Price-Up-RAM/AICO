using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// JukeboxView.prefab 을 "에디터에서 1회성"으로 재배치하는 패치.
/// (런타임 그리기 없음 — 프리팹 자체를 정적으로 수정·저장한다.)
///
/// 하는 일:
///  1) TransportRow 를 좌/우 컨테이너로 분리
///     - 좌(TransportLeft): 기존 재생/중지/정지 버튼
///     - 우(TransportRight): 신규 ShuffleButton(순차/랜덤), RepeatButton(한곡/전곡/없음)
///     - 기존 ModeButton("다음곡"), OptionRow("랜덤재생" 토글) 제거
///  2) playInfo 우측에 볼륨 아이콘(placeholder square) + 볼륨 슬라이더(MasterSlider 이동) 배치
///     - 기존 NowPlaying 제거, VolumeRow(하단 VOL 행) 제거
///
/// 스타일은 JukeboxUi 팩토리를 재사용해 기존 위젯과 통일한다.
/// 실행: 메뉴 Tools/Jukebox/Patch Layout (buttons + volume) 또는 -executeMethod JukeboxLayoutPatch.Apply
/// </summary>
public static class JukeboxLayoutPatch
{
    private const string PrefabPath =
        "Assets/Prefabs/UI/Jukebox/JukeboxView/Prefabs/JukeboxView.prefab";

    [MenuItem("Tools/Jukebox/Patch Layout (buttons + volume)")]
    public static void Apply()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[JukeboxLayoutPatch] 프리팹 로드 실패: {PrefabPath}");
            return;
        }

        try
        {
            // 중복 실행 가드: 이미 패치된 프리팹이면 그만둔다(컨테이너/버튼 중복 생성 방지).
            if (FindDeep(root.transform, "TransportLeft") != null)
            {
                Debug.LogWarning("[JukeboxLayoutPatch] 이미 패치됨(TransportLeft 존재) — 건너뜀.");
                return;
            }

            // 스타일 소스: 기존 버튼에서 둥근 스프라이트/폰트를 재사용.
            Transform playBtn = FindDeep(root.transform, "PlayButton");
            Sprite sprite = null;
            TMP_FontAsset font = null;
            if (playBtn != null)
            {
                Image img = playBtn.GetComponent<Image>();
                if (img != null) sprite = img.sprite;
                TMP_Text txt = playBtn.GetComponentInChildren<TMP_Text>(true);
                if (txt != null) font = txt.font;
            }
            if (font == null) font = TMP_Settings.defaultFontAsset;

            PatchTransport(root, sprite, font);
            PatchPlayInfo(root, sprite, font);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[JukeboxLayoutPatch] 완료: TransportRow 좌/우 분리 + 볼륨 슬라이더 playInfo 이동.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 1) TransportRow 재배치 ────────────────────────────────────────────────
    private static void PatchTransport(GameObject root, Sprite sprite, TMP_FontAsset font)
    {
        Transform transport = FindDeep(root.transform, "TransportRow");
        if (transport == null)
        {
            Debug.LogError("[JukeboxLayoutPatch] TransportRow 없음");
            return;
        }

        // 좌 컨테이너: 재생/중지/정지 이동
        GameObject left = JukeboxUi.Obj("TransportLeft", transport);
        HorizontalLayoutGroup leftHl = JukeboxUi.Row(left, 6f);
        leftHl.childAlignment = TextAnchor.MiddleLeft;
        leftHl.childForceExpandHeight = true;
        JukeboxUi.Layout(left, flexW: 1f, minH: 30f);

        MoveInto(transport, "PlayButton", left.transform, 44f);
        MoveInto(transport, "PauseButton", left.transform, 44f);
        MoveInto(transport, "StopButton", left.transform, 44f);

        // 우 컨테이너: 순차/랜덤, 반복 모드
        GameObject rightGo = JukeboxUi.Obj("TransportRight", transport);
        HorizontalLayoutGroup rightHl = JukeboxUi.Row(rightGo, 6f);
        rightHl.childAlignment = TextAnchor.MiddleRight;
        rightHl.childForceExpandHeight = true;
        JukeboxUi.Layout(rightGo, minH: 30f);

        Button shuffle = JukeboxUi.MakeButton("ShuffleButton", rightGo.transform, "순차재생",
            JukeboxUi.ButtonBg, 13f, sprite, font);
        RenameLabel(shuffle, "ShuffleLabel");
        JukeboxUi.Layout(shuffle.gameObject, prefW: 86f, minW: 78f, prefH: 30f, minH: 30f);

        Button repeat = JukeboxUi.MakeButton("RepeatButton", rightGo.transform, "전곡반복",
            JukeboxUi.ButtonBg, 13f, sprite, font);
        RenameLabel(repeat, "RepeatLabel");
        JukeboxUi.Layout(repeat.gameObject, prefW: 86f, minW: 78f, prefH: 30f, minH: 30f);

        // 폐지: 기존 모드 버튼 / 랜덤재생 옵션 행
        DestroyChild(transport, "ModeButton");
        DestroyDeep(root.transform, "OptionRow");
    }

    // ── 2) playInfo 볼륨 배치 ─────────────────────────────────────────────────
    private static void PatchPlayInfo(GameObject root, Sprite sprite, TMP_FontAsset font)
    {
        Transform playInfo = FindDeep(root.transform, "playInfo");
        if (playInfo == null)
        {
            Debug.LogError("[JukeboxLayoutPatch] playInfo 없음");
            return;
        }

        // 현재 재생곡 표시 제거
        DestroyDeep(playInfo, "NowPlaying");

        // 볼륨 아이콘(placeholder): 20x20 박스 + 2px inset 된 하위 이미지(사용자가 스프라이트 교체)
        GameObject icon = JukeboxUi.Obj("VolumeIcon", playInfo);
        JukeboxUi.Layout(icon, minW: 20f, prefW: 20f, minH: 20f, prefH: 20f);
        GameObject iconImg = JukeboxUi.Panel("Icon", icon.transform, null, JukeboxUi.TextWhite);
        JukeboxUi.Stretch(iconImg, new Vector4(2f, 2f, 2f, 2f));
        Image iconImage = iconImg.GetComponent<Image>();
        if (iconImage != null) iconImage.raycastTarget = false;

        // 볼륨 슬라이더: 기존 MasterSlider 를 그대로 이동(재사용).
        Transform master = FindDeep(root.transform, "MasterSlider");
        if (master != null)
        {
            master.SetParent(playInfo, false);
            // flexW:0 → 슬라이더가 폭을 독차지하지 않게 해 [아이콘][슬라이더]가 playInfo 우측에 붙도록.
            JukeboxUi.Layout(master.gameObject, prefW: 150f, minW: 120f, prefH: 16f, minH: 16f, flexW: 0f);
        }
        else
        {
            Debug.LogWarning("[JukeboxLayoutPatch] MasterSlider 없음 — 볼륨 슬라이더 이동 생략");
        }

        // 하단 VOL 행 폐지 (MasterSlider 는 위에서 이미 빼냈으므로 안전)
        DestroyDeep(root.transform, "VolumeRow");
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────
    private static void MoveInto(Transform from, string childName, Transform target, float minW)
    {
        Transform t = FindChild(from, childName);
        if (t == null) t = FindDeep(from, childName);
        if (t == null) return;
        t.SetParent(target, false);
        JukeboxUi.Layout(t.gameObject, minW: minW); // 폭 여유 확보(좌 컨테이너에서 균등 분배)
    }

    private static void RenameLabel(Button button, string labelName)
    {
        TMP_Text txt = button.GetComponentInChildren<TMP_Text>(true);
        if (txt != null) txt.gameObject.name = labelName;
    }

    private static void DestroyChild(Transform parent, string name)
    {
        Transform t = FindChild(parent, name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }

    private static void DestroyDeep(Transform root, string name)
    {
        Transform t = FindDeep(root, name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
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
