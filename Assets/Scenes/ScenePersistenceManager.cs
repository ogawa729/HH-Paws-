using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景物体持久化管理器 - 简化版
/// </summary>
public class ScenePersistenceManager : MonoBehaviour
{
    private static ScenePersistenceManager instance;

    public static ScenePersistenceManager Instance
    {
        get { return instance; }
    }

    [Header("持久化配置")]
    [Tooltip("输入需要保留的物体名称")]
    public string[] persistentObjectNames;

    [Header("调试选项")]
    public bool enableDebugLog = true;

    private HashSet<GameObject> persistentObjects = new HashSet<GameObject>();
    private bool isInitialized = false;

    void Awake()
    {
        // 严格的单例模式
        if (instance != null)
        {
            if (enableDebugLog) Debug.LogWarning("[Persistence] 已存在实例，销毁重复的管理器");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // 立即监听场景卸载事件（在 Start 之前）
        SceneManager.sceneUnloaded += OnSceneUnloaded;

        if (enableDebugLog) Debug.Log("[Persistence] ========== 管理器初始化 ==========");
    }

    void Start()
    {
        // 延迟初始化，确保场景完全加载
        StartCoroutine(Initialize());
        
        // 启动协程，定期检查持久化物体是否还在 DontDestroyOnLoad 中
        StartCoroutine(CheckPersistentObjectsLoop());
    }
    
    void OnSceneUnloaded(Scene scene)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[Persistence] ========== 场景卸载: {scene.name} ==========");
        }
        
        // 在场景卸载时，强制确保所有持久化物体都在 DontDestroyOnLoad 中
        List<GameObject> snapshot = new List<GameObject>(persistentObjects);
        
        foreach (GameObject obj in snapshot)
        {
            if (obj == null)
            {
                if (enableDebugLog)
                {
                    Debug.LogError($"[Persistence] ✗✗✗ 场景卸载时发现物体已经是 null！");
                    Debug.LogError($"[Persistence] 这说明物体在场景卸载前就被销毁了！");
                }
                continue;
            }
            
            if (enableDebugLog)
            {
                Debug.Log($"[Persistence] 检查 {obj.name}:");
                Debug.Log($"[Persistence]   当前场景: {obj.scene.name}");
                Debug.Log($"[Persistence]   InstanceID: {obj.GetInstanceID()}");
            }
            
            if (obj.scene.name != "DontDestroyOnLoad")
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning($"[Persistence] ⚠ {obj.name} 不在 DontDestroyOnLoad 中！立即保护");
                }
                
                // 确保父级为 null
                if (obj.transform.parent != null)
                {
                    if (enableDebugLog)
                    {
                        Debug.LogWarning($"[Persistence]   父级: {obj.transform.parent.name}，设置为 null");
                    }
                    obj.transform.SetParent(null);
                }
                
                DontDestroyOnLoad(obj);
                
                if (enableDebugLog)
                {
                    Debug.Log($"[Persistence] ✓ 已保护 {obj.name}");
                }
            }
            else
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[Persistence] ✓ {obj.name} 已在 DontDestroyOnLoad 中");
                }
            }
        }
        
        if (enableDebugLog)
        {
            Debug.Log($"[Persistence] ========== 场景卸载处理完成 ==========");
        }
    }
    
    // 定期检查持久化物体是否被移出 DontDestroyOnLoad（例如被 XR 抓取放下后）
    IEnumerator CheckPersistentObjectsLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f); // 每0.5秒检查一次
            
            // 创建快照避免在遍历时修改集合
            List<GameObject> snapshot = new List<GameObject>(persistentObjects);
            
            foreach (GameObject obj in snapshot)
            {
                if (obj == null) continue;
                
                // 如果物体被移出 DontDestroyOnLoad，重新标记
                if (obj.scene.name != "DontDestroyOnLoad")
                {
                    if (enableDebugLog)
                    {
                        Debug.LogWarning($"[Persistence] ⚠ {obj.name} 被移出 DontDestroyOnLoad！当前场景: {obj.scene.name}");
                        Debug.Log($"[Persistence] 重新标记 {obj.name} 为持久化");
                    }
                    
                    // 确保父级为 null
                    if (obj.transform.parent != null)
                    {
                        obj.transform.SetParent(null);
                    }
                    
                    // 重新标记为 DontDestroyOnLoad
                    DontDestroyOnLoad(obj);
                }
            }
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
        
        // 取消监听
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    IEnumerator Initialize()
    {
        // 等待两帧，确保所有物体都已加载
        yield return null;
        yield return null;

        if (isInitialized)
        {
            yield break;
        }

        isInitialized = true;

        if (enableDebugLog) Debug.Log("[Persistence] ========== 开始处理持久化物体 ==========");

        if (persistentObjectNames == null || persistentObjectNames.Length == 0)
        {
            if (enableDebugLog) Debug.LogWarning("[Persistence] 没有配置需要持久化的物体");
            yield break;
        }

        foreach (string objName in persistentObjectNames)
        {
            if (string.IsNullOrEmpty(objName)) continue;

            // 查找物体
            GameObject obj = FindObjectByName(objName);

            if (obj == null)
            {
                if (enableDebugLog) Debug.LogError($"[Persistence] ✗ 未找到物体: {objName}");
                continue;
            }

            // 标记为持久化
            MakePersistent(obj);
        }

        if (enableDebugLog)
        {
            Debug.Log($"[Persistence] ========== 初始化完成，共保留 {persistentObjects.Count} 个物体 ==========");
        }

        // 监听场景加载
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 查找物体（包括 inactive 的）
    GameObject FindObjectByName(string name)
    {
        // 查找所有物体（包括 inactive）
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        if (enableDebugLog)
        {
            Debug.Log($"[Persistence] 开始查找物体: {name}，共扫描 {allObjects.Length} 个物体");
        }

        foreach (GameObject obj in allObjects)
        {
            // 跳过 Prefab 和资源
            if (!obj.scene.IsValid()) continue;
            
            // 跳过已经在 DontDestroyOnLoad 场景中的物体
            if (obj.scene.name == "DontDestroyOnLoad") continue;

            // 调试：打印所有场景中的物体名称
            if (enableDebugLog && obj.name.Contains("table"))
            {
                Debug.Log($"[Persistence] 找到包含 'table' 的物体: {obj.name} (场景: {obj.scene.name})");
            }

            if (obj.name == name)
            {
                return obj;
            }
        }

        if (enableDebugLog)
        {
            Debug.LogWarning($"[Persistence] 查找完成，未找到名为 '{name}' 的物体");
        }

        return null;
    }

    // 标记物体为持久化
    void MakePersistent(GameObject obj)
    {
        if (obj == null) return;

        // 检查是否已经持久化
        if (persistentObjects.Contains(obj))
        {
            if (enableDebugLog) Debug.Log($"[Persistence] 物体已持久化: {obj.name}");
            return;
        }

        // 获取信息
        int childCount = obj.transform.childCount;
        bool isActive = obj.activeSelf;
        Vector3 pos = obj.transform.position;
        int instanceID = obj.GetInstanceID();

        // 检查物体上是否有可能导致销毁的脚本
        var persistentAcrossScenes = obj.GetComponent<PersistentAcrossScenes>();
        if (persistentAcrossScenes != null && enableDebugLog)
        {
            Debug.LogWarning($"[Persistence] ⚠ {obj.name} 上有 PersistentAcrossScenes 脚本，可能会冲突！");
        }

        // 标记为持久化
        DontDestroyOnLoad(obj);
        persistentObjects.Add(obj);

        if (enableDebugLog)
        {
            string status = isActive ? "active" : "inactive";
            Debug.Log($"[Persistence] ✓ 保留物体: {obj.name} ({status}, {childCount} 个子物体)");
            Debug.Log($"[Persistence]   位置: {pos}");
            Debug.Log($"[Persistence]   InstanceID: {instanceID}");
            Debug.Log($"[Persistence]   场景: {obj.scene.name}");
        }
    }

    // 场景加载回调
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[Persistence] ========== 场景加载: {scene.name} ==========");
        }

        // 检查新场景中是否有与持久化物体同名的物体
        StartCoroutine(CheckForDuplicatesInNewScene(scene));
        
        StartCoroutine(ValidateAfterSceneLoad());
    }
    
    // 检查新场景中是否有重复的物体
    IEnumerator CheckForDuplicatesInNewScene(Scene scene)
    {
        yield return new WaitForEndOfFrame();
        
        if (enableDebugLog)
        {
            Debug.Log($"[Persistence] 检查新场景 {scene.name} 中是否有重复物体");
        }
        
        // 获取新场景中的所有根物体
        GameObject[] rootObjects = scene.GetRootGameObjects();
        
        foreach (GameObject persistentObj in persistentObjects)
        {
            if (persistentObj == null) continue;
            
            // 在新场景中查找同名物体
            foreach (GameObject sceneObj in rootObjects)
            {
                if (sceneObj.name == persistentObj.name)
                {
                    if (enableDebugLog)
                    {
                        Debug.LogWarning($"[Persistence] ⚠ 新场景中发现重复物体: {sceneObj.name}");
                        Debug.LogWarning($"[Persistence]   持久化物体 InstanceID: {persistentObj.GetInstanceID()}");
                        Debug.LogWarning($"[Persistence]   场景物体 InstanceID: {sceneObj.GetInstanceID()}");
                        Debug.LogWarning($"[Persistence]   将销毁场景中的副本");
                    }
                    
                    // 销毁场景中的副本，保留持久化的
                    Destroy(sceneObj);
                }
            }
        }
    }

    IEnumerator ValidateAfterSceneLoad()
    {
        // 等待场景完全加载
        yield return new WaitForEndOfFrame();

        int validCount = 0;
        int missingCount = 0;

        // 复制快照以避免在枚举时修改集合（遍历 HashSet 直接删除会抛异常或行为不确定）
        List<GameObject> snapshot = new List<GameObject>(persistentObjects);

        foreach (GameObject obj in snapshot)
        {
            if (obj != null)
            {
                validCount++;
                if (enableDebugLog)
                {
                    Debug.Log($"[Persistence] ✓ {obj.name} 仍然存在");
                    Debug.Log($"[Persistence]   场景: {obj.scene.name}");
                    Debug.Log($"[Persistence]   子物体数: {obj.transform.childCount}");
                }
            }
            else
            {
                missingCount++;
                if (enableDebugLog)
                {
                    Debug.LogError($"[Persistence] ✗ 物体变成 missing！");
                }
            }
        }

        // 从 HashSet 中移除所有已被销毁（== null）的引用；RemoveWhere 返回移除的数量
        int removed = persistentObjects.RemoveWhere(go => go == null);
        if (enableDebugLog && removed != missingCount)
        {
            Debug.LogWarning($"[Persistence] 清理 missing 引用数量不一致：RemoveWhere 移除 {removed} 项，但检测到 missingCount={missingCount}");
        }

        if (enableDebugLog)
        {
            if (missingCount > 0)
            {
                Debug.LogError($"[Persistence] ========== 验证失败！{missingCount} 个物体 missing，{validCount} 个有效 ==========");
            }
            else
            {
                Debug.Log($"[Persistence] ========== 验证通过！共 {validCount} 个物体 ==========");
            }
        }
    }

    /// <summary>
    /// 清理所有持久化物体
    /// </summary>
    public void ClearAllPersistentObjects()
    {
        foreach (GameObject obj in persistentObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        persistentObjects.Clear();
        isInitialized = false;

        if (enableDebugLog)
        {
            Debug.Log("[Persistence] 已清理所有持久化物体");
        }
    }

    /// <summary>
    /// 运行时添加持久化物体
    /// </summary>
    public void AddPersistentObject(GameObject obj)
    {
        if (obj == null) return;
        MakePersistent(obj);
    }

    /// <summary>
    /// 获取所有持久化物体
    /// </summary>
    public List<GameObject> GetPersistentObjects()
    {
        List<GameObject> result = new List<GameObject>();
        foreach (GameObject obj in persistentObjects)
        {
            if (obj != null)
            {
                result.Add(obj);
            }
        }
        return result;
    }
    
    /// <summary>
    /// 强制保护所有持久化物体（在场景切换前调用）
    /// </summary>
    public IEnumerator ForceProtectAllObjects()
    {
        if (enableDebugLog)
        {
            Debug.Log("[Persistence] ========== 强制保护所有持久化物体 ==========");
        }
        
        foreach (GameObject obj in persistentObjects)
        {
            if (obj == null)
            {
                if (enableDebugLog)
                {
                    Debug.LogError("[Persistence] ✗ 发现 null 物体！");
                }
                continue;
            }
            
            // 确保物体在 DontDestroyOnLoad 场景中
            if (obj.scene.name != "DontDestroyOnLoad")
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning($"[Persistence] ⚠ {obj.name} 不在 DontDestroyOnLoad 中！");
                    Debug.LogWarning($"[Persistence]   当前场景: {obj.scene.name}");
                    Debug.LogWarning($"[Persistence]   InstanceID: {obj.GetInstanceID()}");
                }
                
                // 确保父级为 null
                if (obj.transform.parent != null)
                {
                    if (enableDebugLog)
                    {
                        Debug.LogWarning($"[Persistence]   父级不为 null: {obj.transform.parent.name}，设置为 null");
                    }
                    obj.transform.SetParent(null);
                }
                
                // 重新标记为 DontDestroyOnLoad
                DontDestroyOnLoad(obj);
                
                if (enableDebugLog)
                {
                    Debug.Log($"[Persistence] ✓ 已重新保护 {obj.name}");
                }
            }
            else
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[Persistence] ✓ {obj.name} 已在 DontDestroyOnLoad 中");
                }
            }
        }
        
        yield return null;
    }
}

