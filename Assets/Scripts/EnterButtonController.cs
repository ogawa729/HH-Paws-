using System.Collections;
using UnityEngine;

public class EnterButtonController : MonoBehaviour
{
    [Header("视觉/声音")]
    public Renderer targetRenderer;            // 要改色的 Renderer（可以是自己或者子物体）
    public Color pressedColor = Color.yellow;
    private Color originalColor;

    public AudioSource audioSource;            // 可拖入场景中的 AudioSource
    public AudioClip pressClip;
    public AudioClip releaseClip;

    [Header("按键动画")]
    public float pressDepth = 0.02f;           // 按下深度（本地坐标）
    public float pressSpeed = 8f;              // 动画速度
    private Vector3 originalLocalPos;
    private Coroutine moveCoroutine;

    [Header("显示物体")]
    public GameObject[] objectsToShow;         // Hover 结束后要显现的物体（可以为空）
    public GameObject[] objectsToHide;         // Hover 开始时要隐藏的物体（可以为空）

    void Start()
    {
        if (targetRenderer != null)
            originalColor = targetRenderer.material.color; // 使用 material 会自动实例化
        originalLocalPos = transform.localPosition;
        // 可选：初始将 objects 隐藏（若需）
        // foreach (var g in objectsToShow) if (g) g.SetActive(false);
    }

    // 在 GrabInteractable 的 Inspector -> On Hover Begin 中调用
    public void OnHoverBegin()
    {
        // 立即按下（停止任何正在进行的移动）
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        Vector3 target = originalLocalPos + Vector3.down * pressDepth;
        moveCoroutine = StartCoroutine(MoveToLocal(target));

        // 变色
        if (targetRenderer != null) targetRenderer.material.color = pressedColor;

        // 播放按下音效
        if (audioSource != null && pressClip != null) audioSource.PlayOneShot(pressClip);

        // 隐藏指定物体（如果有设置）
        if (objectsToHide != null)
        {
            foreach (var g in objectsToHide)
                if (g != null) g.SetActive(false);
        }
    }

    // 在 GrabInteractable 的 Inspector -> On Hover End 中调用
    public void OnHoverEnd()
    {
        // 弹回
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveToLocal(originalLocalPos));

        // 还原颜色
        if (targetRenderer != null) targetRenderer.material.color = originalColor;

        // 播放释放音效
        if (audioSource != null && releaseClip != null) audioSource.PlayOneShot(releaseClip);

        // 显现物体
        foreach (var g in objectsToShow)
            if (g != null) g.SetActive(true);
    }

    private IEnumerator MoveToLocal(Vector3 targetLocal)
    {
        while ((transform.localPosition - targetLocal).sqrMagnitude > 0.00001f)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocal, Time.deltaTime * pressSpeed);
            yield return null;
        }
        transform.localPosition = targetLocal;
    }
}
