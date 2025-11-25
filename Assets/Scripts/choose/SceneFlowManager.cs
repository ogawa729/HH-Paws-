using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景流程管理器 - 简化版（移除新手引导功能）
/// </summary>
public class SceneFlowManager : MonoBehaviour
{
    private static SceneFlowManager instance;

    public static SceneFlowManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("SceneFlowManager");
                instance = go.AddComponent<SceneFlowManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [System.Serializable]
    public class LevelInfo
    {
        public string levelName;           // 关卡名称（用于数据记录）
        public string sceneName;           // 场景名称
        public GameObject[] sceneSpecificPrefabs; // 场景特定预制体
    }

    [Header("关卡配置")]
    public List<LevelInfo> allLevels = new List<LevelInfo>(); // 所有可用关卡

    [Header("持久化桌面和场景特定预制体")]
    public GameObject[] persistentDeskPrefabs;     // 持久化桌面预制体
    public GameObject[] sceneSpecificPrefabs;      // 当前场景特定预制体

    [Header("桌面初始物体移除(切换场景时)")]
    public GameObject[] initialDeskObjects;        // 桌面初始物体（切换场景时移除）

    [Header("隐藏的持久化物体")]
    public GameObject[] hiddenPersistentObjects;   // 隐藏的持久化物体

    [Header("持久化物体")]
    public GameObject[] persistentObjects;         // 持久化物体

    [Tooltip("当在新场景中出现与持久化对象同名的场景对象时，是否优先保留持久化对象并移除场景中的副本（true），否则移除持久化对象（false）")]
    public bool preferPersistentOverSceneCopies = true;

    [Header("调试")]
    public bool enableDebugLog = true;             // 启用调试日志

    // 多关卡系统变量
    private Queue<LevelInfo> levelQueue = new Queue<LevelInfo>(); // 待游玩的关卡队列
    private LevelInfo currentLevel;                               // 当前关卡
    private float levelStartTime;                                 // 关卡开始时间
    private string returnSceneName = "MainMenu";                  // 返回的场景名称
    private int currentSessionID = 0;                             // 当前游玩会话ID（用于区分不同的游玩会话）

    // 持久化物体管理变量
    private List<GameObject> instantiatedPersistentObjects = new List<GameObject>();
    private List<GameObject> instantiatedDeskObjects = new List<GameObject>();
    private List<GameObject> instantiatedSceneSpecificObjects = new List<GameObject>();
    private bool hasInitialized = false; // 初始化标记

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // 初始化持久化物体
        InitializePersistentObjects();
    }

    void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoadedPersistent;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedPersistent;
    }

    #region 多关卡系统

    // 开始游玩选中的关卡
    public void StartSelectedLevels(List<int> selectedLevelIndices, string returnScene = "MainMenu")
    {
        if (selectedLevelIndices == null || selectedLevelIndices.Count == 0)
        {
            if (enableDebugLog) Debug.LogWarning("[SceneFlow] 没有选择任何关卡");
            return;
        }

        returnSceneName = returnScene;
        levelQueue.Clear();

        // 按顺序添加选中的关卡到队列
        foreach (int index in selectedLevelIndices)
        {
            if (index >= 0 && index < allLevels.Count)
            {
                levelQueue.Enqueue(allLevels[index]);
                if (enableDebugLog) Debug.Log($"[SceneFlow] 添加关卡到队列: {allLevels[index].levelName}");
            }
        }

        // 生成新的会话ID（用于 ShowOnFirstLevelOnly 等脚本判断是否是新的游玩会话）
        currentSessionID++;
        if (enableDebugLog)
        {
            Debug.Log($"[SceneFlow] 开始新的游玩会话，会话ID: {currentSessionID}");
        }

        // 开始第一个关卡
        LoadNextLevel();
    }

    // 加载下一个关卡
    void LoadNextLevel()
    {
        if (levelQueue.Count == 0)
        {
            if (enableDebugLog) Debug.Log("[SceneFlow] 所有关卡完成，返回主菜单");
            ReturnToMenu();
            return;
        }

        currentLevel = levelQueue.Dequeue();
        if (enableDebugLog) Debug.Log($"[SceneFlow] 加载关卡: {currentLevel.levelName}, 剩余关卡: {levelQueue.Count}");

        // 记录关卡开始
        GameDataManager.Instance.RecordLevelStart(currentLevel.levelName);
        levelStartTime = Time.time;

        // 在加载场景前，确保所有持久化物体都安全（必须在 RemoveInitialDeskObjects 之前）
        EnsureAllPersistentObjectsSafe();
        
        // 通知 ScenePersistenceManager 也保护一下
        if (ScenePersistenceManager.Instance != null)
        {
            StartCoroutine(ScenePersistenceManager.Instance.ForceProtectAllObjects());
        }

        // 移除桌面初始物体（切换场景时）
        RemoveInitialDeskObjects();

        // 场景加载完成后的回调（必须在 LoadScene 之前注册）
        SceneManager.sceneLoaded += OnSceneLoadedForLevel;

        // 使用异步加载场景（更安全，不会意外销毁 DontDestroyOnLoad 物体）
        SceneManager.LoadSceneAsync(currentLevel.sceneName, LoadSceneMode.Single);
    }

    // 场景加载完成（关卡系统）
    void OnSceneLoadedForLevel(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedForLevel;
        if (enableDebugLog) Debug.Log($"[SceneFlow] 关卡场景加载完成: {scene.name}");

        // 实例化当前关卡的场景特定预制体
        InstantiateCurrentLevelPrefabs();
    }

    // 实例化当前关卡的场景特定预制体
    void InstantiateCurrentLevelPrefabs()
    {
        if (currentLevel != null && currentLevel.sceneSpecificPrefabs != null)
        {
            foreach (GameObject prefab in currentLevel.sceneSpecificPrefabs)
            {
                if (prefab != null)
                {
                    GameObject instance = Instantiate(prefab);
                    instantiatedSceneSpecificObjects.Add(instance);
                    if (enableDebugLog) Debug.Log($"[SceneFlow] 实例化关卡特定预制体: {prefab.name}");
                }
            }
        }
    }

    // 当前关卡完成
    public void CompleteCurrentLevel()
    {
        if (currentLevel == null)
        {
            if (enableDebugLog) Debug.LogWarning("[SceneFlow] 没有当前关卡");
            return;
        }

        // 计算游玩时间
        float playTime = Time.time - levelStartTime;

        // 记录关卡完成
        GameDataManager.Instance.RecordLevelComplete(currentLevel.levelName, playTime);
        if (enableDebugLog) Debug.Log($"[SceneFlow] 关卡 {currentLevel.levelName} 完成");

        // 清理当前关卡的场景特定物体
        CleanupSceneSpecificObjects();

        // 加载下一个关卡
        LoadNextLevel();
    }

    // 返回主菜单
    public void ReturnToMenu()
    {
        levelQueue.Clear();
        currentLevel = null;

        // 清理场景特定物体
        CleanupSceneSpecificObjects();

        // 重置持久化物体
        ResetPersistentObjects();

        // 清理 ScenePersistenceManager 的持久化物体
        if (ScenePersistenceManager.Instance != null)
        {
            ScenePersistenceManager.Instance.ClearAllPersistentObjects();
        }

        // 标记不再是第一次游玩
        GameDataManager.Instance.MarkNotFirstTime();

        if (enableDebugLog) Debug.Log($"[SceneFlow] 返回场景: {returnSceneName}");
        SceneManager.LoadScene(returnSceneName);
    }

    // 清理场景特定物体
    void CleanupSceneSpecificObjects()
    {
        foreach (GameObject obj in instantiatedSceneSpecificObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        instantiatedSceneSpecificObjects.Clear();
        if (enableDebugLog) Debug.Log("[SceneFlow] 清理场景特定物体");
    }

    // 重置持久化物体
    void ResetPersistentObjects()
    {
        // 清理所有持久化物体
        foreach (GameObject obj in instantiatedPersistentObjects)
        {
            if (obj != null) Destroy(obj);
        }
        instantiatedPersistentObjects.Clear();

        foreach (GameObject obj in instantiatedDeskObjects)
        {
            if (obj != null) Destroy(obj);
        }
        instantiatedDeskObjects.Clear();

        // 重置初始化标记，以便重新初始化
        hasInitialized = false;

        if (enableDebugLog) Debug.Log("[SceneFlow] 重置持久化物体");
    }

    #endregion

    #region 持久化物体管理

    // 初始化持久化物体
    void InitializePersistentObjects()
    {
        // 如果已经初始化过，直接返回（避免重复初始化）
        if (hasInitialized)
        {
            if (enableDebugLog) Debug.Log("[SceneFlow] 持久化物体已初始化，跳过");
            return;
        }

        hasInitialized = true;

        // 实例化持久化物体
        if (persistentObjects != null)
        {
            foreach (GameObject prefab in persistentObjects)
            {
                if (prefab != null)
                {
                    // 首先尝试在当前已加载的场景中找到同名的对象（仅第一次）
                    GameObject found = null;
                    var all = Resources.FindObjectsOfTypeAll<GameObject>();
                    foreach (var go in all)
                    {
                        if (go == null) continue;
                        if (!go.scene.IsValid()) continue; // 跳过项目资源
                        if (go.name == prefab.name)
                        {
                            found = go;
                            break;
                        }
                    }

                    if (found != null)
                    {
                        // 将场景中的对象标记为持久化并加入 registry
                        DontDestroyOnLoad(found);
                        instantiatedPersistentObjects.Add(found);
                        if (enableDebugLog) Debug.Log($"[SceneFlow] 复用现有场景对象作为持久化物体: {found.name}");
                    }
                    else
                    {
                        GameObject instance = Instantiate(prefab);
                        instance.name = prefab.name; // 去掉 "(Clone)" 后缀
                        DontDestroyOnLoad(instance);
                        instantiatedPersistentObjects.Add(instance);
                        if (enableDebugLog) Debug.Log($"[SceneFlow] 创建持久化物体: {prefab.name}");
                    }
                }
            }
        }

        // 实例化持久化桌面物体
        if (persistentDeskPrefabs != null)
        {
            foreach (GameObject prefab in persistentDeskPrefabs)
            {
                if (prefab != null)
                {
                    GameObject instance = Instantiate(prefab);
                    instance.name = prefab.name; // 去掉 "(Clone)" 后缀
                    DontDestroyOnLoad(instance);
                    instantiatedDeskObjects.Add(instance);
                    if (enableDebugLog) Debug.Log($"[SceneFlow] 创建持久化桌面物体: {prefab.name}");
                }
            }
        }

        // 隐藏指定的持久化物体
        if (hiddenPersistentObjects != null)
        {
            foreach (GameObject obj in hiddenPersistentObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    if (enableDebugLog) Debug.Log($"[SceneFlow] 隐藏持久化物体: {obj.name}");
                }
            }
        }
    }

    // 场景加载完成（持久化系统）
    void OnSceneLoadedPersistent(Scene scene, LoadSceneMode mode)
    {
        if (enableDebugLog) Debug.Log($"[SceneFlow] 场景加载: {scene.name}");

        // 实例化场景特定预制体（非关卡模式）
        if (currentLevel == null && sceneSpecificPrefabs != null)
        {
            InstantiateSceneSpecificPrefabs();
        }
    }

    // 实例化场景特定预制体
    void InstantiateSceneSpecificPrefabs()
    {
        // 清理之前的场景特定物体
        CleanupSceneSpecificObjects();

        foreach (GameObject prefab in sceneSpecificPrefabs)
        {
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab);
                instantiatedSceneSpecificObjects.Add(instance);
                if (enableDebugLog) Debug.Log($"[SceneFlow] 实例化场景特定预制体: {prefab.name}");
            }
        }
    }

    // 确保所有持久化物体都在 DontDestroyOnLoad 场景中
    void EnsureAllPersistentObjectsSafe()
    {
        if (enableDebugLog)
        {
            Debug.Log("[SceneFlow] 场景切换前，确保所有持久化物体安全");
        }
        
        // 检查并保护所有实例化的持久化物体
        foreach (GameObject obj in instantiatedPersistentObjects)
        {
            if (obj != null && obj.scene.name != "DontDestroyOnLoad")
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning($"[SceneFlow] {obj.name} 不在 DontDestroyOnLoad 中，重新标记");
                }
                if (obj.transform.parent != null) obj.transform.SetParent(null);
                DontDestroyOnLoad(obj);
            }
        }
        
        // 检查并保护所有桌面物体
        foreach (GameObject obj in instantiatedDeskObjects)
        {
            if (obj != null && obj.scene.name != "DontDestroyOnLoad")
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning($"[SceneFlow] {obj.name} 不在 DontDestroyOnLoad 中，重新标记");
                }
                if (obj.transform.parent != null) obj.transform.SetParent(null);
                DontDestroyOnLoad(obj);
            }
        }
        
        // 通知 ScenePersistenceManager 也检查一下
        if (ScenePersistenceManager.Instance != null)
        {
            // ScenePersistenceManager 的 CheckPersistentObjectsLoop 会自动处理
            if (enableDebugLog)
            {
                Debug.Log("[SceneFlow] ScenePersistenceManager 也在保护持久化物体");
            }
        }
    }
    
    // 移除桌面初始物体
    void RemoveInitialDeskObjects()
    {
        if (enableDebugLog)
        {
            Debug.Log("[SceneFlow] 准备移除桌面初始物体");
        }
        
        if (initialDeskObjects != null)
        {
            foreach (GameObject obj in initialDeskObjects)
            {
                if (obj != null)
                {
                    // 检查是否是持久化物体（在 DontDestroyOnLoad 场景中）
                    if (obj.scene.name == "DontDestroyOnLoad")
                    {
                        if (enableDebugLog) Debug.Log($"[SceneFlow] 跳过持久化物体: {obj.name}");
                        continue;
                    }

                    obj.SetActive(false);
                    if (enableDebugLog) Debug.Log($"[SceneFlow] 移除桌面初始物体: {obj.name}");
                }
            }
        }
    }

    // 恢复桌面初始物体
    public void RestoreInitialDeskObjects()
    {
        if (initialDeskObjects != null)
        {
            foreach (GameObject obj in initialDeskObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    if (enableDebugLog) Debug.Log($"[SceneFlow] 恢复桌面初始物体: {obj.name}");
                }
            }
        }
    }

    // 显示隐藏的持久化物体
    public void ShowHiddenPersistentObjects()
    {
        if (hiddenPersistentObjects != null)
        {
            foreach (GameObject obj in hiddenPersistentObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    if (enableDebugLog) Debug.Log($"[SceneFlow] 显示隐藏的持久化物体: {obj.name}");
                }
            }
        }
    }

    // 隐藏持久化物体
    public void HideHiddenPersistentObjects()
    {
        if (hiddenPersistentObjects != null)
        {
            foreach (GameObject obj in hiddenPersistentObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    if (enableDebugLog) Debug.Log($"[SceneFlow] 隐藏持久化物体: {obj.name}");
                }
            }
        }
    }

    #endregion

    #region 公共接口

    // 获取剩余关卡数量
    public int GetRemainingLevelCount()
    {
        return levelQueue.Count;
    }

    // 获取当前关卡名称
    public string GetCurrentLevelName()
    {
        return currentLevel != null ? currentLevel.levelName : "";
    }

    // 检查是否在关卡模式
    public bool IsInLevelMode()
    {
        return currentLevel != null;
    }
    
    // 获取当前会话ID（用于 ShowOnFirstLevelOnly 等脚本判断是否是新的游玩会话）
    public int GetCurrentSessionID()
    {
        return currentSessionID;
    }

    // 直接加载场景（非关卡模式）
    public void LoadScene(string sceneName)
    {
        if (enableDebugLog) Debug.Log($"[SceneFlow] 直接加载场景: {sceneName}");

        // 清理关卡相关数据
        levelQueue.Clear();
        currentLevel = null;

        SceneManager.LoadScene(sceneName);
    }

    // 设置场景特定预制体（运行时）
    public void SetSceneSpecificPrefabs(GameObject[] prefabs)
    {
        sceneSpecificPrefabs = prefabs;
        if (enableDebugLog) Debug.Log($"[SceneFlow] 设置场景特定预制体数量: {prefabs?.Length ?? 0}");
    }

    #endregion
}


