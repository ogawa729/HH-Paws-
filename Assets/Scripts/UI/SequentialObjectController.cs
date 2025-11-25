using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SequentialObjectController : MonoBehaviour
{
    [System.Serializable]
    public class ObjectGroup
    {
        public string groupName;               // 组名（方便识别）
        public GameObject[] objects;           // 这一组的物体
        public float delayBeforeNextGroup = 1f; // 这组消失后，下一组出现前的延迟
    }

    [Header("物体组设置")]
    public List<ObjectGroup> objectGroups = new List<ObjectGroup>(); // 物体组列表（3组，每组5个）

    [Header("完成后显示的物体")]
    public GameObject[] finalObjectsToShow;    // 15个物体都消失后显示的物体
    public string[] finalObjectNamesToShow;    // 通过名字显示的最终物体

    [Header("完成后消失的物体")]
    public GameObject[] finalObjectsToHide;    // 15个物体都消失后隐藏的物体
    public string[] finalObjectNamesToHide;    // 通过名字隐藏的最终物体

    [Header("UI 引用")]
    public TextMeshProUGUI counterText;        // 计数文本（显示 1/15）
    public Button startButton;                 // 开始按钮（可选）

    [Header("设置")]
    public bool autoStart = false;             // 是否自动开始
    public bool hideCounterWhenComplete = false; // 完成后是否隐藏计数器
    public float checkInterval = 0.5f;         // 检测物体状态的间隔（秒）
    public bool enableDebugLog = true;         // 是否启用调试日志

    [Header("搜索范围")]
    public Transform searchRoot;               // 搜索物体的根节点

    private int totalObjectCount = 0;          // 总物体数量
    private int disappearedCount = 0;          // 已消失的物体数量
    private int currentGroupIndex = 0;         // 当前激活的组索引
    private bool isRunning = false;
    private bool isTransitioning = false;      // 是否正在切换组
    private Dictionary<string, GameObject> cachedObjects = new Dictionary<string, GameObject>();
    private HashSet<GameObject> disappearedObjects = new HashSet<GameObject>(); // 已消失的物体

    void Start()
    {
        // 计算总物体数量
        foreach (ObjectGroup group in objectGroups)
        {
            totalObjectCount += group.objects.Length;
        }

        if (enableDebugLog)
        {
            Debug.Log($"[SequentialController] 总物体数量: {totalObjectCount}");
        }

        // 初始化计数器
        UpdateCounter();

        // 隐藏所有物体组（除了第一组）
        for (int i = 1; i < objectGroups.Count; i++)
        {
            HideGroup(i);
        }

        // 隐藏最终显示的物体
        HideFinalShowObjects();
        
        // 确保完成后要消失的物体是显示的（初始状态）
        // 不需要特别处理，保持它们当前的状态

        // 设置开始按钮
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartSequence);
        }

        // 自动开始
        if (autoStart)
        {
            StartSequence();
        }
    }

    public void StartSequence()
    {
        if (isRunning)
        {
            if (enableDebugLog) Debug.Log("[SequentialController] 已经在运行中");
            return;
        }
        
        isRunning = true;
        currentGroupIndex = 0;
        disappearedCount = 0;
        disappearedObjects.Clear();
        isTransitioning = false;
        
        if (enableDebugLog)
        {
            Debug.Log("[SequentialController] 开始序列");
        }

        // 显示第一组
        ShowGroup(0);
        UpdateCounter();

        // 开始检测物体状态
        StartCoroutine(CheckObjectsStatus());
    }

    // 定期检测物体状态
    IEnumerator CheckObjectsStatus()
    {
        while (isRunning)
        {
            yield return new WaitForSeconds(checkInterval);

            // 如果正在切换组，跳过检测
            if (isTransitioning) continue;

            // 检查当前组的物体
            if (currentGroupIndex < objectGroups.Count)
            {
                ObjectGroup group = objectGroups[currentGroupIndex];
                bool groupChanged = false;

                foreach (GameObject obj in group.objects)
                {
                    if (obj != null && !disappearedObjects.Contains(obj))
                    {
                        // 检查物体是否不活跃
                        if (!obj.activeInHierarchy)
                        {
                            OnObjectDisappeared(obj);
                            groupChanged = true;
                        }
                    }
                }

                // 如果当前组有变化，检查是否全部消失
                if (groupChanged && IsCurrentGroupAllDisappeared())
                {
                    isTransitioning = true; // 标记为正在切换
                    StartCoroutine(ShowNextGroup());
                }
            }
        }
    }

    // 当物体消失时调用（可以被外部脚本调用）
    public void OnObjectDisappeared(GameObject obj)
    {
        if (!isRunning) return;
        if (disappearedObjects.Contains(obj)) return; // 避免重复计数

        // 标记为已消失
        disappearedObjects.Add(obj);

        // 增加计数
        disappearedCount++;
        UpdateCounter();

        if (enableDebugLog)
        {
            Debug.Log($"[SequentialController] 物体 {obj.name} 已消失，当前进度：{disappearedCount}/{totalObjectCount}");
        }
    }

    // 检查当前组是否全部消失
    bool IsCurrentGroupAllDisappeared()
    {
        if (currentGroupIndex >= objectGroups.Count) return false;

        ObjectGroup group = objectGroups[currentGroupIndex];
        int activeCount = 0;

        foreach (GameObject obj in group.objects)
        {
            if (obj != null && !disappearedObjects.Contains(obj) && obj.activeInHierarchy)
            {
                activeCount++;
            }
        }

        if (enableDebugLog && activeCount == 0)
        {
            Debug.Log($"[SequentialController] 第 {currentGroupIndex + 1} 组全部消失");
        }

        return activeCount == 0;
    }

    // 显示下一组
    IEnumerator ShowNextGroup()
    {
        // 等待延迟
        if (currentGroupIndex < objectGroups.Count)
        {
            float delay = objectGroups[currentGroupIndex].delayBeforeNextGroup;
            if (enableDebugLog)
            {
                Debug.Log($"[SequentialController] 等待 {delay} 秒后显示下一组");
            }
            yield return new WaitForSeconds(delay);
        }

        currentGroupIndex++;

        // 如果还有下一组，显示它
        if (currentGroupIndex < objectGroups.Count)
        {
            ShowGroup(currentGroupIndex);
            isTransitioning = false; // 切换完成
        }
        // 如果所有组都完成了，显示最终物体
        else
        {
            OnAllObjectsDisappeared();
        }
    }

    // 所有物体都消失后
    void OnAllObjectsDisappeared()
    {
        isRunning = false;

        if (enableDebugLog)
        {
            Debug.Log("[SequentialController] 所有物体已消失，执行最终操作");
        }

        // 显示最终物体
        ShowFinalObjects();
        
        // 隐藏最终物体
        HideFinalObjects();

        // 隐藏计数器（如果设置了）
        if (hideCounterWhenComplete && counterText != null)
        {
            counterText.gameObject.SetActive(false);
        }
        else
        {
            UpdateCounter(); // 显示完成状态
        }
    }

    // 显示指定组
    void ShowGroup(int groupIndex)
    {
        if (groupIndex >= objectGroups.Count)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning($"[SequentialController] 组索引 {groupIndex} 超出范围");
            }
            return;
        }

        ObjectGroup group = objectGroups[groupIndex];
        int showCount = 0;

        foreach (GameObject obj in group.objects)
        {
            if (obj != null)
            {
                // 只显示还没消失的物体
                if (!disappearedObjects.Contains(obj))
                {
                    obj.SetActive(true);
                    showCount++;
                    
                    if (enableDebugLog)
                    {
                        Debug.Log($"[SequentialController] 显示物体: {obj.name}");
                    }
                }
            }
            else
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning($"[SequentialController] 第 {groupIndex + 1} 组中有空物体引用");
                }
            }
        }

        if (enableDebugLog)
        {
            Debug.Log($"[SequentialController] 显示第 {groupIndex + 1} 组，共显示 {showCount} 个物体");
        }
    }

    // 隐藏指定组
    void HideGroup(int groupIndex)
    {
        if (groupIndex >= objectGroups.Count) return;

        ObjectGroup group = objectGroups[groupIndex];
        foreach (GameObject obj in group.objects)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }

        if (enableDebugLog)
        {
            Debug.Log($"[SequentialController] 隐藏第 {groupIndex + 1} 组");
        }
    }

    // 显示最终物体（完成后显示）
    void ShowFinalObjects()
    {
        int showCount = 0;

        // 通过引用显示
        if (finalObjectsToShow != null)
        {
            foreach (GameObject obj in finalObjectsToShow)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    showCount++;
                    
                    if (enableDebugLog)
                    {
                        Debug.Log($"[SequentialController] 显示最终物体: {obj.name}");
                    }
                }
            }
        }

        // 通过名字显示
        if (finalObjectNamesToShow != null)
        {
            foreach (string objName in finalObjectNamesToShow)
            {
                if (string.IsNullOrEmpty(objName)) continue;

                GameObject obj = FindObjectByName(objName);
                if (obj != null)
                {
                    obj.SetActive(true);
                    showCount++;
                    
                    if (enableDebugLog)
                    {
                        Debug.Log($"[SequentialController] 显示最终物体（通过名字）: {objName}");
                    }
                }
            }
        }

        if (enableDebugLog)
        {
            Debug.Log($"[SequentialController] 共显示 {showCount} 个最终物体");
        }
    }

    // 隐藏最终物体（完成后消失）
    void HideFinalObjects()
    {
        int hideCount = 0;

        // 通过引用隐藏
        if (finalObjectsToHide != null)
        {
            foreach (GameObject obj in finalObjectsToHide)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    hideCount++;
                    
                    if (enableDebugLog)
                    {
                        Debug.Log($"[SequentialController] 隐藏最终物体: {obj.name}");
                    }
                }
            }
        }

        // 通过名字隐藏
        if (finalObjectNamesToHide != null)
        {
            foreach (string objName in finalObjectNamesToHide)
            {
                if (string.IsNullOrEmpty(objName)) continue;

                GameObject obj = FindObjectByName(objName);
                if (obj != null)
                {
                    obj.SetActive(false);
                    hideCount++;
                    
                    if (enableDebugLog)
                    {
                        Debug.Log($"[SequentialController] 隐藏最终物体（通过名字）: {objName}");
                    }
                }
            }
        }

        if (enableDebugLog && hideCount > 0)
        {
            Debug.Log($"[SequentialController] 共隐藏 {hideCount} 个最终物体");
        }
    }

    // 隐藏最终显示的物体（初始化时调用）
    void HideFinalShowObjects()
    {
        if (finalObjectsToShow != null)
        {
            foreach (GameObject obj in finalObjectsToShow)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
    }

    // 更新计数器显示
    void UpdateCounter()
    {
        if (counterText != null)
        {
            counterText.text = $"{disappearedCount}/{totalObjectCount}";
        }
    }

    // 通过名字查找物体
    GameObject FindObjectByName(string objectName)
    {
        if (cachedObjects.ContainsKey(objectName))
        {
            return cachedObjects[objectName];
        }

        GameObject foundObject = null;

        if (searchRoot != null)
        {
            Transform[] allTransforms = searchRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (t.gameObject.name == objectName)
                {
                    foundObject = t.gameObject;
                    break;
                }
            }
        }
        else
        {
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.hideFlags == HideFlags.None && obj.scene.isLoaded && obj.name == objectName)
                {
                    foundObject = obj;
                    break;
                }
            }
        }

        if (foundObject != null)
        {
            cachedObjects[objectName] = foundObject;
        }

        return foundObject;
    }

    // 重置
    public void Reset()
    {
        disappearedCount = 0;
        currentGroupIndex = 0;
        isRunning = false;
        isTransitioning = false;
        disappearedObjects.Clear();

        // 显示第一组
        ShowGroup(0);

        // 隐藏其他组
        for (int i = 1; i < objectGroups.Count; i++)
        {
            HideGroup(i);
        }

        // 隐藏最终显示的物体
        HideFinalShowObjects();

        // 显示计数器
        if (counterText != null)
        {
            counterText.gameObject.SetActive(true);
        }

        UpdateCounter();

        if (enableDebugLog)
        {
            Debug.Log("[SequentialController] 已重置");
        }
    }
}
