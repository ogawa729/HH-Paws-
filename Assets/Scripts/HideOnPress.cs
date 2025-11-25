using System.Collections;
using UnityEngine;

/// <summary>
/// 简单的按下隐藏脚本。
/// 使用方法：把该脚本挂到任意物体（例如 `enter`），
/// 在 GrabInteractable 的 On Hover Begin 中指向 Hide() 方法，
/// 将要隐藏的物体拖到 `targets` 列表。
/// 支持延时隐藏和选择是 SetActive(false) 还是 Destroy。
/// </summary>
public class HideOnPress : MonoBehaviour
{
    [Tooltip("要隐藏的物体列表")] public GameObject[] targets;
    [Tooltip("延时隐藏（秒），0 表示立即")] public float delay = 0f;
    [Tooltip("勾选后会 Destroy 而不是 SetActive(false)")] public bool destroyInsteadOfDisable = false;

    // 无参公有方法，方便在 Inspector 里直接绑定 GrabInteractable 事件
    public void Hide()
    {
        if (delay <= 0f)
        {
            HideNow();
            return;
        }
        StartCoroutine(HideCoroutine());
    }

    private IEnumerator HideCoroutine()
    {
        yield return new WaitForSeconds(delay);
        HideNow();
    }

    private void HideNow()
    {
        if (targets == null || targets.Length == 0) return;
        foreach (var go in targets)
        {
            if (go == null) continue;
            if (destroyInsteadOfDisable) Destroy(go);
            else go.SetActive(false);
        }
    }

    // 可选： 公开 Show 方法，便于在 On Hover End 或其他事件中重新显示
    public void Show()
    {
        if (targets == null || targets.Length == 0) return;
        foreach (var go in targets)
            if (go != null) go.SetActive(true);
    }
}
