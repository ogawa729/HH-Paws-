using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 调试用：监控物体的生命周期，记录何时被销毁或场景切换
/// </summary>
public class DestroyLogger : MonoBehaviour
{
    [Header("调试设置")]
    public string objectName = "Unknown";
    public bool logEveryFrame = false; // 是否每帧都记录（会产生大量日志）
    
    private int instanceID;
    
    void Awake()
    {
        if (string.IsNullOrEmpty(objectName))
        {
            objectName = gameObject.name;
        }
        instanceID = gameObject.GetInstanceID();
        Debug.Log($"[DestroyLogger] ========== {objectName} (ID:{instanceID}) - Awake ==========");
        Debug.Log($"[DestroyLogger] 场景: {gameObject.scene.name}");
        Debug.Log($"[DestroyLogger] 位置: {transform.position}");
    }
    
    void Start()
    {
        Debug.Log($"[DestroyLogger] {objectName} (ID:{instanceID}) - Start 被调用");
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    
    void OnEnable()
    {
        Debug.Log($"[DestroyLogger] {objectName} (ID:{instanceID}) - OnEnable 被调用");
    }
    
    void OnDisable()
    {
        Debug.LogWarning($"[DestroyLogger] {objectName} (ID:{instanceID}) - OnDisable 被调用！");
        Debug.LogWarning($"[DestroyLogger] 调用堆栈:\n{System.Environment.StackTrace}");
    }
    
    void OnDestroy()
    {
        Debug.LogError($"[DestroyLogger] ========== {objectName} (ID:{instanceID}) - OnDestroy 被调用！==========");
        Debug.LogError($"[DestroyLogger] 场景: {gameObject.scene.name}");
        Debug.LogError($"[DestroyLogger] 完整调用堆栈:\n{System.Environment.StackTrace}");
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[DestroyLogger] {objectName} (ID:{instanceID}) - 场景加载: {scene.name}");
        Debug.Log($"[DestroyLogger] 当前场景: {gameObject.scene.name}");
        Debug.Log($"[DestroyLogger] 当前位置: {transform.position}");
    }
    
    void OnSceneUnloaded(Scene scene)
    {
        Debug.LogWarning($"[DestroyLogger] {objectName} (ID:{instanceID}) - 场景卸载: {scene.name}");
        Debug.LogWarning($"[DestroyLogger] 当前场景: {gameObject.scene.name}");
    }
    
    void OnTransformParentChanged()
    {
        string parentName = transform.parent != null ? transform.parent.name : "null";
        Debug.LogWarning($"[DestroyLogger] {objectName} (ID:{instanceID}) - 父级改变！");
        Debug.LogWarning($"[DestroyLogger] 新父级: {parentName}");
        Debug.LogWarning($"[DestroyLogger] 当前场景: {gameObject.scene.name}");
    }
    
    void Update()
    {
        if (logEveryFrame)
        {
            Debug.Log($"[DestroyLogger] {objectName} - Frame {Time.frameCount}, 场景: {gameObject.scene.name}");
        }
        else
        {
            // 每秒检查一次物体状态
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[DestroyLogger] {objectName} (ID:{instanceID}) - 存活中");
                Debug.Log($"[DestroyLogger]   场景: {gameObject.scene.name}");
                Debug.Log($"[DestroyLogger]   位置: {transform.position}");
                Debug.Log($"[DestroyLogger]   父级: {(transform.parent != null ? transform.parent.name : "null")}");
            }
        }
    }
}
