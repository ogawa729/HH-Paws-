using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 只在本次游玩的第一个关卡中显示物体
/// 配合 SceneFlowManager 使用
/// 支持场景中的物体（非持久化）
/// </summary>
public class ShowOnFirstLevelOnly : MonoBehaviour
{
    [Header("要控制的物体")]
    [Tooltip("要在第一个关卡显示的物体（留空则控制自己）")]
    public GameObject[] objectsToShow;
    
    [Tooltip("通过名称指定要显示的物体")]
    public string[] objectNamesToShow;
    
    [Header("行为设置")]
    [Tooltip("是否在非第一关卡时隐藏物体（false 则保持原状态）")]
    public bool hideInOtherLevels = true;
    
    [Header("调试")]
    public bool enableDebugLog = true;
    
    // 使用静态变量记录全局状态（跨场景保持）
    private static bool hasEnteredFirstLevel = false;
    private static int currentSessionID = -1; // 用于区分不同的游玩会话
    
    void Start()
    {
        // 监听场景加载事件
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // 检查当前场景
        CheckAndApply();
    }
    
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckAndApply();
    }
    
    void CheckAndApply()
    {
        // 检查是否在关卡模式中
        if (SceneFlowManager.Instance == null)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning("[ShowOnFirstLevel] SceneFlowManager 不存在");
            }
            return;
        }
        
        bool isInLevelMode = SceneFlowManager.Instance.IsInLevelMode();
        
        if (!isInLevelMode)
        {
            if (enableDebugLog)
            {
                Debug.Log("[ShowOnFirstLevel] 不在关卡模式中，跳过");
            }
            return;
        }
        
        // 获取当前会话ID（用于区分不同的游玩会话）
        int sessionID = SceneFlowManager.Instance.GetCurrentSessionID();
        
        // 如果是新的会话，重置状态
        if (sessionID != currentSessionID)
        {
            currentSessionID = sessionID;
            hasEnteredFirstLevel = false;
            if (enableDebugLog)
            {
                Debug.Log($"[ShowOnFirstLevel] 新的游玩会话 (ID: {sessionID})，重置状态");
            }
        }
        
        // 获取剩余关卡数量
        int remainingLevels = SceneFlowManager.Instance.GetRemainingLevelCount();
        string currentLevelName = SceneFlowManager.Instance.GetCurrentLevelName();
        
        if (enableDebugLog)
        {
            Debug.Log($"[ShowOnFirstLevel] 当前关卡: {currentLevelName}");
            Debug.Log($"[ShowOnFirstLevel] 剩余关卡: {remainingLevels}");
            Debug.Log($"[ShowOnFirstLevel] 已进入第一关卡: {hasEnteredFirstLevel}");
        }
        
        // 判断是否是第一个关卡
        bool isFirstLevel = !hasEnteredFirstLevel;
        
        if (isFirstLevel)
        {
            // 第一个关卡：显示物体
            ShowObjects();
            hasEnteredFirstLevel = true; // 标记已经进入过第一个关卡
            if (enableDebugLog)
            {
                Debug.Log("[ShowOnFirstLevel] 这是第一个关卡，显示物体");
            }
        }
        else
        {
            // 后续关卡：隐藏物体（如果设置了）
            if (hideInOtherLevels)
            {
                HideObjects();
                if (enableDebugLog)
                {
                    Debug.Log("[ShowOnFirstLevel] 这是后续关卡，隐藏物体");
                }
            }
        }
    }
    
    void ShowObjects()
    {
        // 显示通过引用指定的物体
        if (objectsToShow != null && objectsToShow.Length > 0)
        {
            foreach (GameObject obj in objectsToShow)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    if (enableDebugLog)
                    {
                        Debug.Log($"[ShowOnFirstLevel] 显示: {obj.name}");
                    }
                }
            }
        }
        else
        {
            // 如果没有指定物体，显示自己
            gameObject.SetActive(true);
            if (enableDebugLog)
            {
                Debug.Log($"[ShowOnFirstLevel] 显示自己: {gameObject.name}");
            }
        }
        
        // 显示通过名称指定的物体
        if (objectNamesToShow != null)
        {
            foreach (string name in objectNamesToShow)
            {
                if (string.IsNullOrEmpty(name)) continue;
                
                GameObject obj = FindChildByName(name);
                if (obj != null)
                {
                    obj.SetActive(true);
                    if (enableDebugLog)
                    {
                        Debug.Log($"[ShowOnFirstLevel] 通过名称显示: {name}");
                    }
                }
                else
                {
                    if (enableDebugLog)
                    {
                        Debug.LogWarning($"[ShowOnFirstLevel] 未找到物体: {name}");
                    }
                }
            }
        }
    }
    
    void HideObjects()
    {
        // 隐藏通过引用指定的物体
        if (objectsToShow != null && objectsToShow.Length > 0)
        {
            foreach (GameObject obj in objectsToShow)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    if (enableDebugLog)
                    {
                        Debug.Log($"[ShowOnFirstLevel] 隐藏: {obj.name}");
                    }
                }
            }
        }
        else
        {
            // 如果没有指定物体，隐藏自己
            gameObject.SetActive(false);
            if (enableDebugLog)
            {
                Debug.Log($"[ShowOnFirstLevel] 隐藏自己: {gameObject.name}");
            }
        }
        
        // 隐藏通过名称指定的物体
        if (objectNamesToShow != null)
        {
            foreach (string name in objectNamesToShow)
            {
                if (string.IsNullOrEmpty(name)) continue;
                
                GameObject obj = FindChildByName(name);
                if (obj != null)
                {
                    obj.SetActive(false);
                    if (enableDebugLog)
                    {
                        Debug.Log($"[ShowOnFirstLevel] 通过名称隐藏: {name}");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 查找子物体，支持路径格式 "parent/child" 或直接名称 "child"
    /// </summary>
    GameObject FindChildByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        
        // 如果包含 '/'，使用 Transform.Find（支持路径）
        if (name.Contains("/"))
        {
            Transform found = transform.Find(name);
            return found != null ? found.gameObject : null;
        }
        
        // 否则递归查找所有子物体
        return FindChildRecursive(transform, name);
    }
    
    /// <summary>
    /// 递归查找子物体
    /// </summary>
    GameObject FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child.gameObject;
            }
        }
        
        foreach (Transform child in parent)
        {
            GameObject found = FindChildRecursive(child, name);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 重置状态（用于测试或重新开始游戏）
    /// 注意：这个方法现在由 SceneFlowManager 通过会话ID自动管理
    /// </summary>
    public void ResetState()
    {
        // 静态变量会在新会话时自动重置，这里保留方法以保持兼容性
        if (enableDebugLog)
        {
            Debug.Log("[ShowOnFirstLevel] ResetState 被调用（现在由会话ID自动管理）");
        }
    }
    
    /// <summary>
    /// 手动重置全局状态（用于调试）
    /// </summary>
    [ContextMenu("强制重置全局状态")]
    public void ForceResetGlobalState()
    {
        hasEnteredFirstLevel = false;
        currentSessionID = -1;
        if (enableDebugLog)
        {
            Debug.Log("[ShowOnFirstLevel] 全局状态已强制重置");
        }
    }
}
