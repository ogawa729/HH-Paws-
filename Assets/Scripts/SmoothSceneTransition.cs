using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SmoothSceneTransition : MonoBehaviour
{
    [Header("Overlay")]
    public CanvasGroup overlay;         // 全屏遮罩（alpha 0 = 透明, 1 = 不透明）
    public float fadeDuration = 0.5f;   // 淡入/淡出时长（秒）

    [Header("Progress")]
    public Image progressBar;           // 可选的进度条（fillAmount）

    [Header("Trigger")]
    public Button triggerButton;        // 可选：按下此按钮开始过渡并加载指定场景
    public bool triggerUseSceneName = true; // true 使用 triggerSceneName，否则使用 triggerSceneIndex
    public string triggerSceneName = "";
    public int triggerSceneIndex = 0;
    public LoadSceneMode triggerMode = LoadSceneMode.Single;

    void Start()
    {
        if (triggerButton != null)
            triggerButton.onClick.AddListener(OnTriggerClicked);
    }

    void OnTriggerClicked()
    {
        if (triggerUseSceneName)
            LoadByName(triggerSceneName, triggerMode);
        else
            LoadByIndex(triggerSceneIndex, triggerMode);
    }

    void OnDestroy()
    {
        if (triggerButton != null)
            triggerButton.onClick.RemoveListener(OnTriggerClicked);
    }

    // 通过名字加载并使用过渡
    public void LoadByName(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        StartCoroutine(LoadRoutineByName(sceneName, mode));
    }

    // 通过索引加载并使用过渡
    public void LoadByIndex(int index, LoadSceneMode mode = LoadSceneMode.Single)
    {
        StartCoroutine(LoadRoutineByIndex(index, mode));
    }

    IEnumerator LoadRoutineByName(string sceneName, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("SmoothSceneTransition: sceneName is empty.");
            yield break;
        }

        // Fade in overlay to cover current scene
        yield return StartCoroutine(FadeOverlay(0f, 1f, fadeDuration));

        var op = SceneManager.LoadSceneAsync(sceneName, mode);
        if (op == null)
        {
            Debug.LogWarning($"SmoothSceneTransition: failed to load scene '{sceneName}'");
            yield break;
        }

        // Don't activate until ready
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            if (progressBar != null) progressBar.fillAmount = op.progress;
            yield return null;
        }

        if (progressBar != null) progressBar.fillAmount = 1f;

        // One frame buffer then activate
        yield return null;
        op.allowSceneActivation = true;

        while (!op.isDone)
            yield return null;

        // Fade out overlay to reveal new scene
        yield return StartCoroutine(FadeOverlay(1f, 0f, fadeDuration));
    }

    IEnumerator LoadRoutineByIndex(int index, LoadSceneMode mode)
    {
        // Fade in overlay to cover current scene
        yield return StartCoroutine(FadeOverlay(0f, 1f, fadeDuration));

        var op = SceneManager.LoadSceneAsync(index, mode);
        if (op == null)
        {
            Debug.LogWarning($"SmoothSceneTransition: failed to load scene index {index}");
            yield break;
        }

        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            if (progressBar != null) progressBar.fillAmount = op.progress;
            yield return null;
        }

        if (progressBar != null) progressBar.fillAmount = 1f;

        yield return null;
        op.allowSceneActivation = true;

        while (!op.isDone)
            yield return null;

        // Fade out overlay to reveal new scene
        yield return StartCoroutine(FadeOverlay(1f, 0f, fadeDuration));
    }

    IEnumerator FadeOverlay(float from, float to, float duration)
    {
        if (overlay == null)
            yield break;

        float t = 0f;
        overlay.alpha = from;
        if (duration <= 0f)
        {
            overlay.alpha = to;
            yield break;
        }

        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // use unscaled so UI still animates when timeScale == 0
            overlay.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }

        overlay.alpha = to;
    }
}
