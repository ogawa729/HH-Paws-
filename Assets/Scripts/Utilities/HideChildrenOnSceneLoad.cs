using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// 在特定场景加载时隐藏/显示指定的子物体
/// 用于持久化物体在不同场景中显示不同的子物体
/// </summary>
public class HideChildrenOnSceneLoad : MonoBehaviour
{
    [System.Serializable]
    public class SceneRule
    {
        [Tooltip("场景名称（留空表示所有场景）")]
        public string sceneName = "";
        
        [Header("通过引用配置")]
        [Tooltip("在这个场景中要隐藏的物体（直接拖拽）")]
        public GameObject[] objectsToHide;
        
        [Tooltip("在这个场景中要显示的物体（直接拖拽）")]
        public GameObject[] objectsToShow;
        
        [Header("通过名称配置")]
        [Tooltip("在这个场景中要隐藏的物体名称（支持子物体，如 'parent/child'）")]
        public string[] objectNamesToHide;
        
        [Tooltip("在这个场景中要显示的物体名称（支持子物体，如 'parent/child'）")]
        public string[] objectNamesToShow;
    }
    
    [Header("场景规则")]
    [Tooltip("为不同场景配置不同的显示/隐藏规则")]
    public SceneRule[] sceneRules;
    
    [Header("默认规则 - 通过引用")]
    [Tooltip("在所有未配置的场景中要隐藏的物体")]
    public GameObject[] defaultHideObjects;
    
    [Tooltip("在所有未配置的场景中要显示的物体")]
    public GameObject[] defaultShowObjects;
    
    [Header("默认规则 - 通过名称")]
    [Tooltip("在所有未配置的场景中要隐藏的物体名称")]
    public string[] defaultHideObjectNames;
    
    [Tooltip("在所有未配置的场景中要显示的物体名称")]
    public string[] defaultShowObjectNames;
    
    [Header("调试")]
    public bool enableDebugLog = true;
    
    void Start()
    {
        // 监听场景加载事件
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // 处理当前场景
        ApplyRulesForScene(SceneManager.GetActiveScene().name);
    }
    
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyRulesForScene(scene.name);
    }
    
    void ApplyRulesForScene(string sceneName)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[HideChildren] 应用场景规则: {sceneName}");
        }
        
        // 查找匹配的场景规则
        SceneRule matchedRule = null;
        foreach (SceneRule rule in sceneRules)
        {
            if (rule.sceneName == sceneName)
            {
                matchedRule = rule;
                break;
            }
        }
        
        if (matchedRule != null)
        {
            // 应用匹配的规则
            HideObjects(matchedRule.objectsToHide);
            ShowObjects(matchedRule.objectsToShow);
            HideObjectsByName(matchedRule.objectNamesToHide);
            ShowObjectsByName(matchedRule.objectNamesToShow);
        }
        else
        {
            // 应用默认规则
            if (enableDebugLog)
            {
                Debug.Log($"[HideChildren] 未找到场景 {sceneName} 的规则，应用默认规则");
            }
            HideObjects(defaultHideObjects);
            ShowObjects(defaultShowObjects);
            HideObjectsByName(defaultHideObjectNames);
            ShowObjectsByName(defaultShowObjectNames);
        }
    }
    
    void HideObjects(GameObject[] objects)
    {
        if (objects == null) return;
        
        foreach (GameObject obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(false);
                if (enableDebugLog)
                {
                    Debug.Log($"[HideChildren] 隐藏: {obj.name}");
                }
            }
        }
    }
    
    void ShowObjects(GameObject[] objects)
    {
        if (objects == null) return;
        
        foreach (GameObject obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(true);
                if (enableDebugLog)
                {
                    Debug.Log($"[HideChildren] 显示: {obj.name}");
                }
            }
        }
    }
    
    void HideObjectsByName(string[] objectNames)
    {
        if (objectNames == null) return;
        
        foreach (string name in objectNames)
        {
            if (string.IsNullOrEmpty(name)) continue;
            
            GameObject obj = FindChildByName(name);
            if (obj != null)
            {
                obj.SetActive(false);
                if (enableDebugLog)
                {
                    Debug.Log($"[HideChildren] 通过名称隐藏: {name}");
                }
            }
            else
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning($"[HideChildren] 未找到物体: {name}");
                }
            }
        }
    }
    
    void ShowObjectsByName(string[] objectNames)
    {
        if (objectNames == null) return;
        
        foreach (string name in objectNames)
        {
            if (string.IsNullOrEmpty(name)) continue;
            
            GameObject obj = FindChildByName(name);
            if (obj != null)
            {
                obj.SetActive(true);
                if (enableDebugLog)
                {
                    Debug.Log($"[HideChildren] 通过名称显示: {name}");
                }
            }
            else
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning($"[HideChildren] 未找到物体: {name}");
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
        // 检查直接子物体
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child.gameObject;
            }
        }
        
        // 递归检查子物体的子物体
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
    
    // 手动应用当前场景的规则
    [ContextMenu("应用当前场景规则")]
    public void ApplyCurrentSceneRules()
    {
        ApplyRulesForScene(SceneManager.GetActiveScene().name);
    }
    
    // 显示所有物体
    [ContextMenu("显示所有物体")]
    public void ShowAllObjects()
    {
        foreach (SceneRule rule in sceneRules)
        {
            ShowObjects(rule.objectsToHide);
            ShowObjects(rule.objectsToShow);
        }
        ShowObjects(defaultHideObjects);
        ShowObjects(defaultShowObjects);
    }
}
