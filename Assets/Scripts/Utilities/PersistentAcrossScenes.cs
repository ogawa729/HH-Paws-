using UnityEngine;

/// <summary>
/// 简单的跨场景持久化组件：
/// - 将当前物体（或根物体）标记为 DontDestroyOnLoad
/// - 可通过名称或显式 key 避免重复保留
/// 使用方法：把该组件挂到你想保留的 GameObject 上，然后播放场景切换。
/// </summary>
public class PersistentAcrossScenes : MonoBehaviour
{
    [Header("Persistence Settings")]
    [Tooltip("If true, call DontDestroyOnLoad on the root GameObject instead of this GameObject.")]
    public bool persistRoot = true;

    [Tooltip("If true and a duplicate with the same key/name exists, destroy the newly-created one. If false, keep the newest and destroy the old one.")]
    public bool destroyNewIfDuplicate = true;

    [Tooltip("Optional explicit key to identify duplicates. If empty, the GameObject name (or root name) is used.")]
    public string uniqueKey = "";

    void Awake()
    {
        // Decide which GameObject we will make persistent
        GameObject target = persistRoot ? transform.root.gameObject : gameObject;

        // Build an identification key
        string key = string.IsNullOrEmpty(uniqueKey) ? target.name : uniqueKey;

        // Find other PersistentAcrossScenes components in the scene (includes DontDestroyOnLoad objects)
        var others = FindObjectsOfType<PersistentAcrossScenes>();
        foreach (var other in others)
        {
            if (other == this) continue;
            GameObject theirTarget = other.persistRoot ? other.transform.root.gameObject : other.gameObject;
            string theirKey = string.IsNullOrEmpty(other.uniqueKey) ? theirTarget.name : other.uniqueKey;

            if (theirKey == key)
            {
                if (destroyNewIfDuplicate)
                {
                    // If there is an existing persistent object with same key, destroy this one (new)
                    if (target == gameObject) Destroy(gameObject);
                    else Destroy(target);
                    return;
                }
                else
                {
                    // Otherwise destroy the existing one and continue to persist this
                    try { Destroy(theirTarget); } catch { }
                    break;
                }
            }
        }

        // Mark persistent
        DontDestroyOnLoad(target);
        Debug.Log($"[PersistentAcrossScenes] 保留: {target.name} (root={persistRoot}, key={key})");
    }
}
