using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 强制保持物体在 DontDestroyOnLoad 场景中
/// 每帧检查，如果被移出就立即移回
/// </summary>
public class ForcePersistent : MonoBehaviour
{
    [Header("调试")]
    public bool enableDebugLog = true;
    
    private bool hasMarkedPersistent = false;
    
    void Awake()
    {
        MarkAsPersistent();
    }
    
    void Start()
    {
        MarkAsPersistent();
    }
    
    void Update()
    {
        // 每帧检查物体是否还在 DontDestroyOnLoad 场景中
        if (gameObject.scene.name != "DontDestroyOnLoad")
        {
            if (enableDebugLog)
            {
                Debug.LogWarning($"[ForcePersistent] {gameObject.name} 被移出 DontDestroyOnLoad！当前场景: {gameObject.scene.name}");
                Debug.LogWarning($"[ForcePersistent] 调用堆栈:\n{System.Environment.StackTrace}");
            }
            
            // 重新标记为持久化
            MarkAsPersistent();
        }
    }
    
    void MarkAsPersistent()
    {
        if (gameObject.scene.name == "DontDestroyOnLoad")
        {
            if (!hasMarkedPersistent && enableDebugLog)
            {
                Debug.Log($"[ForcePersistent] {gameObject.name} 已在 DontDestroyOnLoad 中");
                hasMarkedPersistent = true;
            }
            return;
        }
        
        DontDestroyOnLoad(gameObject);
        
        if (enableDebugLog)
        {
            Debug.Log($"[ForcePersistent] 标记 {gameObject.name} 为 DontDestroyOnLoad");
        }
        
        hasMarkedPersistent = true;
    }
    
    void OnTransformParentChanged()
    {
        if (enableDebugLog)
        {
            string parentName = transform.parent != null ? transform.parent.name : "null";
            Debug.LogWarning($"[ForcePersistent] {gameObject.name} 的父级改变了！新父级: {parentName}");
            Debug.LogWarning($"[ForcePersistent] 当前场景: {gameObject.scene.name}");
        }
        
        // 父级改变可能导致物体被移出 DontDestroyOnLoad
        // 重新标记
        MarkAsPersistent();
    }
}
