using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 监听 PlayableDirector 的 stopped 事件，时间轴停止后显示指定物体。
/// 使用：把脚本挂到任意物体，Inspector 填入要监听的 PlayableDirector 和要显示的 Targets。
/// 支持延时显示和只触发一次（oneShot）。
/// </summary>
public class ShowOnTimelineEnd : MonoBehaviour
{
    [Tooltip("要监听的 PlayableDirector（Timeline）")]
    public PlayableDirector director;

    [Tooltip("时间轴停止时要 SetActive(true) 的物体列表")]
    public GameObject[] targets;

    [Tooltip("停止后延迟（秒），0 表示立即显示）")]
    public float delay = 0f;

    [Tooltip("是否在启用脚本时自动订阅 director.stopped（否则可手动调用 StartWatching()）")]
    public bool subscribeOnStart = true;

    [Tooltip("是否只在第一次停止时触发一次")]
    public bool oneShot = true;

    private bool hasShown = false;

    [Header("位置来源与定位选项")]
    [Tooltip("用于获取时间轴结束时位置的 Transform（通常是被时间轴控制的物体）")]
    public Transform positionSource;
    [Tooltip("是否将 targets 移动到 positionSource 的位置（在显示前）")]
    public bool moveTargetsToSourcePosition = false;
    [Tooltip("是否把 targets 挂到 positionSource 下成为子对象（如果为 true 则使用 localOffset，否则使用 world 偏移）")]
    public bool parentTargetsToSource = false;
    [Tooltip("每个 target 的偏移（local 或 world，取决于 parentTargetsToSource）。长度可小于 targets，缺失项默认为 Vector3.zero。")]
    public Vector3[] targetPositionOffsets;

    [Header("父级透明化（可选）")]
    [Tooltip("若设置，时间轴结束时会对该父对象做透明化（可选择仅对父对象自身的 Renderer 生效，不影响子物体）")]
    public GameObject fadeParent;
    [Tooltip("透明目标 alpha 值（0 完全透明，1 不透明）")]
    [Range(0f,1f)]
    public float fadeParentAlpha = 0.35f;
    [Tooltip("若为 true，则只对父对象自身的 Renderer 生效，不会影响子物体的 Renderer；若为 false，则对子对象一并处理（GetComponentsInChildren）")]
    public bool fadeOnlyParentSelf = true;

    void OnEnable()
    {
        if (subscribeOnStart && director != null)
            director.stopped += OnDirectorStopped;
    }

    void OnDisable()
    {
        if (director != null)
            director.stopped -= OnDirectorStopped;
    }

    /// <summary>
    /// 手动开始监听（可在运行时通过代码或事件调用）
    /// </summary>
    public void StartWatching()
    {
        if (director == null) return;
        director.stopped += OnDirectorStopped;

        // 如果 director 当前不在播放且已经到结尾，则立刻触发一次
        if (director.state != PlayState.Playing && director.time >= director.duration)
        {
            OnDirectorStopped(director);
        }
    }

    /// <summary>
    /// 手动停止监听
    /// </summary>
    public void StopWatching()
    {
        if (director == null) return;
        director.stopped -= OnDirectorStopped;
    }

    private void OnDirectorStopped(PlayableDirector d)
    {
        if (director != null && d != director) return;
        if (oneShot && hasShown) return;

        if (delay <= 0f)
            ShowTargets();
        else
            StartCoroutine(ShowAfterDelay());

        hasShown = true;
    }

    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        ShowTargets();
    }

    private void ShowTargets()
    {
        if (targets == null) return;
        Vector3 sourcePos = Vector3.zero;
        bool haveSource = (positionSource != null);
        if (haveSource)
            sourcePos = positionSource.position;

        for (int i = 0; i < targets.Length; i++)
        {
            var go = targets[i];
            if (go == null) continue;

            // 计算偏移
            Vector3 offset = Vector3.zero;
            if (targetPositionOffsets != null && i < targetPositionOffsets.Length)
                offset = targetPositionOffsets[i];

            // 如果需要移动到 source 位置
            if (moveTargetsToSourcePosition && haveSource)
            {
                if (parentTargetsToSource)
                {
                    go.transform.SetParent(positionSource, false);
                    go.transform.localPosition = offset;
                }
                else
                {
                    go.transform.SetParent(null, true); // 保持世界位置模式
                    go.transform.position = sourcePos + offset;
                }
            }

            go.SetActive(true);
        }

        // 如果配置了 fadeParent，则仅对父对象做透明化（可选仅对父对象自身生效）
        if (fadeParent != null)
        {
            FadeGameObject(fadeParent, fadeParentAlpha, fadeOnlyParentSelf);
        }
    }

    /// <summary>
    /// 隐藏目标并重置 oneShot 标志，方便重复监听
    /// </summary>
    public void HideTargets()
    {
        if (targets == null) return;
        foreach (var go in targets)
            if (go != null) go.SetActive(false);
        hasShown = false;
    }

    // 对指定 GameObject 做透明处理（可选择是否包含子物体）
    private void FadeGameObject(GameObject obj, float alpha, bool onlyThis)
    {
        if (obj == null) return;
        Renderer[] renderers = onlyThis ? obj.GetComponents<Renderer>() : obj.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mats = r.materials; // 实例化材质
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null) continue;
                TryMakeMaterialTransparent(mat);
                Color c = mat.color;
                c.a = alpha;
                mat.color = c;
             }
             r.materials = mats;
         }
     }
 
    private void TryMakeMaterialTransparent(Material mat)
    {
        if (mat == null) return;
        if (mat.shader != null && mat.shader.name.Contains("Standard"))
        {
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
 }
