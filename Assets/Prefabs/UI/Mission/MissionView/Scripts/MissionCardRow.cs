using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 미션 카드 1장. 계층은 MissionView가 구성, 이 컴포넌트는 바인딩 + 렌더 + 도장/서랍/보상셀 동작.
// 카드 어디를 눌러도: 수령 가능하면 수령, 아니면 보상 상세 서랍 토글.
public class MissionCardRow : MonoBehaviour, IPointerClickHandler
{
    private static readonly Color CardNormal = new Color(0.153f, 0.169f, 0.204f, 1f); // 미수행
    private static readonly Color CardClaimable = new Color(0.16f, 0.30f, 0.21f, 1f); // 수령 가능: 초록빛 강조
    private static readonly Color CardDone = new Color(0.105f, 0.115f, 0.135f, 1f);   // 달성: 더 어둡게(완료)

    [SerializeField] private Image background;
    [SerializeField] private TMP_Text description;
    [SerializeField] private Image gaugeFill;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text tierLabel;
    [SerializeField] private Image rewardCellImage;
    [SerializeField] private Button rewardCellButton;
    [SerializeField] private CanvasGroup rewardContentGroup;
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TMP_Text rewardAmount;
    [SerializeField] private RectTransform drawer;
    [SerializeField] private CanvasGroup drawerGroup;
    [SerializeField] private RectTransform drawerContent;
    [SerializeField] private GameObject stamp;
    [SerializeField] private CanvasGroup stampGroup;

    private MissionInfo def;
    private MissionReward currentReward;
    private readonly List<int> cycleKinds = new List<int>();
    private int cycleIndex;
    private Coroutine cycleCo;
    private bool drawerOpen;
    private bool bound;

    private Action<string> onClaim;
    private Action<MissionCardRow> onDrawerOpen;

    public string MissionId => def != null ? def.id : null;

    public void BindExisting()
    {
        if (bound)
        {
            return;
        }

        // 인스펙터에 등록된 참조 우선, 비어 있을 때만 이름으로 탐색(fallback).
        background = background != null ? background : GetComponent<Image>();
        description = description != null ? description : MissionUi.FindComponent<TMP_Text>(transform, "Description");
        gaugeFill = gaugeFill != null ? gaugeFill : MissionUi.FindComponent<Image>(transform, "GaugeFill");
        progressLabel = progressLabel != null ? progressLabel : MissionUi.FindComponent<TMP_Text>(transform, "ProgressLabel");
        tierLabel = tierLabel != null ? tierLabel : MissionUi.FindComponent<TMP_Text>(transform, "TierLabel");

        Transform cellT = MissionUi.FindDeepChild(transform, "RewardCell");
        if (cellT != null)
        {
            rewardCellImage = rewardCellImage != null ? rewardCellImage : cellT.GetComponent<Image>();
            rewardCellButton = rewardCellButton != null ? rewardCellButton : cellT.GetComponent<Button>();
            rewardContentGroup = rewardContentGroup != null ? rewardContentGroup : MissionUi.FindComponent<CanvasGroup>(cellT, "Content");
            rewardIcon = rewardIcon != null ? rewardIcon : MissionUi.FindComponent<Image>(cellT, "Icon");
            rewardAmount = rewardAmount != null ? rewardAmount : MissionUi.FindComponent<TMP_Text>(cellT, "Amount");
        }

        if (rewardCellButton != null)
        {
            rewardCellButton.onClick.RemoveListener(OnRewardCellClicked);
            rewardCellButton.onClick.AddListener(OnRewardCellClicked);
        }

        if (drawer == null)
        {
            Transform drawerT = MissionUi.FindDeepChild(transform, "Drawer");
            if (drawerT != null)
            {
                drawer = drawerT as RectTransform;
            }
        }

        if (drawer != null)
        {
            drawerGroup = drawerGroup != null ? drawerGroup : drawer.GetComponent<CanvasGroup>();
            drawerContent = drawer; // 셀은 Drawer 직접 자식
        }

        if (stamp == null)
        {
            Transform stampT = MissionUi.FindDeepChild(transform, "Stamp");
            if (stampT != null)
            {
                stamp = stampT.gameObject;
            }
        }

        if (stamp != null)
        {
            stampGroup = stampGroup != null ? stampGroup : stamp.GetComponent<CanvasGroup>();
        }

        bound = true;
    }

    private void OnDisable()
    {
        StopCycle();
        // 파괴/비활성 시 진행 중인 트윈 정리(destroyed target 트윈 경고 방지)
        if (stamp != null) stamp.transform.DOKill();
        if (stampGroup != null) stampGroup.DOKill();
        if (drawer != null) drawer.DOKill();
        if (drawerGroup != null) drawerGroup.DOKill();
    }

    public void Setup(MissionInfo info, string lang, Action<string> onClaim, Action<MissionCardRow> onDrawerOpen)
    {
        BindExisting();
        this.def = info;
        this.onClaim = onClaim;
        this.onDrawerOpen = onDrawerOpen;

        CloseDrawerImmediate();
        StopCycle();

        if (info == null)
        {
            return;
        }

        int level = info.claimedTiers;
        int current = info.current;
        int target = info.NextTarget;
        bool allDone = info.AllDone;
        bool claimable = info.Claimable;

        if (description != null)
        {
            description.text = info.title != null ? info.title.Get(lang) : info.id;
        }

        if (gaugeFill != null)
        {
            float p = allDone ? 1f : Mathf.Clamp01((float)current / Mathf.Max(1, target));
            RectTransform fr = gaugeFill.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(p, 1f);
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;
        }

        if (progressLabel != null)
        {
            progressLabel.text = allDone ? "달성 완료" : current + " / " + target;
        }

        if (tierLabel != null)
        {
            tierLabel.text = BuildTierLabel(level);
            tierLabel.gameObject.SetActive(!string.IsNullOrEmpty(tierLabel.text));
        }

        // 보상 셀/서랍 — 달성 완료면 '마지막으로 받은' 단계 보상을 보여준다(빈 흰 칸 방지).
        int rewardLevel = allDone ? Mathf.Max(0, level - 1) : level;
        currentReward = info.RewardForLevel(rewardLevel);
        cycleKinds.Clear();
        if (currentReward != null)
        {
            cycleKinds.AddRange(currentReward.NonZeroKinds());
        }

        cycleIndex = 0;
        if (rewardContentGroup != null)
        {
            rewardContentGroup.alpha = 1f;
        }

        if (cycleKinds.Count > 0)
        {
            MissionUi.ApplyRewardCell(rewardIcon, rewardAmount, cycleKinds[0], currentReward.ValueOf(cycleKinds[0]));
        }
        else if (rewardAmount != null)
        {
            rewardAmount.text = string.Empty;
            if (rewardIcon != null) rewardIcon.enabled = false;
        }

        BuildDrawer();

        // 다중 보상이면 3초마다 내용(Content)만 페이드로 순환(배경 흰 박스는 유지)
        if (cycleKinds.Count > 1 && isActiveAndEnabled)
        {
            cycleCo = StartCoroutine(CycleRoutine());
        }

        // 상태별 카드 색: 미수행 / 수령가능 / 달성 셋 다 다르게
        if (background != null)
        {
            background.color = claimable ? CardClaimable : (allDone ? CardDone : CardNormal);
        }

        if (rewardCellImage != null)
        {
            rewardCellImage.color = claimable ? MissionUi.Gold : MissionUi.GaugeBorder;
        }

        // 도장
        bool showStamp = allDone;
        if (stamp != null)
        {
            stamp.SetActive(showStamp);
            if (stampGroup != null)
            {
                stampGroup.alpha = showStamp ? 1f : 0f;
            }

            if (showStamp)
            {
                stamp.transform.localScale = Vector3.one;
                stamp.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            }
        }
    }

    // 카드 어디를 눌러도(보상 셀 제외): 수령 가능할 때만 수령. (상세는 보상 셀 클릭으로)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (def == null)
        {
            return;
        }

        if (def.Claimable)
        {
            onClaim?.Invoke(def.id);
        }
    }

    // 보상 흰 사각형 클릭 → 상세 보상 서랍 토글(이 클릭은 카드로 전파되지 않음).
    private void OnRewardCellClicked()
    {
        if (def == null || currentReward == null || currentReward.IsEmpty)
        {
            return;
        }

        if (drawerOpen)
        {
            CloseDrawer();
        }
        else
        {
            OpenDrawer();
        }
    }

    public void PlayClaimEffect(bool nowAllDone)
    {
        if (stamp == null)
        {
            return;
        }

        stamp.SetActive(true);
        stamp.transform.DOKill();
        if (stampGroup != null)
        {
            stampGroup.DOKill();
        }

        stamp.transform.localScale = Vector3.one * 2f;
        stamp.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        stamp.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        stamp.transform.DOLocalRotate(new Vector3(0f, 0f, -12f), 0.4f).SetEase(Ease.OutBack);

        if (stampGroup != null)
        {
            stampGroup.alpha = 0f;
            stampGroup.DOFade(1f, 0.25f);
            if (!nowAllDone)
            {
                stampGroup.DOFade(0f, 0.3f).SetDelay(0.7f).OnComplete(() =>
                {
                    if (stamp != null)
                    {
                        stamp.SetActive(false);
                    }
                });
            }
        }
    }

    // ── 보상 셀 렌더 ──────────────────────────────────────────────────────────
    private void BuildDrawer()
    {
        if (drawerContent == null)
        {
            return;
        }

        for (int i = drawerContent.childCount - 1; i >= 0; i--)
        {
            Destroy(drawerContent.GetChild(i).gameObject);
        }

        for (int i = 0; i < cycleKinds.Count; i++)
        {
            int kind = cycleKinds[i];
            GameObject cell = MissionUi.CreateRewardCell("Chip", drawerContent, out Image ic, out TMP_Text am);
            LayoutElement le = cell.AddComponent<LayoutElement>();
            le.preferredWidth = 54f;   // 대표 보상 셀과 동일 크기
            le.preferredHeight = 54f;
            MissionUi.ApplyRewardCell(ic, am, kind, currentReward.ValueOf(kind));
        }
    }

    private IEnumerator CycleRoutine()
    {
        WaitForSecondsRealtime hold = new WaitForSecondsRealtime(3f);
        WaitForSecondsRealtime fade = new WaitForSecondsRealtime(0.3f);
        while (true)
        {
            yield return hold;
            if (rewardContentGroup != null)
            {
                rewardContentGroup.DOKill();
                rewardContentGroup.DOFade(0f, 0.3f);
            }

            yield return fade;
            cycleIndex = (cycleIndex + 1) % cycleKinds.Count;
            MissionUi.ApplyRewardCell(rewardIcon, rewardAmount, cycleKinds[cycleIndex], currentReward.ValueOf(cycleKinds[cycleIndex]));
            if (rewardContentGroup != null)
            {
                rewardContentGroup.DOFade(1f, 0.3f);
            }

            yield return fade;
        }
    }

    private void StopCycle()
    {
        if (cycleCo != null)
        {
            StopCoroutine(cycleCo);
            cycleCo = null;
        }

        if (rewardContentGroup != null)
        {
            rewardContentGroup.DOKill();
            rewardContentGroup.alpha = 1f;
        }
    }

    // ── 서랍 ─────────────────────────────────────────────────────────────────
    public void CloseDrawer()
    {
        if (!drawerOpen)
        {
            return;
        }

        drawerOpen = false;
        if (drawer == null)
        {
            return;
        }

        drawer.DOKill();
        drawer.DOScaleX(0f, 0.16f).SetEase(Ease.InCubic).OnComplete(() =>
        {
            if (drawer != null)
            {
                drawer.gameObject.SetActive(false);
            }
        });

        if (drawerGroup != null)
        {
            drawerGroup.DOKill();
            drawerGroup.DOFade(0f, 0.16f);
        }
    }

    private void CloseDrawerImmediate()
    {
        drawerOpen = false;
        if (drawer != null)
        {
            drawer.DOKill();
            drawer.localScale = new Vector3(0f, 1f, 1f);
            if (drawerGroup != null)
            {
                drawerGroup.DOKill();
                drawerGroup.alpha = 0f;
            }

            drawer.gameObject.SetActive(false);
        }
    }

    // 서랍은 오른쪽(피벗 1,0.5)에서 왼쪽으로 펼쳐진다(scaleX 0→1).
    private void OpenDrawer()
    {
        if (drawer == null || drawerOpen)
        {
            return;
        }

        drawerOpen = true;
        onDrawerOpen?.Invoke(this);

        drawer.gameObject.SetActive(true);
        drawer.DOKill();
        drawer.localScale = new Vector3(0f, 1f, 1f);
        drawer.DOScaleX(1f, 0.22f).SetEase(Ease.OutCubic);

        if (drawerGroup != null)
        {
            drawerGroup.DOKill();
            drawerGroup.alpha = 0f;
            drawerGroup.DOFade(1f, 0.22f);
        }
    }

    private string BuildTierLabel(int level)
    {
        if (def == null)
        {
            return string.Empty;
        }

        if (def.type == MissionType.Increment)
        {
            return "Lv." + (level + 1);
        }

        if (def.type == MissionType.Tiered)
        {
            return Mathf.Min(level + 1, def.tiers.Count) + "/" + def.tiers.Count;
        }

        return string.Empty;
    }
}
