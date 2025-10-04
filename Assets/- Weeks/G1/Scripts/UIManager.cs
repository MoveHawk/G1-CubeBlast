using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("Containers for Panels (stretch anchors)")]
    public RectTransform startContainer;
    public RectTransform gameContainer;
    public RectTransform finishContainer;
    public RectTransform pauseContainer; // Assign your Pause Panel Container here

    [Header("CanvasGroups for Panels")]
    public CanvasGroup startGroup;
    public CanvasGroup gameGroup;
    public CanvasGroup finishGroup;
    public CanvasGroup pauseGroup; // Assign your Pause Panel CanvasGroup here

    [Header("Transition Settings")]
    public float slideDuration = 0.5f;
    public float pauseSlideDuration = 0.4f;  // Pause transition speed adjustable in Inspector
    public float delayBeforeSpawning = 1f;

    [Header("Audio Source for UI Sounds")]
    public AudioClip playSound;
    public AudioClip restartSound;
    public AudioClip quitSound;
    public AudioClip slideInSound;
    public AudioSource popAudioSource;

    public GameManager gameManager; // Assign in Inspector

    Vector2 offRight, offLeft, center, pauseOffDown; // Directions

    void Awake()
    {
        float width = Screen.width;
        float height = Screen.height;
        offRight = new Vector2(width, 0);
        offLeft = new Vector2(-width, 0);
        center = Vector2.zero;
        pauseOffDown = new Vector2(0, -height);
    }

    void Start()
    {
        SetPanel(startContainer, startGroup, center, 1f, true);
        SetPanel(gameContainer, gameGroup, offRight, 0f, false);
        SetPanel(finishContainer, finishGroup, offRight, 0f, false);
        SetPanel(pauseContainer, pauseGroup, pauseOffDown, 0f, false);
    }

    void SetPanel(RectTransform panel, CanvasGroup group, Vector2 pos, float alpha, bool interact)
    {
        if (panel) panel.anchoredPosition = pos;
        if (group)
        {
            group.alpha = alpha;
            group.interactable = interact && alpha > 0.99f;
            group.blocksRaycasts = interact && alpha > 0.99f;
            group.gameObject.SetActive(true);
        }
    }

    public void OnStartButtonPressed()
    {
        PlaySound(playSound);
        SetPanel(finishContainer, finishGroup, offRight, 0f, false);

        // Set the new target INSTANTLY, before the panel slides in
        if (gameManager != null)
            gameManager.SetNewTarget();

        StartCoroutine(SlideTransitionSideways(startContainer, startGroup, offLeft, gameContainer, gameGroup, center, () =>
        {
            StartCoroutine(StartGameDelayed());
        }));
    }

    public void OnRestartButtonPressed()
    {
        PlaySound(restartSound);
        if (gameManager != null)
            gameManager.RestartGame();

        SetPanel(startContainer, startGroup, offRight, 0f, false);

        // Set the new target INSTANTLY, before the panel slides in
        if (gameManager != null)
            gameManager.SetNewTarget();

        StartCoroutine(SlideTransitionSideways(finishContainer, finishGroup, offRight, gameContainer, gameGroup, center, () =>
        {
            StartCoroutine(StartGameDelayed());
        }));
    }

    void PlaySound(AudioClip clip)
    {
        if (popAudioSource != null && clip != null)
            popAudioSource.PlayOneShot(clip);
    }

    IEnumerator StartGameDelayed()
    {
        yield return new WaitForSeconds(delayBeforeSpawning);
        if (gameManager != null)
            gameManager.StartGameplay();
    }

    IEnumerator SlideTransitionSideways(
        RectTransform outPanel, CanvasGroup outGroup, Vector2 outTarget,
        RectTransform inPanel, CanvasGroup inGroup, Vector2 inTarget,
        System.Action onComplete = null)
    {
        Vector2 inStart = (inTarget == center) ? offRight : offLeft;

        if (outGroup != null) { outGroup.interactable = false; outGroup.blocksRaycasts = false; }
        if (inGroup != null)
        {
            inPanel.gameObject.SetActive(true);
            inPanel.anchoredPosition = inStart;
            inGroup.alpha = 1f;
            inGroup.interactable = false;
            inGroup.blocksRaycasts = false;
        }

        float t = 0;
        Vector2 outStart = outPanel.anchoredPosition;

        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float p = t / slideDuration;
            outPanel.anchoredPosition = Vector2.Lerp(outStart, outTarget, p);
            inPanel.anchoredPosition = Vector2.Lerp(inStart, inTarget, p);
            yield return null;
        }
        outPanel.anchoredPosition = outTarget;
        inPanel.anchoredPosition = inTarget;

        outGroup.alpha = 0f;
        outGroup.interactable = false;
        outGroup.blocksRaycasts = false;
        outPanel.gameObject.SetActive(false);

        inGroup.alpha = 1f;
        inGroup.interactable = true;
        inGroup.blocksRaycasts = true;
        inPanel.gameObject.SetActive(true);

        onComplete?.Invoke();
    }

    // PAUSE/RESUME
    public void OnPauseButtonPressed()
    {
        PlaySound(slideInSound);
        Time.timeScale = 0f;
        if (gameManager != null)
            gameManager.SetPaused(true);
        StartCoroutine(SlidePauseIn());
    }

    IEnumerator SlidePauseIn()
    {
        pauseContainer.gameObject.SetActive(true);
        pauseContainer.anchoredPosition = pauseOffDown;
        pauseGroup.alpha = 1f;
        pauseGroup.interactable = false;
        pauseGroup.blocksRaycasts = false;

        float t = 0f;
        Vector2 start = pauseOffDown;
        Vector2 end = center;
        while (t < pauseSlideDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / pauseSlideDuration;
            pauseContainer.anchoredPosition = Vector2.Lerp(start, end, p);
            yield return null;
        }
        pauseContainer.anchoredPosition = center;
        pauseGroup.interactable = true;
        pauseGroup.blocksRaycasts = true;
    }

    public void OnResumeButtonPressed()
    {
        PlaySound(playSound);
        StartCoroutine(SlidePauseOutAndResume());
    }

    IEnumerator SlidePauseOutAndResume()
    {
        PlaySound(slideInSound);
        pauseGroup.interactable = false;
        pauseGroup.blocksRaycasts = false;

        float t = 0f;
        Vector2 start = center;
        Vector2 end = pauseOffDown;
        while (t < pauseSlideDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / pauseSlideDuration;
            pauseContainer.anchoredPosition = Vector2.Lerp(start, end, p);
            yield return null;
        }
        pauseContainer.anchoredPosition = end;
        pauseGroup.alpha = 0f;
        pauseContainer.gameObject.SetActive(false);

        Time.timeScale = 1f;
        if (gameManager != null)
            gameManager.SetPaused(false);
    }

    public void OnPauseRestartButtonPressed()
    {
        PlaySound(restartSound);
        StartCoroutine(SlidePauseOutAndCallback(() =>
        {
            Time.timeScale = 1f;
            if (gameManager != null)
                gameManager.SetPaused(false);
            if (gameManager != null)
                gameManager.RestartGame();
            SetPanel(startContainer, startGroup, offRight, 0f, false);

            // Set the new target INSTANTLY, before the panel slides in
            if (gameManager != null)
                gameManager.SetNewTarget();

            StartCoroutine(SlideTransitionSideways(finishContainer, finishGroup, offRight, gameContainer, gameGroup, center, () =>
            {
                StartCoroutine(StartGameDelayed());
            }));
        }));
    }

    public void OnPauseQuitButtonPressed()
    {
        PlaySound(quitSound);
        StartCoroutine(SlidePauseOutAndCallback(() =>
        {
            Time.timeScale = 1f;
            if (gameManager != null)
                gameManager.SetPaused(false);
            Application.Quit();
        }));
    }

    IEnumerator SlidePauseOutAndCallback(System.Action callback)
    {
        PlaySound(slideInSound);
        pauseGroup.interactable = false;
        pauseGroup.blocksRaycasts = false;

        float t = 0f;
        Vector2 start = center;
        Vector2 end = pauseOffDown;
        while (t < pauseSlideDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / pauseSlideDuration;
            pauseContainer.anchoredPosition = Vector2.Lerp(start, end, p);
            yield return null;
        }
        pauseContainer.anchoredPosition = end;
        pauseGroup.alpha = 0f;
        pauseContainer.gameObject.SetActive(false);

        callback?.Invoke();
    }

    public void ShowFinish()
    {
        StartCoroutine(SlideTransitionSideways(gameContainer, gameGroup, offLeft, finishContainer, finishGroup, center, null));
    }

    public void ResetUI()
    {
        SetPanel(startContainer, startGroup, center, 1f, true);
        SetPanel(gameContainer, gameGroup, offRight, 0f, false);
        SetPanel(finishContainer, finishGroup, offRight, 0f, false);
        SetPanel(pauseContainer, pauseGroup, pauseOffDown, 0f, false);
    }
}