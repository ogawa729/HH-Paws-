using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按钮推进的“逐页文本”脚本：
/// - pages: 一组作为“页面”的 GameObject（通常包含 Text/TMP），脚本会在每次按下时显示下一页并隐藏上一页。
/// - hideOnComplete: 当全部页面浏览完后要隐藏的物体（例如提示面板、某个模型等）。
/// - 可选：按键冷却与完成后禁用按钮。
/// 用法：把该脚本挂在任意对象上，在 Button 的 OnClick 中绑定 ToggleTextSequence.OnPress。
/// </summary>
public class ToggleTextSequence : MonoBehaviour
{
    [Header("页面 (按顺序)")]
    [Tooltip("要逐页显示的 GameObject 列表，按顺序。脚本会激活当前页并禁用上一页。")]
    public GameObject[] pages;

    [Header("完成后隐藏")] 
    [Tooltip("当所有页面浏览完后要隐藏的对象（可为空）")]
    public GameObject[] hideOnComplete;

    [Header("完成后按名称显现/隐藏（跨场景查找）")]
    [Tooltip("完成序列时按名称在所有已加载场景中查找并激活这些对象（支持 inactive 对象）")]
    public bool showObjectsByNameOnComplete = false;
    [Tooltip("要在完成时激活的对象名称列表；完全匹配名称，会激活找到的所有对象（跨所有有效场景）")]
    public string[] showNamesOnComplete;
    [Tooltip("完成序列时按名称在所有已加载场景中查找并隐藏这些对象（支持 inactive 对象）")]
    public bool hideObjectsByNameOnComplete = false;
    [Tooltip("要在完成时隐藏的对象名称列表；完全匹配名称，会隐藏找到的所有对象（跨所有有效场景）")]
    public string[] hideNamesOnComplete;

    [Header("首次按下隐藏（可选）")]
    [Tooltip("按钮第一次被按下时要隐藏的物体（只在首次按下执行）")]
    public GameObject[] hideOnFirstPress;

    [Header("行为设置")]
    [Tooltip("是否在 Start 时把 pages 全部设为 inactive（默认 true）")]
    public bool clearPagesOnStart = true;
    [Tooltip("完成后是否同时隐藏所有页面")]
    public bool hidePagesOnComplete = false;
    [Tooltip("完成后是否禁用绑定按钮（可在 Inspector 指定）")]
    public Button controlButton;

    [Header("防抖/冷却 (秒)")]
    public float pressCooldown = 0.15f;

    int index = -1; // 当前已显示的页索引；-1 表示未显示任何页
    float lastPressTime = -999f;

    void Start()
    {
        if (clearPagesOnStart && pages != null)
        {
            foreach (var p in pages)
            {
                if (p != null) p.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 绑定到按钮的回调。每次按下显示下一页并隐藏前一页；全部看完后触发完成行为。
    /// </summary>
    public void OnPress()
    {
        if (Time.time - lastPressTime < pressCooldown) return;
        lastPressTime = Time.time;

        // 首次按下时隐藏指定物体（仅第一次）
        if (index == -1 && hideOnFirstPress != null)
        {
            foreach (var g in hideOnFirstPress)
            {
                if (g == null) continue;
                g.SetActive(false);
            }
        }

        if (pages == null || pages.Length == 0)
        {
            // 只有完成动作（没有页面）
            OnSequenceComplete();
            return;
        }

        int next = index + 1;
        if (next < pages.Length)
        {
            // 显示 next，隐藏当前 index
            if (index >= 0 && index < pages.Length && pages[index] != null)
                pages[index].SetActive(false);

            if (pages[next] != null)
                pages[next].SetActive(true);

            index = next;

            // 如果刚刚显示的是最后一页，则执行完成逻辑
            if (index >= pages.Length - 1)
            {
                OnSequenceComplete();
            }
        }
        else
        {
            // 已经超出范围，直接完成
            OnSequenceComplete();
        }
    }

    void OnSequenceComplete()
    {
        // 隐藏指定物体
        if (hideOnComplete != null)
        {
            foreach (var g in hideOnComplete)
            {
                if (g == null) continue;
                g.SetActive(false);
            }
        }

        // 按名称激活对象（跨所有有效场景）
        if (showObjectsByNameOnComplete && showNamesOnComplete != null)
        {
            foreach (var name in showNamesOnComplete)
            {
                if (string.IsNullOrEmpty(name)) continue;
                ActivateByNameAcrossScenes(name);
            }
        }

        // 按名称隐藏对象（跨所有有效场景）
        if (hideObjectsByNameOnComplete && hideNamesOnComplete != null)
        {
            foreach (var name in hideNamesOnComplete)
            {
                if (string.IsNullOrEmpty(name)) continue;
                DeactivateByNameAcrossScenes(name);
            }
        }

        // 可选：隐藏所有页面
        if (hidePagesOnComplete && pages != null)
        {
            foreach (var p in pages)
            {
                if (p == null) continue;
                p.SetActive(false);
            }
        }

        // 可选：禁用控制按钮
        if (controlButton != null)
        {
            controlButton.interactable = false;
        }
    }

    /// <summary>
    /// 复位序列（从头开始）。可选择是否隐藏当前显示页。
    /// </summary>
    public void ResetSequence(bool hideCurrent = true)
    {
        if (hideCurrent && pages != null)
        {
            foreach (var p in pages)
            {
                if (p != null) p.SetActive(false);
            }
        }
        index = -1;
        lastPressTime = -999f;

        if (controlButton != null) controlButton.interactable = true;
    }

    // 在所有已加载/有效场景中查找名称匹配的对象（包括 inactive），并激活它们。
    private void ActivateByNameAcrossScenes(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        bool any = false;
        foreach (var go in all)
        {
            if (go == null) continue;
            // 只处理 scene 对象，跳过项目资源 (prefab asset 等)
            if (!go.scene.IsValid()) continue;
            if (go.name != name) continue;
            any = true;
            try { go.SetActive(true); } catch { }
        }
        // 可选：记录日志便于调试
        if (!any) Debug.LogWarning($"ToggleTextSequence: no scene object named '{name}' found to activate.");
    }

    // 在所有已加载/有效场景中查找名称匹配的对象（包括 inactive），并隐藏它们（SetActive(false)）。
    private void DeactivateByNameAcrossScenes(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        bool any = false;
        foreach (var go in all)
        {
            if (go == null) continue;
            if (!go.scene.IsValid()) continue;
            if (go.name != name) continue;
            any = true;
            try { go.SetActive(false); } catch { }
        }
        if (!any) Debug.LogWarning($"ToggleTextSequence: no scene object named '{name}' found to deactivate.");
    }
}
