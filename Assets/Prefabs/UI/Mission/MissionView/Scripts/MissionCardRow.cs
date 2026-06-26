using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 미션 카드 1장. 계층은 MissionView가 구성, 이 컴포넌트는 바인딩 + 렌더 + 도장/서랍 동작.
public class MissionCardRow : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text description;
    [SerializeField] private Image gaugeFill;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text tierLabel;
    [SerializeField] private Button rewardChipButton;
    [SerializeField] private TMP_Text rewardChipText;
    [SerializeField] private RectTransform drawer;
    [SerializeField] private CanvasGroup drawerGroup;
    [SerializeField] private RectTransform drawerContent;
    [SerializeField] private GameObject stamp;
    [SerializeField] private CanvasGroup stampGroup;

    private MissionDef def;
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

        background = GetComponent<Image>();
        description = MissionUi.FindComponent<TMP_Text>(transform, "Description");
        gaugeFill = MissionUi.FindComponent<Image>(transform, "GaugeFill");
        progressLabel = MissionUi.FindComponent<TMP_Text>(transform, "ProgressLabel");
        tierLabel = MissionUi.FindComponent<TMP_Text>(transform, "TierLabel");
        rewardChipButton = MissionUi.FindComponent<Button>(transform, "RewardChipButton");
        rewardChipText = MissionUi.FindComponent<TMP_Text>(transform, "RewardChipText");

        Transform drawerT = MissionUi.FindDeepChild(transform, "Drawer");
        if (drawerT != null)
        {
            drawer = drawerT as RectTransform;
            drawerGroup = drawerT.GetComponent<CanvasGroup>();
        }

        drawerContent = MissionUi.FindComponent<RectTransform>(transform, "DrawerContent");

        Transform stampT = MissionUi.FindDeepChild(transform, "Stamp");
        if (stampT != null)
        {
            stamp = stampT.gameObject;
            stampGroup = stampT.GetComponent<CanvasGroup>();
        }

        if (rewardChipButton != null)
        {
            rewardChipButton.onClick.RemoveListener(OnRewardChipClicked);
            rewardChipButton.onClick.AddListener(OnRewardChipClicked);
        }

        bound = true;
    }

    public void Setup(MissionDef def, MissionProgress progress, string lang, Action<string> onClaim,
        Action<MissionCardRow> onDrawerOpen)
    {
        BindExisting();
        this.def = def;
        this.onClaim = onClaim;
        this.onDrawerOpen = onDrawerOpen;

        CloseDrawerImmediate();

        if (def == null)
        {
            return;
        }

        int level = progress != null ? progress.claimedTiers : 0;
        int current = progress != null ? progress.currentCount : 0;
        int target = def.TargetForLevel(level);
        bool allDone = def.IsAllDone(level);
        bool claimable = MissionManager.IsClaimable(def, progress ?? new MissionProgress(def.id));

        if (description != null)
        {
            description.text = def.title != null ? def.title.Get(lang) : def.id;
        }

        if (gaugeFill != null)
        {
            // 표시되는 X/Y 그대로의 비율로 채운다(예: 3/10 → 30%). float 캐스팅 필수.
            float p = allDone ? 1f : Mathf.Clamp01((float)current / Mathf.Max(1, target));
            gaugeFill.fillAmount = p;
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

        MissionReward reward = def.RewardForLevel(level);
        RenderRewardChip(reward, claimable, allDone);
        BuildDrawer(reward);

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

    // 보상 수령 직후 호출: 도장 팝 연출. nowAllDone이면 도장 유지, 아니면 잠깐 보였다 사라짐.
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
        onDrawerOpen?.Invoke(this); // 다른 카드 서랍 닫기 요청

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

    private void OnRewardChipClicked()
    {
        if (def == null)
        {
            return;
        }

        // 수령 가능하면 보상 수령(도장은 View가 PlayClaimEffect로 트리거)
        MissionProgress p = MissionManager.Instance != null ? MissionManager.Instance.GetProgress(def.id) : null;
        bool claimable = MissionManager.IsClaimable(def, p ?? new MissionProgress(def.id));
        if (claimable)
        {
            onClaim?.Invoke(def.id);
            return;
        }

        // 아니면 보상이 여러 종일 때 서랍 토글(미리보기)
        MissionReward reward = def.RewardForLevel(p != null ? p.claimedTiers : 0);
        if (reward != null && reward.RewardKinds > 1)
        {
            if (drawerOpen)
            {
                CloseDrawer();
            }
            else
            {
                OpenDrawer();
            }
        }
    }

    private void RenderRewardChip(MissionReward reward, bool claimable, bool allDone)
    {
        if (rewardChipButton != null)
        {
            Image chipImage = rewardChipButton.GetComponent<Image>();
            if (chipImage != null)
            {
                MissionUi.ApplyRounded(chipImage, claimable ? MissionUi.Accent : MissionUi.PanelBg2);
            }

            rewardChipButton.interactable = !allDone;
        }

        if (rewardChipText != null)
        {
            if (allDone)
            {
                rewardChipText.text = "완료";
            }
            else if (claimable)
            {
                rewardChipText.text = "받기";
            }
            else
            {
                rewardChipText.text = ShortReward(reward);
            }
        }
    }

    private void BuildDrawer(MissionReward reward)
    {
        if (drawerContent == null)
        {
            return;
        }

        for (int i = drawerContent.childCount - 1; i >= 0; i--)
        {
            Destroy(drawerContent.GetChild(i).gameObject);
        }

        if (reward == null)
        {
            return;
        }

        AddDrawerChip(reward.gold, "G", MissionUi.Gold);
        AddDrawerChip(reward.item1, "i1", MissionUi.ItemChip);
        AddDrawerChip(reward.item2, "i2", MissionUi.ItemChip);
        AddDrawerChip(reward.item3, "i3", MissionUi.ItemChip);
    }

    private void AddDrawerChip(int amount, string prefix, Color color)
    {
        if (amount == 0 || drawerContent == null)
        {
            return;
        }

        GameObject chip = MissionUi.CreatePanel("Chip", drawerContent, color);
        MissionUi.Layout(chip, minH: 26f, prefH: 26f, minW: 48f, prefW: 56f);
        TextMeshProUGUI text = MissionUi.CreateText("Text", chip.transform,
            prefix == "G" ? "G " + amount : prefix + "×" + amount, 13f, MissionUi.TextWhite, TextAlignmentOptions.Center);
        MissionUi.SetStretch(text.gameObject, Vector4.zero);
    }

    private static string ShortReward(MissionReward reward)
    {
        if (reward == null || reward.IsEmpty)
        {
            return "-";
        }

        if (reward.gold != 0)
        {
            return reward.RewardKinds > 1 ? "G" + reward.gold + "+" : "G" + reward.gold;
        }

        if (reward.item1 != 0) return "i1×" + reward.item1 + (reward.RewardKinds > 1 ? "+" : "");
        if (reward.item2 != 0) return "i2×" + reward.item2 + (reward.RewardKinds > 1 ? "+" : "");
        if (reward.item3 != 0) return "i3×" + reward.item3;
        return "-";
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
