using System.Collections;
using UnityEngine;

/// <summary>
/// 监听一个目标对象的“消失”状态（被设为 inactive / 被销毁 / renderer disabled / collider disabled / 任一），
/// 当满足条件时播放音效并生成特效（可选）。
/// 用法：挂在常驻管理对象上或任意 GameObject，配置 triggerObject, sound, effectPrefab 等。
/// </summary>
public class PlaySoundAndEffectOnDisappear : MonoBehaviour
{
    public enum DisappearCondition
    {
        SetInactive,
        Destroyed,
        RendererDisabled,
        ColliderDisabled,
        Any
    }

    [Header("Trigger")]
    [Tooltip("要监视的目标对象（被认为“消失”后触发）。可以同时监视多个对象。若只设置单个旧字段 triggerObject（兼容旧项目），会自动转换为数组。")]
    public GameObject[] triggerObjects;
    [Tooltip("(兼容旧项目) 旧的单个目标字段，若 triggerObjects 为空会自动使用此字段。")]
    public GameObject triggerObject;
    [Tooltip("判定为消失的条件")]
    public DisappearCondition condition = DisappearCondition.Any;
    [Tooltip("是否在开始监听前等待 triggerObject 首次出现，避免场景刚加载时误判（默认 true）。")]
    public bool waitUntilTriggerPresent = true;
    [Tooltip("等待超时（秒），0 表示无限等待直到对象出现）")]
    public float waitForTriggerTimeout = 0f;

    [Header("Behavior")]
    [Tooltip("只触发一次（触发后禁用脚本）")]
    public bool spawnOnce = true;
    [Tooltip("触发后等待的秒数（可用于延迟效果/声音）")]
    public float delayAfterDisappear = 0f;

    [Header("Audio")]
    [Tooltip("按下时播放的音频片段（优先使用此 clip）")]
    public AudioClip soundClip;
    [Tooltip("用于播放音效的 AudioSource；若为空脚本将尝试自动获取或添加一个")]
    public AudioSource audioSource;
    [Tooltip("使用 PlayOneShot 播放 clip（true）或使用 audioSource.clip + Play（false）")]
    public bool usePlayOneShot = true;

    [Header("Effect")]
    [Tooltip("生成的特效预制体（可为带 ParticleSystem 的 prefab）")]
    public GameObject effectPrefab;
    [Tooltip("生成点（如果为空则使用 triggerObject 的位置，triggerObject 也可能为 null）")]
    public Transform effectSpawnPoint;
    [Tooltip("生成后是否把特效设为 spawnPoint 的子对象（便于随父物体移动）")]
    public bool parentEffectToSpawnPoint = false;
    [Tooltip("如果生成的是 ParticleSystem 并且该系统没有自我销毁，脚本将在此秒数后销毁特效（<=0 表示不自动销毁）")]
    public float effectAutoDestroyAfter = 5f;

    [Header("Debug")]
    public bool verboseLogs = true;

    // runtime - per-target state
    private bool[] hasSpawned;
    private bool[] hasSeenTrigger;
    private bool[] ignoreNullDestroyedAfterTimeout;
    private bool arraysInitialized = false;

    void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }
    }

    void OnEnable()
    {
        StartCoroutine(MonitorRoutine());
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator MonitorRoutine()
    {
        EnsureArraysInitialized();

        // wait until at least one trigger appears if requested
        if (waitUntilTriggerPresent)
        {
            float t = 0f;
            bool anySeen = false;
            while (!anySeen)
            {
                for (int i = 0; i < triggerObjects.Length; i++)
                {
                    var go = triggerObjects[i];
                    if (go != null && go.activeInHierarchy)
                    {
                        hasSeenTrigger[i] = true;
                        anySeen = true;
                        break;
                    }
                }
                if (anySeen) break;
                if (waitForTriggerTimeout > 0f)
                {
                    t += Time.deltaTime;
                    if (t >= waitForTriggerTimeout)
                    {
                        // mark all as seen but ignore null-as-destroyed for those that are null
                        for (int i = 0; i < triggerObjects.Length; i++)
                        {
                            hasSeenTrigger[i] = true;
                            if (triggerObjects[i] == null) ignoreNullDestroyedAfterTimeout[i] = true;
                        }
                        if (verboseLogs) Debug.Log("PlaySoundAndEffectOnDisappear: wait timeout, will start listening but ignore null-as-destroyed for null entries.");
                        break;
                    }
                }
                yield return null;
            }
        }

        while (true)
        {
            bool allDone = true;
            for (int i = 0; i < triggerObjects.Length; i++)
            {
                if (spawnOnce && hasSpawned[i]) continue;
                allDone = false;

                if (!hasSeenTrigger[i] && triggerObjects[i] != null) hasSeenTrigger[i] = true;

                if (CheckCondition(i))
                {
                    if (verboseLogs) Debug.Log($"PlaySoundAndEffectOnDisappear: condition met for {GetObjectName(triggerObjects[i])}");
                    if (delayAfterDisappear > 0f) yield return new WaitForSeconds(delayAfterDisappear);
                    DoEffectAndSoundFor(i);
                    hasSpawned[i] = true;
                    if (!spawnOnce)
                    {
                        // wait until restored before listening again for this target
                        yield return StartCoroutine(WaitForRestore(i));
                    }
                }
            }

            if (spawnOnce && allDone) yield break;

            yield return null;
        }
    }

    private void EnsureArraysInitialized()
    {
        // backward compatibility: if user only filled legacy triggerObject, convert it
        if ((triggerObjects == null || triggerObjects.Length == 0) && triggerObject != null)
        {
            triggerObjects = new GameObject[] { triggerObject };
        }

        if (triggerObjects == null) triggerObjects = new GameObject[0];

        int n = triggerObjects.Length;
        if (!arraysInitialized || hasSpawned == null || hasSpawned.Length != n)
        {
            hasSpawned = new bool[n];
            hasSeenTrigger = new bool[n];
            ignoreNullDestroyedAfterTimeout = new bool[n];
            arraysInitialized = true;
        }
    }

    private bool CheckCondition(int index)
    {
        if (triggerObjects == null || index < 0 || index >= triggerObjects.Length) return false;
        var go = triggerObjects[index];

        // null handling
        if (go == null)
        {
            if (ignoreNullDestroyedAfterTimeout != null && index < ignoreNullDestroyedAfterTimeout.Length && ignoreNullDestroyedAfterTimeout[index]) return false;
            return condition == DisappearCondition.Destroyed || condition == DisappearCondition.Any;
        }

        if (condition == DisappearCondition.SetInactive || condition == DisappearCondition.Any)
        {
            if (!go.activeInHierarchy) return true;
        }

        if (condition == DisappearCondition.RendererDisabled || condition == DisappearCondition.Any)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends != null && rends.Length > 0)
            {
                bool anyEnabled = false;
                foreach (var r in rends)
                {
                    if (r == null) continue;
                    if (r.enabled) { anyEnabled = true; break; }
                }
                if (!anyEnabled) return true;
            }
        }

        if (condition == DisappearCondition.ColliderDisabled || condition == DisappearCondition.Any)
        {
            var cols = go.GetComponentsInChildren<Collider>(true);
            if (cols != null && cols.Length > 0)
            {
                bool anyEnabled = false;
                foreach (var c in cols)
                {
                    if (c == null) continue;
                    if (c.enabled) { anyEnabled = true; break; }
                }
                if (!anyEnabled) return true;
            }
        }

        return false;
    }

    private IEnumerator WaitForRestore(int index)
    {
        while (true)
        {
            if (triggerObjects == null || index < 0 || index >= triggerObjects.Length) { yield return null; continue; }
            var go = triggerObjects[index];
            if (go == null) { yield return null; continue; }
            if (go.activeInHierarchy)
            {
                var rends = go.GetComponentsInChildren<Renderer>(true);
                var cols = go.GetComponentsInChildren<Collider>(true);
                bool rendererOk = rends.Length == 0 ? true : false;
                foreach (var r in rends) { if (r != null && r.enabled) { rendererOk = true; break; } }
                bool colliderOk = cols.Length == 0 ? true : false;
                foreach (var c in cols) { if (c != null && c.enabled) { colliderOk = true; break; } }
                if (rendererOk && colliderOk) yield break;
            }
            yield return null;
        }
    }

    private void DoEffectAndSoundFor(int index)
    {
        GameObject go = (triggerObjects != null && index >= 0 && index < triggerObjects.Length) ? triggerObjects[index] : null;

        // sound
        if (soundClip != null)
        {
            // If the configured audioSource is missing or belongs to a different scene (likely a scene-local AudioSource
            // that will be unloaded), create a temporary AudioSource on THIS GameObject (which is expected to be persistent)
            // and play the clip there. This ensures the sound keeps playing even across scene unloads.
            bool usedTemp = false;
            AudioSource playSource = audioSource;
            try
            {
                if (playSource == null || playSource.gameObject.scene != gameObject.scene)
                {
                    playSource = gameObject.AddComponent<AudioSource>();
                    playSource.playOnAwake = false;
                    usedTemp = true;
                }

                if (usePlayOneShot)
                {
                    playSource.PlayOneShot(soundClip);
                    if (usedTemp)
                    {
                        // schedule temp AudioSource removal after clip length
                        Destroy(playSource, soundClip.length + 0.25f);
                    }
                }
                else
                {
                    playSource.clip = soundClip;
                    playSource.Play();
                    if (usedTemp)
                    {
                        Destroy(playSource, soundClip.length + 0.25f);
                    }
                }
            }
            catch { }
        }
        else if (audioSource != null && audioSource.clip != null)
        {
            try { audioSource.Play(); } catch { }
        }

        // effect
        if (effectPrefab != null)
        {
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            Transform parent = null;
            if (effectSpawnPoint != null)
            {
                pos = effectSpawnPoint.position;
                rot = effectSpawnPoint.rotation;
                parent = parentEffectToSpawnPoint ? effectSpawnPoint : null;
            }
            else if (go != null)
            {
                pos = go.transform.position;
                rot = go.transform.rotation;
            }
            else
            {
                pos = transform.position;
                rot = transform.rotation;
            }

            GameObject ef = null;
            if (parent != null)
                ef = Instantiate(effectPrefab, pos, rot, parent);
            else
                ef = Instantiate(effectPrefab, pos, rot);

            if (ef != null && effectAutoDestroyAfter > 0f)
            {
                // try to detect particle duration
                var ps = ef.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    var dur = ps.main.duration;
                    float destroyAfter = Mathf.Max(effectAutoDestroyAfter, dur);
                    Destroy(ef, destroyAfter);
                }
                else
                {
                    Destroy(ef, effectAutoDestroyAfter);
                }
            }
        }
    }

    private string GetObjectName(GameObject go) => go == null ? "<null>" : go.name;
}
