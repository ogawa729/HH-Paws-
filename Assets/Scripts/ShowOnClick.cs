using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 简单的“点击显示”脚本。
/// 用法：把该脚本挂到可点击的物体上（例如按钮 enter），
/// 在对应的交互组件的 On Click / On Hover Begin 中绑定 Show() 方法。
/// 将要显现的物体拖入 targets，或填写 prefabToSpawn 来实例化预制体。
/// 支持延时显示。
/// </summary>
public class ShowOnClick : MonoBehaviour
{
    [Tooltip("要显示的现有物体（会 SetActive(true)）")]
    public GameObject[] targets;

    [Tooltip("如果设置了 prefabToSpawn，会在触发时实例化该预制体（可选）")]
    public GameObject prefabToSpawn;
    [Tooltip("实例化时的父对象（留空则不设置父对象）")]
    public Transform spawnParent;
    [Tooltip("实例化时是否使用父对象的本地坐标（否则使用世界坐标，位置为脚本挂载物的位置）")]
    public bool useParentLocalPosition = false;

    [Tooltip("触发后延迟显示（秒）")]
    public float delay = 0f;

    [Header("拾取/放下事件")]
    [Tooltip("用于播放拾取音效的 AudioSource（可留空）")]
    public AudioSource audioSource;
    [Tooltip("拾取时播放的音效")]
    public AudioClip pickUpClip;
    [Tooltip("在 Drop Down 时要显现的物体列表（会 SetActive(true)）")]
    public GameObject[] showOnDropTargets;

    [Header("持有时显示（On Held Update）")]
    [Tooltip("在 OnHeldUpdate 被调用时要显现的物体（会 SetActive(true)）")]
    public GameObject[] showOnHeldTargets;
    [Tooltip("是否只在第一次 OnHeldUpdate 时显示一次（防止重复触发）")]
    public bool showOnHeldOnce = true;
    private bool hasShownOnHeld = false;

    [Header("Timeline")]
    [Tooltip("要监听的 PlayableDirector（Timeline）")]
    public PlayableDirector director;
    [Tooltip("是否在 Start/启用时自动订阅 director.stopped 事件")]
    public bool subscribeDirectorOnStart = false;
    [Tooltip("Timeline 停止时要显现的物体（会 SetActive(true)）")]
    public GameObject[] showOnTimelineEnd;
    [Tooltip("是否只在第一次 Timeline 停止时触发一次（防止重复）")]
    public bool showOnTimelineOnce = true;
    private bool hasShownTimeline = false;

    void OnEnable()
    {
        if (director != null && subscribeDirectorOnStart)
            director.stopped += OnDirectorStopped;
    }

    void OnDisable()
    {
        if (director != null)
            director.stopped -= OnDirectorStopped;
    }

    // 手动开始监听 Timeline 停止事件（可在 Inspector 里绑定或通过代码调用）
    public void StartWatchingTimeline()
    {
        if (director != null)
            director.stopped += OnDirectorStopped;
    }

    // 停止监听 Timeline
    public void StopWatchingTimeline()
    {
        if (director != null)
            director.stopped -= OnDirectorStopped;
    }

    private void OnDirectorStopped(PlayableDirector d)
    {
        if (director != null && d != director) return;
        if (showOnTimelineEnd == null) return;
        if (showOnTimelineOnce && hasShownTimeline) return;
        foreach (var go in showOnTimelineEnd)
            if (go != null) go.SetActive(true);
        hasShownTimeline = true;
    }

    // 无参公有方法，便于在 Inspector 里直接绑定事件
    public void Show()
    {
        if (delay <= 0f)
        {
            ShowNow();
            return;
        }
        StartCoroutine(ShowCoroutine());
    }

    private IEnumerator ShowCoroutine()
    {
        yield return new WaitForSeconds(delay);
        ShowNow();
    }

    private void ShowNow()
    {
        if (targets != null)
        {
            foreach (var go in targets)
            {
                if (go == null) continue;
                go.SetActive(true);
            }
        }

        if (prefabToSpawn != null)
        {
            Vector3 pos = transform.position;
            Quaternion rot = Quaternion.identity;
            if (spawnParent != null)
            {
                if (useParentLocalPosition) pos = spawnParent.localPosition;
                else pos = spawnParent.position;
            }
            Instantiate(prefabToSpawn, pos, rot, spawnParent);
        }
    }

    // 在 GrabInteractable 的 On Pick Up 事件中调用：播放音效
    public void OnPickUp()
    {
        if (audioSource != null && pickUpClip != null)
            audioSource.PlayOneShot(pickUpClip);
    }

    // 在 GrabInteractable 的 On Drop Down 事件中调用：显示指定物体
    public void OnDropDown()
    {
        if (showOnDropTargets == null) return;
        foreach (var go in showOnDropTargets)
            if (go != null) go.SetActive(true);
        // 重置持有时的显示标志，方便下次再次触发
        hasShownOnHeld = false;
    }

    // 在 GrabInteractable 的 On Held Update 事件中调用：显示指定物体
    public void OnHeldUpdate()
    {
        if (showOnHeldTargets == null) return;
        if (showOnHeldOnce && hasShownOnHeld) return;
        foreach (var go in showOnHeldTargets)
            if (go != null) go.SetActive(true);
        hasShownOnHeld = true;
    }

    // 可选：向 Inspector 暴露一个带参数的方法，方便只显示列表中某个索引
    public void ShowIndex(int index)
    {
        if (targets == null || index < 0 || index >= targets.Length) return;
        if (delay <= 0f) targets[index].SetActive(true);
        else StartCoroutine(ShowIndexCoroutine(index));
    }

    private IEnumerator ShowIndexCoroutine(int index)
    {
        yield return new WaitForSeconds(delay);
        if (targets != null && index >= 0 && index < targets.Length && targets[index] != null)
            targets[index].SetActive(true);
    }
}
