using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 让可抓取物体在放下后保持在 DontDestroyOnLoad 场景中
/// 需要配合 XRGrabInteractable 使用
/// </summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class PersistentGrabbable : MonoBehaviour
{
    [Header("调试")]
    public bool enableDebugLog = true;
    
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private bool shouldBePersistent = false;
    
    void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        
        // 检查物体是否应该持久化
        if (gameObject.scene.name == "DontDestroyOnLoad")
        {
            shouldBePersistent = true;
            if (enableDebugLog)
            {
                Debug.Log($"[PersistentGrabbable] {gameObject.name} 已在 DontDestroyOnLoad 中，将保持持久化");
            }
        }
    }
    
    void OnEnable()
    {
        if (grabInteractable != null)
        {
            // 监听放下事件
            grabInteractable.selectExited.AddListener(OnDropped);
        }
    }
    
    void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectExited.RemoveListener(OnDropped);
        }
    }
    
    void Start()
    {
        // 如果物体应该持久化，确保它在 DontDestroyOnLoad 中
        if (shouldBePersistent)
        {
            EnsurePersistent();
        }
    }
    
    private void OnDropped(SelectExitEventArgs args)
    {
        if (!shouldBePersistent) return;
        
        if (enableDebugLog)
        {
            Debug.Log($"[PersistentGrabbable] {gameObject.name} 被放下，检查场景归属");
        }
        
        // 延迟一帧，等待 XR 系统完成父级设置
        StartCoroutine(EnsurePersistentNextFrame());
    }
    
    private System.Collections.IEnumerator EnsurePersistentNextFrame()
    {
        yield return null; // 等待一帧
        EnsurePersistent();
    }
    
    private void EnsurePersistent()
    {
        if (gameObject.scene.name != "DontDestroyOnLoad")
        {
            if (enableDebugLog)
            {
                Debug.LogWarning($"[PersistentGrabbable] {gameObject.name} 不在 DontDestroyOnLoad 中！当前场景: {gameObject.scene.name}");
                Debug.Log($"[PersistentGrabbable] 重新标记为 DontDestroyOnLoad");
            }
            
            // 确保父级为 null（在场景根节点）
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            
            // 移回 DontDestroyOnLoad
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            if (enableDebugLog)
            {
                Debug.Log($"[PersistentGrabbable] {gameObject.name} 仍在 DontDestroyOnLoad 中 ✓");
            }
        }
    }
    
    void OnTransformParentChanged()
    {
        if (!shouldBePersistent) return;
        
        if (enableDebugLog)
        {
            string parentName = transform.parent != null ? transform.parent.name : "null";
            Debug.Log($"[PersistentGrabbable] {gameObject.name} 父级改变: {parentName}");
        }
    }
    
    // 公共方法：标记物体应该持久化
    public void MarkAsPersistent()
    {
        shouldBePersistent = true;
        EnsurePersistent();
    }
    
    // 公共方法：取消持久化
    public void UnmarkPersistent()
    {
        shouldBePersistent = false;
    }
}
