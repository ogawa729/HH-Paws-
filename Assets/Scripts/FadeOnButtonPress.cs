using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 点击按钮调用 OnPress() 后将指定渲染器的材质逐渐变为指定透明度。
/// 把脚本挂在任意对象上（或按钮上），在按钮的 UnityEvent 中绑定 FadeOnButtonPress -> OnPress().
/// 注意：访问 renderer.materials 会为材质创建实例，避免修改 sharedMaterial。
/// </summary>
public class FadeOnButtonPress : MonoBehaviour
{
    [Tooltip("要变透明的 Renderer 列表（可以是多个，包含子物体的 Renderer）")]
    public Renderer[] targets;

    [Tooltip("目标 alpha 值（0 完全透明，1 不透明）")]
    [Range(0f,1f)]
    public float targetAlpha = 0.35f;

    [Tooltip("渐变时长（秒）")]
    public float duration = 0.5f;

    [Header("声音（可选）")]
    public AudioSource audioSource;
    public AudioClip pressClip;

    // 新增：在按下时要移除的组件（在 Inspector 中直接拖入组件实例，例如某物体上的脚本）
    [Header("按下时移除的组件（可选）")]
    [Tooltip("在按下按钮时销毁这些组件实例（只移除组件，不销毁 GameObject）。")]
    public GameObject[] colliderTargets;

    // 新增：按下时禁用的 Behaviour 组件（可在 Inspector 中直接将组件拖入，例如某物体上的脚本）
    [Header("按下时禁用的组件（可选）")]
    [Tooltip("在按下按钮时禁用这些 Behaviour（仅适用于继承自 Behaviour 的组件，例如自定义脚本、Collider 不一定可用）。")]
    public Behaviour[] componentsToDisable;

    [Header("仅禁用根对象上的 BoxCollider（可选）")]
    [Tooltip("如果勾选，则按下按钮时只会在 colliderTargets 指定的根 GameObject 上禁用 BoxCollider（不会遍历子物体）。")]
    public bool disableOnlyBoxColliderOnRoot = false;

    // 缓存为每个 renderer 分配的实例化材质和原始颜色
    private List<Material[]> instancedMaterials = new List<Material[]>();
    private List<Color[]> originalColors = new List<Color[]>();
    private Coroutine fadeCoroutine;

    void Start()
    {
        // 如果 targets 在 Inspector 中设置，则为它们创建材质实例并缓存原始颜色
        PrepareMaterials();
    }

    // 可在运行时手动准备（如果 targets 在运行时才设置）
    public void PrepareMaterials()
    {
        instancedMaterials.Clear();
        originalColors.Clear();
        if (targets == null) return;
        foreach (var r in targets)
        {
            if (r == null) { instancedMaterials.Add(null); originalColors.Add(null); continue; }
            var mats = r.materials; // 访问 materials 会实例化材质
            instancedMaterials.Add(mats);
            Color[] cols = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++) cols[i] = (mats[i] != null) ? mats[i].color : Color.white;
            originalColors.Add(cols);
        }
    }

    // 在按钮事件或其它地方调用以触发变透明
    public void OnPress()
    {
        if (audioSource != null && pressClip != null)
            audioSource.PlayOneShot(pressClip);

        // 先禁用指定 Behaviour 组件（若有）
        DisableComponentsFromTargets();

        // 然后处理 Collider（若有）
        RemoveCollidersFromTargets();

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        // 确保已有实例化材质
        if (instancedMaterials.Count == 0) PrepareMaterials();
        fadeCoroutine = StartCoroutine(FadeToTarget());
    }

    // 新增：按下时禁用在 Inspector 中指定的 Behaviour 组件（安全，不销毁）
    private void DisableComponentsFromTargets()
    {
        if (componentsToDisable == null || componentsToDisable.Length == 0) return;
        foreach (var comp in componentsToDisable)
        {
            if (comp == null) continue;
            try
            {
                comp.enabled = false;
                Debug.Log($"FadeOnButtonPress: 已禁用组件 {comp.GetType().Name} on {comp.gameObject.name}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"FadeOnButtonPress: 无法禁用组件 {comp.GetType().Name} on {comp.gameObject.name}: {ex.Message}");
            }
        }
    }

    // 新增：移除指定 GameObject 上挂载的 Collider（包括 3D 和 2D Collider）
    // 注意：不要立即 Destroy Collider，因为外部系统可能在同一帧或随后仍然持有引用并访问它们，
    // 会导致 MissingReferenceException。改为禁用 collider 并把对象放到 Ignore Raycast 层作为安全做法。
    private void RemoveCollidersFromTargets()
    {
        if (colliderTargets == null || colliderTargets.Length == 0) return;
        foreach (var go in colliderTargets)
        {
            if (go == null) continue;

            if (disableOnlyBoxColliderOnRoot)
            {
                // 只禁用根对象上的 BoxCollider（3D）
                var box = go.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.enabled = false;
                    Debug.Log($"FadeOnButtonPress: 已禁用根对象上的 BoxCollider on {go.name} (path={GetGameObjectPath(go)})");
                }
                else
                {
                    Debug.Log($"FadeOnButtonPress: 根对象 {go.name} 上未找到 BoxCollider (path={GetGameObjectPath(go)})");
                }
            }
            else
            {
                // 禁用目标及其子物体上的所有 3D Collider（不销毁）
                var colliders = go.GetComponentsInChildren<Collider>(true);
                foreach (var c in colliders)
                {
                    if (c == null) continue;
                    c.enabled = false; // 立即禁用，外部射线检测会立刻失效
                    Debug.Log($"FadeOnButtonPress: 已禁用 Collider {c.GetType().Name} on {c.gameObject.name} (path={GetGameObjectPath(c.gameObject)})");
                }

                // 禁用目标及其子物体上的所有 2D Collider（不销毁）
                var colliders2D = go.GetComponentsInChildren<Collider2D>(true);
                foreach (var c2 in colliders2D)
                {
                    if (c2 == null) continue;
                    c2.enabled = false;
                    Debug.Log($"FadeOnButtonPress: 已禁用 Collider2D {c2.GetType().Name} on {c2.gameObject.name} (path={GetGameObjectPath(c2.gameObject)})");
                }
            }

            // 双重保险：将对象层改为 Ignore Raycast（如果存在）以避免被射线再次命中
            int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreLayer >= 0 && ignoreLayer < 32)
            {
                go.layer = ignoreLayer;
                Debug.Log($"FadeOnButtonPress: 已将 {go.name} 设置为 Ignore Raycast 层");
            }
        }
    }

    // helper: 获取 GameObject 的层级路径，便于日志分析
    private string GetGameObjectPath(GameObject go)
    {
        if (go == null) return "<null>";
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }

    private IEnumerator FadeToTarget()
    {
        float elapsed = 0f;
        // 记录起始颜色数组（当前颜色）
        List<Color[]> startColors = new List<Color[]>();
        foreach (var mats in instancedMaterials)
        {
            if (mats == null) { startColors.Add(null); continue; }
            Color[] arr = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++) arr[i] = mats[i] != null ? mats[i].color : Color.white;
            startColors.Add(arr);
        }

        while (elapsed < duration)
        {
            float t = (duration <= 0f) ? 1f : Mathf.Clamp01(elapsed / duration);
            for (int ri = 0; ri < instancedMaterials.Count; ri++)
            {
                var mats = instancedMaterials[ri];
                var starts = startColors[ri];
                if (mats == null || starts == null) continue;
                for (int mi = 0; mi < mats.Length; mi++)
                {
                    var mat = mats[mi];
                    if (mat == null) continue;
                    Color sc = starts[mi];
                    float newA = Mathf.Lerp(sc.a, targetAlpha, t);
                    Color nc = new Color(sc.r, sc.g, sc.b, newA);
                    mat.color = nc;
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 最终确保目标 alpha
        for (int ri = 0; ri < instancedMaterials.Count; ri++)
        {
            var mats = instancedMaterials[ri];
            if (mats == null) continue;
            for (int mi = 0; mi < mats.Length; mi++)
            {
                var mat = mats[mi];
                if (mat == null) continue;
                Color c = mat.color; c.a = targetAlpha; mat.color = c;
            }
        }

        fadeCoroutine = null;
        yield break;
    }
}
