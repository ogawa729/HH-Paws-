using UnityEngine;

/// <summary>
/// 极简交互脚本：
/// - 在外部事件（例如 XR 的 PickUp）调用 OnPickUp() 时播放一次声音；
/// - 在外部事件（例如 XR 的 DropDown）调用 OnDropDown() 时让物体“消失”（默认使用 SetActive(false)），并禁用碰撞器以防止后续交互。
/// 
/// 使用方式：把脚本挂到目标物体上，在 Inspector 中配置 AudioSource/Clip，然后将 XR 事件绑定到 OnPickUp / OnDropDown。
/// </summary>
public class SimplePickupDisappear : MonoBehaviour
{
    [Header("声音（可选）")]
    public AudioSource audioSource;
    public AudioClip pickUpClip;
    public AudioClip dropClip;

    [Header("消失行为")]
    [Tooltip("当放下时是否禁用 GameObject（SetActive(false)）。")]
    public bool deactivateOnDrop = true;
    [Tooltip("当放下时是否销毁 GameObject（优先级低于 deactivateOnDrop）。")]
    public bool destroyOnDrop = false;

    [Tooltip("放下时是否先禁用所有碰撞器以避免后续交互/引擎报错。建议保留为 true。")]
    public bool disableCollidersOnDrop = true;

    [Header("放下特效 (可选)")]
    [Tooltip("放下时生成的特效预制体 (可选)")]
    public GameObject dropEffectPrefab;
    [Tooltip("如果指定，特效将产生在该 Transform 的位置/朝向；否则使用被放下物体的位置/朝向")] 
    public Transform dropEffectSpawnPoint;
    [Tooltip("若为 true，则尝试把生成的特效设为放下物体的子对象（仅当物体不会被禁用或销毁时生效）")]
    public bool parentEffectToObjectIfRemaining = false;
    [Tooltip("生成后若要自动销毁特效，设置秒数；<=0 表示不自动销毁")] 
    public float dropEffectAutoDestroyAfter = 5f;

    [Header("放下生成物 (可选)")]
    [Tooltip("放下时显示的现有物体（会调用 SetActive(true)）；可以填写多个")] 
    public GameObject[] showObjectsOnDrop;
    [Tooltip("显示延迟（秒），0 表示立即显示")] 
    public float showDelay = 0f;

    // Called by external XR/interaction system when the object is picked up
    public void OnPickUp()
    {
        if (audioSource != null && pickUpClip != null)
        {
            audioSource.PlayOneShot(pickUpClip);
        }
    }

    // Called by external XR/interaction system when the object is dropped
    public void OnDropDown()
    {
        Debug.Log($"SimplePickupDisappear: OnDropDown called on {gameObject.name}. deactivateOnDrop={deactivateOnDrop}, destroyOnDrop={destroyOnDrop}, disableCollidersOnDrop={disableCollidersOnDrop}");

        if (audioSource != null && dropClip != null)
        {
            audioSource.PlayOneShot(dropClip);
        }

        // 生成放下特效（如果配置了）
        if (dropEffectPrefab != null)
        {
            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            if (dropEffectSpawnPoint != null)
            {
                pos = dropEffectSpawnPoint.position;
                rot = dropEffectSpawnPoint.rotation;
            }

            GameObject ef = null;
            // 如果希望把特效设为放在物体下（并且物体不会被禁用或销毁），则 parent
            bool willBeRemoved = destroyOnDrop || deactivateOnDrop;
            if (parentEffectToObjectIfRemaining && !willBeRemoved)
            {
                ef = Instantiate(dropEffectPrefab, pos, rot, transform);
            }
            else
            {
                ef = Instantiate(dropEffectPrefab, pos, rot);
            }

            if (ef != null && dropEffectAutoDestroyAfter > 0f)
            {
                var ps = ef.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    var dur = ps.main.duration;
                    float destroyAfter = Mathf.Max(dropEffectAutoDestroyAfter, dur);
                    Destroy(ef, destroyAfter);
                }
                else
                {
                    Destroy(ef, dropEffectAutoDestroyAfter);
                }
            }
        }

        // 如果配置了要显示的现有物体，则在放下时显示（支持延迟）
        if (showObjectsOnDrop != null && showObjectsOnDrop.Length > 0)
        {
            if (showDelay > 0f)
            {
                StartCoroutine(ShowOnDropCoroutine(showDelay));
            }
            else
            {
                ShowOnDropNow();
            }
        }

        if (disableCollidersOnDrop)
        {
            var cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (c == null) continue;
                c.enabled = false;
            }
            var cols2 = GetComponentsInChildren<Collider2D>(true);
            foreach (var c2 in cols2)
            {
                if (c2 == null) continue;
                c2.enabled = false;
            }
        }

        if (deactivateOnDrop)
        {
            gameObject.SetActive(false);
        }
        else if (destroyOnDrop)
        {
            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning($"SimplePickupDisappear: neither deactivateOnDrop nor destroyOnDrop is true for {gameObject.name}; object will remain active.");
        }
    }

    // Alias methods: some XR frameworks use different event names. These forward to OnDropDown.
    public void OnDrop()
    {
        OnDropDown();
    }

    public void OnReleased()
    {
        OnDropDown();
    }

    private void ShowOnDropNow()
    {
        if (showObjectsOnDrop == null) return;
        foreach (var obj in showObjectsOnDrop)
        {
            if (obj == null)
            {
                Debug.LogWarning("SimplePickupDisappear: showObjectsOnDrop contains a null entry.");
                continue;
            }
            try
            {
                obj.SetActive(true);
            }
            catch { }
        }
    }

    private System.Collections.IEnumerator ShowOnDropCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowOnDropNow();
    }
}
