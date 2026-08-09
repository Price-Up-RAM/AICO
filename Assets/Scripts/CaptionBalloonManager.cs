using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaptionBalloonManager : MonoBehaviour
{
    private static CaptionBalloonManager instance;

    public static CaptionBalloonManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<CaptionBalloonManager>();
            }

            return instance;
        }
    }

    [Header("References")]
    [SerializeField] private GameObject captionBalloon;
    [SerializeField] private RectTransform captionBalloonTransform;
    [SerializeField] private TMP_Text captionText;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private Button dismissButton;

    [Header("Layout")]
    [SerializeField] private float minWidth = 720f;
    [SerializeField] private float maxWidth = 1200f;
    [SerializeField] private float canvasHorizontalMargin = 80f;
    [SerializeField] private float horizontalPadding = 80f;
    [SerializeField] private float verticalPadding = 40f;
    [SerializeField] private float minHeight = 80f;

    private Coroutine timedHideCoroutine;
    private float lastCanvasWidth = -1f;

    private void Awake()
    {
        instance = this;

        if (dismissButton != null)
        {
            dismissButton.onClick.AddListener(Hide);
        }

        Hide();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (dismissButton != null)
        {
            dismissButton.onClick.RemoveListener(Hide);
        }
    }

    private void Update()
    {
        if (captionBalloon == null ||
            !captionBalloon.activeSelf ||
            canvasRect == null)
        {
            return;
        }

        float canvasWidth = Mathf.Abs(canvasRect.rect.width);
        if (!Mathf.Approximately(canvasWidth, lastCanvasWidth))
        {
            RefreshLayout();
        }
    }

    public void ShowForSeconds(string text, float seconds)
    {
        if (captionBalloon == null ||
            captionBalloonTransform == null ||
            captionText == null)
        {
            Debug.LogWarning("[CaptionBalloon] Scene references are missing.");
            return;
        }

        if (timedHideCoroutine != null)
        {
            StopCoroutine(timedHideCoroutine);
        }

        captionText.text = text ?? string.Empty;
        captionBalloon.SetActive(true);
        RefreshLayout();
        timedHideCoroutine =
            StartCoroutine(HideAfterSeconds(Mathf.Max(1f, seconds)));
    }

    public void Hide()
    {
        if (timedHideCoroutine != null)
        {
            StopCoroutine(timedHideCoroutine);
            timedHideCoroutine = null;
        }

        if (captionBalloon != null)
        {
            captionBalloon.SetActive(false);
        }
    }

    private void RefreshLayout()
    {
        float canvasWidth = canvasRect != null
            ? Mathf.Abs(canvasRect.rect.width)
            : maxWidth + canvasHorizontalMargin * 2f;
        float availableWidth =
            Mathf.Max(0f, canvasWidth - canvasHorizontalMargin * 2f);
        float width = Mathf.Clamp(availableWidth, minWidth, maxWidth);
        float textWidth = Mathf.Max(0f, width - horizontalPadding);

        captionText.ForceMeshUpdate();
        float textHeight =
            captionText.GetPreferredValues(
                captionText.text,
                textWidth,
                Mathf.Infinity).y;
        float height = Mathf.Max(minHeight, textHeight + verticalPadding);

        captionBalloonTransform.anchorMin = new Vector2(0.5f, 0f);
        captionBalloonTransform.anchorMax = new Vector2(0.5f, 0f);
        captionBalloonTransform.pivot = new Vector2(0.5f, 0f);
        captionBalloonTransform.sizeDelta = new Vector2(width, height);
        MoveToCaptionPosition();
        lastCanvasWidth = canvasWidth;
    }

    private void MoveToCaptionPosition()
    {
        UIPositionManager positionManager = UIPositionManager.Instance;
        if (positionManager == null || captionBalloonTransform == null)
        {
            Debug.LogWarning(
                "[CaptionBalloon] UI position mapping unavailable: " +
                "captionBalloon");
            return;
        }

        captionBalloonTransform.position =
            positionManager.GetMenuPosition("captionBalloon");
    }

    private IEnumerator HideAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        timedHideCoroutine = null;
        Hide();
    }
}
