using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 简单父物体位置锁定器 - 只锁定位置，不影响其他功能
/// 支持场景切换时保持锁定状态
/// </summary>
public class SimpleParentLocker : MonoBehaviour
{
    [Header("基本设置")]
    public Button lockButton;                  // 锁定按钮
    public Transform targetObject;             // 要锁定的父物体
    
    [Header("锁定设置")]
    public bool lockPosition = true;           // 锁定位置
    public bool lockRotation = false;          // 锁定旋转
    public bool lockScale = false;             // 锁定缩放
    
    [Header("防重复点击")]
    public float clickCooldown = 0.5f;         // 点击冷却时间（秒）
    public bool autoBindButton = false;        // 是否自动绑定按钮事件
    
    [Header("调试")]
    public bool enableDebugLog = true;         // 启用调试日志

    // 私有变量
    private bool isLocked = false;             // 锁定状态
    private Vector3 lockedPosition;            // 锁定的位置
    private Quaternion lockedRotation;         // 锁定的旋转
    private Vector3 lockedScale;               // 锁定的缩放
    private Rigidbody targetRigidbody;         // 目标物体的刚体
    private bool originalIsKinematic;          // 原始运动学状态
    
    // 防重复点击
    private float lastClickTime = 0f;          // 上次点击时间
    private bool isProcessing = false;         // 是否正在处理
    private bool hasAddedListener = false;     // 是否已添加监听器

    void OnEnable()
    {
        // 监听场景加载事件
        SceneManager.sceneLoaded += OnSceneLoadedHandler;
    }

    void OnDisable()
    {
        // 取消监听场景加载事件
        SceneManager.sceneLoaded -= OnSceneLoadedHandler;
    }

    void Start()
    {
        // 验证设置
        if (lockButton == null)
        {
            Debug.LogError("[SimpleLocker] 锁定按钮未设置！");
            return;
        }

        if (targetObject == null)
        {
            Debug.LogError("[SimpleLocker] 目标物体未设置！");
            return;
        }

        // 只有在设置了自动绑定时才添加事件监听器
        if (autoBindButton && !hasAddedListener)
        {
            lockButton.onClick.AddListener(OnButtonClick);
            hasAddedListener = true;
            if (enableDebugLog) Debug.Log("[SimpleLocker] 自动绑定按钮事件");
        }
        else
        {
            if (enableDebugLog) Debug.Log("[SimpleLocker] 跳过自动绑定，请手动在Inspector中设置按钮事件");
        }

        // 获取目标物体的刚体组件（只获取父物体自身的组件）
        targetRigidbody = targetObject.GetComponent<Rigidbody>();

        // 保存刚体原始状态
        if (targetRigidbody != null)
        {
            originalIsKinematic = targetRigidbody.isKinematic;
            if (enableDebugLog) Debug.Log("[SimpleLocker] 找到刚体组件");
        }
        else
        {
            if (enableDebugLog) Debug.Log("[SimpleLocker] 目标物体没有刚体组件");
        }

        if (enableDebugLog) Debug.Log("[SimpleLocker] 初始化完成");
    }

    // 场景加载回调 - 处理场景切换时的锁定状态
    void OnSceneLoadedHandler(Scene scene, LoadSceneMode mode)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[SimpleLocker] 场景切换: {scene.name}, 当前锁定状态: {(isLocked ? "已锁定" : "未锁定")}");
        }

        // 如果物体处于锁定状态，需要在场景加载后重新应用锁定
        if (isLocked && targetObject != null)
        {
            StartCoroutine(RelockAfterSceneLoad());
        }
    }

    // 场景加载后重新锁定
    IEnumerator RelockAfterSceneLoad()
    {
        // 等待场景完全加载
        yield return new WaitForEndOfFrame();

        if (isLocked && targetObject != null)
        {
            // 重新应用锁定位置
            targetObject.position = lockedPosition;
            
            if (lockRotation)
            {
                targetObject.rotation = lockedRotation;
            }
            
            if (lockScale)
            {
                targetObject.localScale = lockedScale;
            }

            // 重新设置刚体状态
            if (targetRigidbody != null)
            {
                targetRigidbody.velocity = Vector3.zero;
                targetRigidbody.angularVelocity = Vector3.zero;
                targetRigidbody.isKinematic = true;
            }

            if (enableDebugLog)
            {
                Debug.Log($"[SimpleLocker] 场景切换后重新锁定: {targetObject.name} 在位置: {lockedPosition}");
            }
        }
    }

    // 按钮点击处理（带防重复点击）- 这个方法可以在Inspector中手动绑定
    public void OnButtonClick()
    {
        float currentTime = Time.time;
        
        if (enableDebugLog)
        {
            Debug.Log($"[SimpleLocker] 按钮被点击 - 当前时间: {currentTime:F2}, 上次点击: {lastClickTime:F2}, 间隔: {currentTime - lastClickTime:F2}");
        }

        // 检查冷却时间
        if (currentTime - lastClickTime < clickCooldown)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[SimpleLocker] 点击过快，忽略 - 冷却时间: {clickCooldown}秒");
            }
            return;
        }

        // 检查是否正在处理
        if (isProcessing)
        {
            if (enableDebugLog)
            {
                Debug.Log("[SimpleLocker] 正在处理中，忽略点击");
            }
            return;
        }

        lastClickTime = currentTime;
        ToggleLock();
    }

    // 直接切换锁定状态（不经过防重复检查）- 供其他脚本调用
    public void DirectToggleLock()
    {
        if (enableDebugLog)
        {
            Debug.Log("[SimpleLocker] 直接切换锁定状态（跳过防重复检查）");
        }
        ToggleLock();
    }

    // 切换锁定状态
    void ToggleLock()
    {
        if (targetObject == null)
        {
            Debug.LogError("[SimpleLocker] 目标物体为空");
            return;
        }

        if (isProcessing)
        {
            if (enableDebugLog) Debug.Log("[SimpleLocker] 正在处理中，跳过切换");
            return;
        }

        if (enableDebugLog)
        {
            Debug.Log($"[SimpleLocker] 切换锁定状态 - 当前状态: {(isLocked ? "已锁定" : "未锁定")}");
        }

        if (isLocked)
        {
            UnlockObject();
        }
        else
        {
            LockObject();
        }
    }

    // 锁定物体
    public void LockObject()
    {
        if (targetObject == null || isProcessing) return;

        isProcessing = true;
        isLocked = true;

        // 记录当前状态
        lockedPosition = targetObject.position;
        lockedRotation = targetObject.rotation;
        lockedScale = targetObject.localScale;

        if (enableDebugLog)
        {
            Debug.Log($"[SimpleLocker] 开始锁定物体: {targetObject.name} 在位置: {lockedPosition}");
        }

        // 锁定位置（使用运动学刚体，这是最稳定的方法）
        if (targetRigidbody != null)
        {
            // 清除当前速度
            targetRigidbody.velocity = Vector3.zero;
            targetRigidbody.angularVelocity = Vector3.zero;
            
            // 设置为运动学模式，这样物理系统不会影响它的位置
            targetRigidbody.isKinematic = true;
            
            if (enableDebugLog)
            {
                Debug.Log("[SimpleLocker] 刚体设置为运动学模式");
            }
        }

        isProcessing = false;
        
        if (enableDebugLog) Debug.Log("[SimpleLocker] 锁定完成");
    }

    // 解锁物体
    public void UnlockObject()
    {
        if (targetObject == null || isProcessing) return;

        isProcessing = true;
        isLocked = false;

        if (enableDebugLog)
        {
            Debug.Log($"[SimpleLocker] 开始解锁物体: {targetObject.name}");
        }

        // 解锁位置
        if (targetRigidbody != null)
        {
            // 恢复原始运动学状态
            targetRigidbody.isKinematic = originalIsKinematic;
            
            if (enableDebugLog)
            {
                Debug.Log($"[SimpleLocker] 刚体恢复原始状态: isKinematic = {originalIsKinematic}");
            }
        }

        isProcessing = false;
        
        if (enableDebugLog) Debug.Log("[SimpleLocker] 解锁完成");
    }

    void Update()
    {
        // 如果物体被锁定，强制保持锁定的位置/旋转/缩放
        if (isLocked && targetObject != null)
        {
            if (lockPosition)
            {
                targetObject.position = lockedPosition;
            }

            if (lockRotation)
            {
                targetObject.rotation = lockedRotation;
            }

            if (lockScale)
            {
                targetObject.localScale = lockedScale;
            }
        }

        // 测试按键（调试用）
        if (Input.GetKeyDown(KeyCode.L))
        {
            if (enableDebugLog) Debug.Log("[SimpleLocker] 按下L键测试");
            OnButtonClick();
        }
    }

    // 获取锁定状态
    public bool IsLocked()
    {
        return isLocked;
    }

    // 获取处理状态
    public bool IsProcessing()
    {
        return isProcessing;
    }

    // 设置锁定位置（手动指定位置）
    public void SetLockPosition(Vector3 position)
    {
        lockedPosition = position;
        if (isLocked && lockPosition)
        {
            targetObject.position = lockedPosition;
        }

        if (enableDebugLog)
        {
            Debug.Log($"[SimpleLocker] 设置锁定位置为: {position}");
        }
    }

    // 设置锁定旋转（手动指定旋转）
    public void SetLockRotation(Quaternion rotation)
    {
        lockedRotation = rotation;
        if (isLocked && lockRotation)
        {
            targetObject.rotation = lockedRotation;
        }

        if (enableDebugLog)
        {
            Debug.Log($"[SimpleLocker] 设置锁定旋转为: {rotation.eulerAngles}");
        }
    }

    // 获取锁定的位置
    public Vector3 GetLockedPosition()
    {
        return lockedPosition;
    }

    // 获取锁定的旋转
    public Quaternion GetLockedRotation()
    {
        return lockedRotation;
    }

    // 重置到锁定位置
    public void ResetToLockedPosition()
    {
        if (targetObject != null && isLocked)
        {
            if (lockPosition) targetObject.position = lockedPosition;
            if (lockRotation) targetObject.rotation = lockedRotation;
            if (lockScale) targetObject.localScale = lockedScale;
            
            if (enableDebugLog)
            {
                Debug.Log($"[SimpleLocker] 重置物体到锁定状态");
            }
        }
    }

    void OnDestroy()
    {
        // 移除事件监听器（如果添加了）
        if (hasAddedListener && lockButton != null)
        {
            lockButton.onClick.RemoveListener(OnButtonClick);
        }
    }

    // 手动测试方法
    [ContextMenu("测试锁定")]
    public void TestLock()
    {
        if (!isLocked && !isProcessing)
        {
            LockObject();
        }
    }

    [ContextMenu("测试解锁")]
    public void TestUnlock()
    {
        if (isLocked && !isProcessing)
        {
            UnlockObject();
        }
    }

    [ContextMenu("强制重置状态")]
    public void ForceReset()
    {
        isProcessing = false;
        isLocked = false;
        lastClickTime = 0f;
        Debug.Log("[SimpleLocker] 状态已重置");
    }

    [ContextMenu("显示当前状态")]
    public void ShowCurrentStatus()
    {
        Debug.Log($"[SimpleLocker] 锁定状态: {isLocked}");
        Debug.Log($"[SimpleLocker] 处理状态: {isProcessing}");
        Debug.Log($"[SimpleLocker] 当前位置: {targetObject?.position}");
        Debug.Log($"[SimpleLocker] 锁定位置: {lockedPosition}");
        if (targetRigidbody != null)
        {
            Debug.Log($"[SimpleLocker] 刚体状态: isKinematic = {targetRigidbody.isKinematic}");
        }
    }
}
