using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 환경음(SFX) 팝업. JukeboxView의 "환경음" 버튼으로 열린다.
/// SFX 행(토글 + 라벨 + 볼륨 + min/max 간격)을 별도 프리팹 없이 이 뷰 안에서 인라인으로 빌드한다.
/// 켜진 SFX는 min/max 범위 안 랜덤 시각에 1회씩 one-shot 재생한다.
/// 설정은 JukeboxView와 같은 jukebox_settings.json을 공유한다.
/// </summary>
public class JukeboxEnvironmentView : MonoBehaviour
{
    [Header("Style")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Vector2 panelSize = new Vector2(440f, 460f);
    [SerializeField] private Sprite panelSprite;

    private bool built;
    private bool runtimeReady;
    private JukeboxSaveData save;

    private RectTransform sfxContent;
    private AudioSource sfxSource;

    private readonly Dictionary<string, JukeboxCatalog.TrackDef> defsById = new Dictionary<string, JukeboxCatalog.TrackDef>();
    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, float> sfxNextTime = new Dictionary<string, float>();

    private void Awake()
    {
        foreach (JukeboxCatalog.TrackDef d in JukeboxCatalog.Sfx)
        {
            defsById[d.id] = d;
        }

        save = JukeboxSettings.Load();
        SetupAudio();

        if (HasBakedHierarchy())
        {
            BindExisting();
        }
        else
        {
            Build();
        }

        WireSfx();
        ApplyInitialSchedule();
        runtimeReady = true;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        save = JukeboxSettings.Load(); // 마스터/타 설정 최신화
    }

    public void Hide() { gameObject.SetActive(false); }

    private void Update()
    {
        if (!runtimeReady || sfxNextTime.Count == 0)
        {
            return;
        }
        float now = Time.unscaledTime;
        List<string> ids = new List<string>(sfxNextTime.Keys);
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            if (now >= sfxNextTime[id])
            {
                PlaySfxOneShot(id);
                sfxNextTime[id] = now + RandomInterval(id);
            }
        }
    }

    // ── 오디오 ──────────────────────────────────────────────────────────────────
    private float Master => save != null ? save.masterVolume : 0.8f;

    private void SetupAudio()
    {
        GameObject go = new GameObject("Sfx");
        go.transform.SetParent(transform, false);
        sfxSource = go.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
    }

    private int RandomInterval(string id)
    {
        JukeboxTrackState s = JukeboxSettings.GetState(save, id);
        int min = Mathf.Max(1, s.minInterval);
        int max = Mathf.Max(min, s.maxInterval);
        return UnityEngine.Random.Range(min, max + 1);
    }

    private bool ClipAvailable(JukeboxCatalog.TrackDef def)
    {
        return File.Exists(Path.Combine(Application.streamingAssetsPath, "Jukebox", def.file));
    }

    private void PlaySfxOneShot(string id)
    {
        if (!defsById.TryGetValue(id, out JukeboxCatalog.TrackDef def))
        {
            return;
        }
        GetClip(def, clip =>
        {
            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip, Master * JukeboxSettings.GetState(save, id).volume);
            }
        });
    }

    private void GetClip(JukeboxCatalog.TrackDef def, Action<AudioClip> onLoaded)
    {
        if (clipCache.TryGetValue(def.id, out AudioClip cached))
        {
            onLoaded?.Invoke(cached);
            return;
        }
        StartCoroutine(LoadClipCoroutine(def, onLoaded));
    }

    private IEnumerator LoadClipCoroutine(JukeboxCatalog.TrackDef def, Action<AudioClip> onLoaded)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, "Jukebox", def.file);
        if (!File.Exists(fullPath))
        {
            onLoaded?.Invoke(null);
            yield break;
        }
        string url = new Uri(fullPath).AbsoluteUri;
        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, GuessAudioType(def.file)))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[JukeboxEnv] 클립 로드 실패: {def.file} ({req.error})");
                onLoaded?.Invoke(null);
                yield break;
            }
            AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip != null)
            {
                clipCache[def.id] = clip;
            }
            onLoaded?.Invoke(clip);
        }
    }

    private static AudioType GuessAudioType(string file)
    {
        switch (Path.GetExtension(file).ToLowerInvariant())
        {
            case ".wav": return AudioType.WAV;
            case ".mp3": return AudioType.MPEG;
            default: return AudioType.OGGVORBIS;
        }
    }

    // ── 행 연결(인라인 빌드된 행을 찾아 값/리스너 바인딩) ─────────────────────────
    private void WireSfx()
    {
        foreach (JukeboxCatalog.TrackDef def in JukeboxCatalog.Sfx)
        {
            Transform rowT = FindDeep(transform, "Row_" + def.id);
            if (rowT == null)
            {
                continue;
            }

            Toggle tg = FindIn<Toggle>(rowT, "Toggle");
            Slider vol = FindIn<Slider>(rowT, "Volume");
            TMP_InputField mn = FindIn<TMP_InputField>(rowT, "MinInput");
            TMP_InputField mx = FindIn<TMP_InputField>(rowT, "MaxInput");

            JukeboxTrackState st = JukeboxSettings.GetState(save, def.id);
            bool avail = ClipAvailable(def);
            string id = def.id;

            if (tg != null)
            {
                tg.onValueChanged.RemoveAllListeners();
                tg.SetIsOnWithoutNotify(st.enabled);
                tg.interactable = avail;
                tg.onValueChanged.AddListener(on => OnSfxToggle(id, on));
            }
            if (vol != null)
            {
                vol.onValueChanged.RemoveAllListeners();
                vol.SetValueWithoutNotify(st.volume);
                vol.interactable = avail;
                vol.onValueChanged.AddListener(v => OnSfxVolume(id, v));
            }
            if (mn != null && mx != null)
            {
                TMP_InputField cmn = mn;
                TMP_InputField cmx = mx;
                mn.onEndEdit.RemoveAllListeners();
                mx.onEndEdit.RemoveAllListeners();
                mn.SetTextWithoutNotify(st.minInterval.ToString());
                mx.SetTextWithoutNotify(st.maxInterval.ToString());
                mn.onEndEdit.AddListener(_ => OnSfxInterval(id, cmn, cmx));
                mx.onEndEdit.AddListener(_ => OnSfxInterval(id, cmn, cmx));
            }
        }
    }

    private void ApplyInitialSchedule()
    {
        foreach (JukeboxCatalog.TrackDef def in JukeboxCatalog.Sfx)
        {
            JukeboxTrackState st = JukeboxSettings.GetState(save, def.id);
            if (st.enabled && ClipAvailable(def))
            {
                sfxNextTime[def.id] = Time.unscaledTime + RandomInterval(def.id);
            }
        }
    }

    private void OnSfxToggle(string id, bool on)
    {
        save = JukeboxSettings.Load();
        JukeboxSettings.GetState(save, id).enabled = on;
        JukeboxSettings.Save(save);
        if (on)
        {
            sfxNextTime[id] = Time.unscaledTime + RandomInterval(id);
        }
        else
        {
            sfxNextTime.Remove(id);
        }
    }

    private void OnSfxVolume(string id, float v)
    {
        save = JukeboxSettings.Load();
        JukeboxSettings.GetState(save, id).volume = v;
        JukeboxSettings.Save(save);
    }

    private void OnSfxInterval(string id, TMP_InputField mn, TMP_InputField mx)
    {
        int min = ParseInt(mn, 30);
        int max = ParseInt(mx, 60);
        if (max < min)
        {
            max = min;
            if (mx != null)
            {
                mx.SetTextWithoutNotify(max.ToString());
            }
        }
        save = JukeboxSettings.Load();
        JukeboxTrackState st = JukeboxSettings.GetState(save, id);
        st.minInterval = min;
        st.maxInterval = max;
        JukeboxSettings.Save(save);
    }

    private static int ParseInt(TMP_InputField field, int fallback)
    {
        if (field != null && int.TryParse(field.text, out int v) && v >= 0)
        {
            return v;
        }
        return fallback;
    }

    private static T FindIn<T>(Transform root, string name) where T : Component
    {
        Transform t = FindDeep(root, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    // ── 빌드 / 바인드 ──────────────────────────────────────────────────────────
    private bool HasBakedHierarchy()
    {
        return transform.Find("Body") != null;
    }

    private void BindExisting()
    {
        built = true;
        sfxContent = FindDeep(transform, "SfxContent") as RectTransform;
        BindButton("CloseButton", Hide);
    }

    private void BindButton(string name, UnityEngine.Events.UnityAction action)
    {
        Transform t = FindDeep(transform, name);
        Button button = t != null ? t.GetComponent<Button>() : null;
        if (button != null)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }
            Transform found = FindDeep(child, name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

#if UNITY_EDITOR
    public void EditorBuild(Sprite sprite, TMP_FontAsset fontAsset)
    {
        panelSprite = sprite;
        font = fontAsset;
        if (save == null)
        {
            save = new JukeboxSaveData();
        }
        Build();
    }
#endif

    private void Build()
    {
        if (built)
        {
            return;
        }
        built = true;

        RectTransform rootRect = transform as RectTransform;
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }
        rootRect.sizeDelta = panelSize;

        Image rootBg = gameObject.GetComponent<Image>();
        if (rootBg == null)
        {
            rootBg = gameObject.AddComponent<Image>();
        }
        JukeboxUi.ApplyRounded(rootBg, panelSprite, JukeboxUi.RootBg);

        VerticalLayoutGroup rootLayout = gameObject.GetComponent<VerticalLayoutGroup>();
        if (rootLayout == null)
        {
            rootLayout = gameObject.AddComponent<VerticalLayoutGroup>();
        }
        rootLayout.padding = new RectOffset(14, 14, 14, 14);
        rootLayout.spacing = 10f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        BuildHeader(transform);
        BuildBody(transform);
        BuildSfxRows();
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = JukeboxUi.Panel("Header", parent, panelSprite, JukeboxUi.HeaderBg);
        JukeboxUi.Layout(header, minH: 44f, prefH: 44f);
        JukeboxUi.Row(header, 8f, padL: 12, padR: 8);

        TextMeshProUGUI title = JukeboxUi.Text("Title", header.transform, "환경음 (SFX)", 18, JukeboxUi.TextWhite, TextAlignmentOptions.MidlineLeft, font);
        JukeboxUi.Layout(title.gameObject, flexW: 1f);

        Button close = JukeboxUi.MakeButton("CloseButton", header.transform, "×", JukeboxUi.HeaderBg, 24, panelSprite, font);
        close.onClick.AddListener(Hide);
        JukeboxUi.Layout(close.gameObject, prefW: 32f, minW: 32f);
    }

    private void BuildBody(Transform parent)
    {
        GameObject body = JukeboxUi.Obj("Body", parent);
        JukeboxUi.Layout(body, minH: 200f, flexH: 1f);

        ScrollRect scroll = body.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        GameObject viewport = JukeboxUi.Obj("Viewport", body.transform);
        JukeboxUi.Stretch(viewport, Vector4.zero);
        viewport.AddComponent<RectMask2D>();

        GameObject content = JukeboxUi.Obj("SfxContent", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        JukeboxUi.Column(content, 4f);
        AddVerticalFitter(content);

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;

        sfxContent = contentRect;
    }

    private void BuildSfxRows()
    {
        if (sfxContent == null)
        {
            return;
        }
        foreach (JukeboxCatalog.TrackDef def in JukeboxCatalog.Sfx)
        {
            GameObject row = JukeboxUi.Panel("Row_" + def.id, sfxContent, panelSprite, JukeboxUi.RowBg);
            JukeboxUi.Row(row, 8f, padL: 8, padR: 8, padT: 2, padB: 2);
            JukeboxUi.Layout(row, minH: 36f, prefH: 36f);

            Toggle tg = JukeboxUi.MakeToggle("Toggle", row.transform, panelSprite);
            JukeboxUi.Layout(tg.gameObject, prefW: 22f, minW: 22f, prefH: 22f, minH: 22f);

            TextMeshProUGUI label = JukeboxUi.Text("Label", row.transform, def.display, 14, JukeboxUi.TextWhite, TextAlignmentOptions.MidlineLeft, font);
            JukeboxUi.Layout(label.gameObject, flexW: 1f, minW: 70f);

            Slider vol = JukeboxUi.MakeSlider("Volume", row.transform, panelSprite);
            JukeboxUi.Layout(vol.gameObject, prefW: 90f, minW: 70f);

            GameObject ig = JukeboxUi.Obj("Interval", row.transform);
            JukeboxUi.Row(ig, 4f);
            JukeboxUi.Layout(ig, prefW: 120f, minW: 120f);

            TMP_InputField mn = JukeboxUi.NumberInput("MinInput", ig.transform, panelSprite, font);
            JukeboxUi.Layout(mn.gameObject, prefW: 38f, minW: 38f, prefH: 26f, minH: 26f);

            JukeboxUi.Text("Tilde", ig.transform, "~", 14, JukeboxUi.TextMuted, TextAlignmentOptions.Center, font);

            TMP_InputField mx = JukeboxUi.NumberInput("MaxInput", ig.transform, panelSprite, font);
            JukeboxUi.Layout(mx.gameObject, prefW: 38f, minW: 38f, prefH: 26f, minH: 26f);

            JukeboxUi.Text("Unit", ig.transform, "s", 13, JukeboxUi.TextMuted, TextAlignmentOptions.MidlineLeft, font);
        }
    }

    private static void AddVerticalFitter(GameObject go)
    {
        ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = go.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
}
