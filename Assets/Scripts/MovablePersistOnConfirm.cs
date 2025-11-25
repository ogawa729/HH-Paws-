using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 将一个可移动物体的“移动检测 + 确认跳转并在下一场景变透明”逻辑封装在一起。
/// 使用：把此脚本挂到需要被移动的物体上（或其根节点）。
/// - 在 Inspector 设置 nextSceneName（下一个要加载的场景名），透明度 transparentAlpha。
/// - 确认按钮在被点击时调用 ConfirmAndLoadNext()（可在 GrabInteractable 的事件里绑定）。
/// 行为：当检测到物体被移动（位置有明显变化）后，ConfirmAndLoadNext 会把物体标记为 DontDestroyOnLoad，加载下一个场景，
/// 在新场景加载完成时把该物体的材质切换为支持透明的模式并设置 alpha 为 transparentAlpha。
/// 注意：如果材质使用的 shader 不支持透明，可能需要在材质上使用合适 shader（例如 Standard）或自定义处理。
/// </summary>
public class MovablePersistOnConfirm : MonoBehaviour
{
    [Header("移动检测")]
    [Tooltip("判定为移动的距离阈值（米），当移动距离平方大于阈值^2 即视为已移动）")]
    public float moveThreshold = 0.01f;
    [Tooltip("检测移动的间隔（秒）")]
    public float checkInterval = 0.1f;

    [Header("跳转设置")]
    [Tooltip("确认后要加载的下一个场景名（与 Build Settings 中的名字一致）")]
    public string nextSceneName;
    [Tooltip("在下一个场景中将物体变为多少透明（0 完全透明，1 不透明）")]
    [Range(0f,1f)]
    public float transparentAlpha = 0.35f;
    [Tooltip("若为 true 则不要求必须移动就可以确认（默认 false）")]
    public bool allowConfirmWithoutMove = false;

    [System.Serializable]
    public class SpawnEntry
    {
        public string name;
        public GameObject prefab;
        [Tooltip("若不为空，可指定为该物体下的某个 Transform（建议为 root 的子 Transform）或场景中任意 Transform")]
        public Transform attachPoint;
        [Tooltip("生成后相对于父对象的本地偏移（仅当 spawnAsChild 为 true 时生效）")]
        public Vector3 localOffset = Vector3.zero;
        [Tooltip("true = 作为 attachPoint 的子对象并使用 localOffset；false = 在 world 位置生成（使用 attachPoint.position 或 root.position）")]
        public bool spawnAsChild = true;
        [Tooltip("生成后是否禁用 spawned 的所有 Collider（防止物理冲突）")]
        public bool disableColliders = false;
        [Tooltip("是否为 spawned 物体设置 layer（若 true 使用 layerIndex）")]
        public bool setLayer = false;
        public int layerIndex = 0;
    }
    [Header("下个场景生成配置")]
    [Tooltip("使用 SpawnEntry 列表按项配置要在下一个场景生成的物体（推荐）")]
    public SpawnEntry[] spawnEntries;

    private Vector3 lastPosition;
    private bool hasMoved = false;
    private Coroutine checkCoroutine;

    void Start()
    {
        lastPosition = transform.position;
        checkCoroutine = StartCoroutine(CheckMovedLoop());
    }

    private IEnumerator CheckMovedLoop()
    {
        var sqThreshold = moveThreshold * moveThreshold;
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            var sqDist = (transform.position - lastPosition).sqrMagnitude;
            if (sqDist > sqThreshold)
            {
                hasMoved = true;
                // 更新 lastPosition，继续检测后续移动
                lastPosition = transform.position;
                // 可在此发送事件或做视觉反馈
                Debug.Log("[MovablePersistOnConfirm] detected move.");
            }
            else
            {
                // 仍然更新 lastPosition，防止累积误差
                lastPosition = transform.position;
            }
        }
    }

    /// <summary>
    /// 服务端（确认按钮）调用：如果已移动（或允许未移动确认），则持久化该物体并加载下一个场景。
    /// 在新场景中该物体会变为透明。
    /// </summary>
    public void ConfirmAndLoadNext()
    {
        if (!allowConfirmWithoutMove && !hasMoved)
        {
            Debug.LogWarning("[MovablePersistOnConfirm] object not moved yet. Confirm denied.");
            return;
        }

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("[MovablePersistOnConfirm] nextSceneName is empty. Cannot load.");
            return;
        }

        // 停止移动检测
        if (checkCoroutine != null) StopCoroutine(checkCoroutine);

        // 把物体根节点标记为 DontDestroyOnLoad（以便在新场景中仍然存在）
        GameObject root = gameObject;
        // 若希望整棵树一起保留，可使用 transform.root.gameObject
        root = transform.root.gameObject;
        DontDestroyOnLoad(root);

        // 订阅场景加载事件，等新场景加载完成后把材质透明化
        SceneManager.sceneLoaded += OnSceneLoaded_SetTransparent;

        // 异步加载下一个场景（Single 模式）
        SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// 允许外部直接指定场景名并立即确认
    /// </summary>
    public void ConfirmAndLoadNextScene(string sceneName)
    {
        nextSceneName = sceneName;
        ConfirmAndLoadNext();
    }

    private void OnSceneLoaded_SetTransparent(Scene scene, LoadSceneMode mode)
    {
        // 取消订阅，保证只处理一次
        SceneManager.sceneLoaded -= OnSceneLoaded_SetTransparent;

        // 将挂有脚本的根节点取出（因为我们对 root 做了 DontDestroyOnLoad）
        GameObject root = transform.root.gameObject;

        // 查找所有 Renderer 并把材质实例化后设置为透明模式与指定 alpha
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            // 对每个材质都实例化一份，避免修改 sharedMaterial
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null) continue;
                TryMakeMaterialTransparent(mat);
                Color c = mat.color;
                c.a = transparentAlpha;
                mat.color = c;
            }
            // 重新赋值（materials 访问本身已完成实例化）
            r.materials = mats;
        }

        // 在物体上生成指定预制体
        if (spawnEntries != null && spawnEntries.Length > 0)
        {
            for (int i = 0; i < spawnEntries.Length; i++)
            {
                var e = spawnEntries[i];
                if (e == null || e.prefab == null) continue;

                GameObject spawned = null;
                if (e.spawnAsChild)
                {
                    Transform parent = (e.attachPoint != null) ? e.attachPoint : root.transform;
                    spawned = Instantiate(e.prefab, parent);
                    spawned.transform.localPosition = e.localOffset;
                    spawned.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    Vector3 pos = (e.attachPoint != null) ? e.attachPoint.position : root.transform.position;
                    Quaternion rot = (e.attachPoint != null) ? e.attachPoint.rotation : Quaternion.identity;
                    spawned = Instantiate(e.prefab, pos, rot);
                    // 若希望仍挂载到 root 下但保持世界位置，可调用 SetParent
                    // spawned.transform.SetParent(root.transform, true);
                }

                if (spawned != null)
                {
                    if (e.disableColliders)
                    {
                        var cols = spawned.GetComponentsInChildren<Collider>(true);
                        foreach (var c in cols) c.enabled = false;
                    }
                    if (e.setLayer)
                    {
                        SetLayerRecursively(spawned, e.layerIndex);
                    }
                }
            }
        }

        Debug.Log($"[MovablePersistOnConfirm] applied transparency ({transparentAlpha}) to object in scene {scene.name}");

        // 这里根据需要可以销毁脚本或做其他标记
        // Destroy(this);
    }

    /// <summary>
    /// 尝试把标准 Shader 的材质模式切换到支持透明的 "Fade" 模式。
    /// 如果使用自定义 Shader 可能需要额外处理。
    /// </summary>
    private void TryMakeMaterialTransparent(Material mat)
    {
        if (mat == null) return;
        // 仅对使用 Standard shader 的材质尝试修改 render mode
        if (mat.shader != null && mat.shader.name.Contains("Standard"))
        {
            // 将材质切换为 Fade 模式
            mat.SetFloat("_Mode", 2f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }

    // 便利：外部可以查询是否已经发生过移动
    public bool HasMoved()
    {
        return hasMoved;
    }

    // 递归设置 layer
    private void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }
}
