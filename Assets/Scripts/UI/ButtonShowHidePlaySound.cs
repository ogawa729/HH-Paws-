using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonShowHidePlaySound : MonoBehaviour
{
    [Header("通过引用配置")]
    [Tooltip("这些在按钮按下时会被隐藏（SetActive(false) 或 Destroy，取决于设置）。")]
    public GameObject[] hideTargets;

    [Tooltip("这些在按钮按下时会被显示（SetActive(true)）。")]
    public GameObject[] showTargets;

    [Header("通过名称配置")]
    [Tooltip("按名称查找并隐藏对象（支持 inactive）。")]
    public string[] hideTargetNames;
    
    [Tooltip("按名称查找并激活对象（支持 inactive）。")]
    public string[] showTargetNames;

    [Header("隐藏选项")]
    [Tooltip("如果为 true，则在按下后销毁 hideTargets，否则仅 SetActive(false)（默认 false）。")]
    public bool destroyHidden = false;
    
    [Tooltip("是否也销毁通过名称找到的物体（默认 false，只 SetActive(false)）。")]
    public bool destroyHiddenByName = false;

    [Header("音频")]
    [Tooltip("在按钮被按下时播放的音频片段（通过此 GameObject 的 AudioSource 播放）。如果此处为空，会尝试使用同一物体上的 AudioSource 的 clip。")]
    public AudioClip clickClip;
    [Tooltip("播放音效使用的 AudioSource（如果为空，脚本会在 Awake 时尝试获取或添加一个）。")]
    public AudioSource audioSource;
    
    [Header("调试")]
    [Tooltip("启用调试日志")]
    public bool enableDebugLog = false;

    private Button btn;

    void Awake()
    {
        btn = GetComponent<Button>();
        if (btn == null)
        {
            Debug.LogError("ButtonShowHidePlaySound requires a Button component on the same GameObject.");
            enabled = false;
            return;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        btn.onClick.AddListener(OnPressed);
    }

    private void OnDestroy()
    {
        if (btn != null) btn.onClick.RemoveListener(OnPressed);
    }

    private void OnPressed()
    {
        if (enableDebugLog)
        {
            Debug.Log($"[ButtonShowHide] 按钮被点击: {gameObject.name}");
        }
        
        // 先播放音效
        PlayClickSound();

        // 1. 通过引用隐藏或销毁指定对象
        if (hideTargets != null)
        {
            foreach (var go in hideTargets)
            {
                if (go == null) continue;
                try
                {
                    if (destroyHidden)
                    {
                        if (enableDebugLog) Debug.Log($"[ButtonShowHide] 销毁: {go.name}");
                        Destroy(go);
                    }
                    else
                    {
                        if (enableDebugLog) Debug.Log($"[ButtonShowHide] 隐藏: {go.name}");
                        go.SetActive(false);
                    }
                }
                catch { }
            }
        }

        // 2. 通过名称隐藏对象
        if (hideTargetNames != null)
        {
            foreach (var name in hideTargetNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                DeactivateObjectsByName(name);
            }
        }

        // 3. 通过引用激活指定对象
        if (showTargets != null)
        {
            foreach (var go in showTargets)
            {
                if (go == null) continue;
                try
                {
                    if (enableDebugLog) Debug.Log($"[ButtonShowHide] 显示: {go.name}");
                    go.SetActive(true);
                }
                catch { }
            }
        }

        // 4. 通过名称查找并激活（可找到 inactive 的对象，包括被 DontDestroyOnLoad 保留的对象）
        if (showTargetNames != null)
        {
            foreach (var name in showTargetNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                ActivateObjectsByName(name);
            }
        }
    }

    // 在所有加载的资源对象中查找指定名字的 GameObject 并尝试隐藏（包含 inactive）
    private void DeactivateObjectsByName(string name)
    {
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        bool found = false;
        
        foreach (var go in all)
        {
            if (go == null) continue;
            if (go.name != name) continue;

            // 跳过 Prefab 资产引用
            #if UNITY_EDITOR
            if (!go.scene.IsValid()) continue;
            #endif

            try
            {
                if (destroyHiddenByName)
                {
                    if (enableDebugLog) Debug.Log($"[ButtonShowHide] 通过名称销毁: {name}");
                    Destroy(go);
                }
                else
                {
                    if (enableDebugLog) Debug.Log($"[ButtonShowHide] 通过名称隐藏: {name}");
                    go.SetActive(false);
                }
                found = true;
            }
            catch (System.Exception e)
            {
                if (enableDebugLog) Debug.LogWarning($"[ButtonShowHide] 隐藏 {name} 时出错: {e.Message}");
            }
        }

        if (!found && enableDebugLog)
        {
            Debug.LogWarning($"[ButtonShowHide] 未找到名为 '{name}' 的物体");
        }
    }
    
    // 在所有加载的资源对象中查找指定名字的 GameObject 并尝试激活（包含 inactive）
    private void ActivateObjectsByName(string name)
    {
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        bool found = false;
        
        foreach (var go in all)
        {
            if (go == null) continue;
            if (go.name != name) continue;

            // 跳过 Prefab 资产引用（通常在编辑器中位于 Assets 下）
            #if UNITY_EDITOR
            // 在编辑器环境中，Resources.FindObjectsOfTypeAll 可能返回项目资产中的 prefab；尝试通过 scene 判断是否为场景对象
            if (!go.scene.IsValid()) continue;
            #endif

            try
            {
                // 确保父链激活以便能看到对象
                ActivateHierarchyLocal(go.transform);
                go.SetActive(true);
                found = true;
                
                if (enableDebugLog)
                {
                    Debug.Log($"[ButtonShowHide] 通过名称显示: {name}");
                }
            }
            catch (System.Exception e)
            {
                if (enableDebugLog) Debug.LogWarning($"[ButtonShowHide] 激活 {name} 时出错: {e.Message}");
            }
        }

        if (!found && enableDebugLog)
        {
            Debug.LogWarning($"[ButtonShowHide] 未找到名为 '{name}' 的物体");
        }
    }

    private void ActivateHierarchyLocal(Transform t)
    {
        if (t == null) return;
        var cur = t.parent;
        while (cur != null)
        {
            if (!cur.gameObject.activeSelf) cur.gameObject.SetActive(true);
            cur = cur.parent;
        }
    }

    private void PlayClickSound()
    {
        if (audioSource == null) return;
        if (clickClip != null)
        {
            audioSource.PlayOneShot(clickClip);
            return;
        }

        if (audioSource.clip != null)
        {
            audioSource.Play();
        }
    }
    
    // 右键菜单：测试按钮功能
    [ContextMenu("测试按钮功能")]
    private void TestButton()
    {
        Debug.Log("[ButtonShowHide] 测试按钮功能");
        OnPressed();
    }
    
    // 右键菜单：显示配置信息
    [ContextMenu("显示配置信息")]
    private void ShowConfiguration()
    {
        Debug.Log("========== ButtonShowHidePlaySound 配置 ==========");
        Debug.Log($"通过引用隐藏: {(hideTargets != null ? hideTargets.Length : 0)} 个物体");
        Debug.Log($"通过引用显示: {(showTargets != null ? showTargets.Length : 0)} 个物体");
        Debug.Log($"通过名称隐藏: {(hideTargetNames != null ? hideTargetNames.Length : 0)} 个物体");
        Debug.Log($"通过名称显示: {(showTargetNames != null ? showTargetNames.Length : 0)} 个物体");
        Debug.Log($"销毁模式: 引用={destroyHidden}, 名称={destroyHiddenByName}");
        Debug.Log("================================================");
    }
}
