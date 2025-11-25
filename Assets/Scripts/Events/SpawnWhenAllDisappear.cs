using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 当指定的一组目标全部“消失”后（根据条件），生成若干物体。
/// 支持的消失判定：SetInactive / Destroyed / RendererDisabled / ColliderDisabled / Any。
/// Inspector 可配置：要监视的对象数组、是否等待首次出现、超时、生成的 prefab 列表、是否在每个目标位置生成、在父物体下生成、延迟、只触发一次等。
/// 用法：把脚本挂到任意 GameObject（推荐常驻 manager），配置字段后运行。
/// </summary>
public class SpawnWhenAllDisappear : MonoBehaviour
{
    public enum DisappearCondition
    {
        SetInactive,
        Destroyed,
        RendererDisabled,
        ColliderDisabled,
        Any
    }

    [Header("监视目标")]
    [Tooltip("要监视的一组目标；当这些目标全部满足消失条件时触发生成")]
    public GameObject[] watchObjects;
    [Tooltip("是否改为使用名称列表来识别要监视的目标（在某些场景切换场景中比较有用，会按活动场景内的对象名查找）")]
    public bool useWatchNames = false;
    [Tooltip("当 useWatchNames 为 true 时，按此列表中的名称在活动场景中查找对象作为监视目标（完全匹配名字，支持 inactive 对象）")]
    public string[] watchNames;
    [Tooltip("判定为消失的条件")]
    public DisappearCondition condition = DisappearCondition.Any;
    [Tooltip("是否在开始监听前等待所有目标至少出现一次以避免场景刚加载时误判")] 
    public bool waitUntilAllPresent = true;
    [Tooltip("等待出现的超时时间（秒）；0 表示无限等待直到对象出现")] 
    public float waitForPresenceTimeout = 0f;

    [Header("生成设置")]
    [Tooltip("要生成的 prefab 列表（可以为多个）")]
    public GameObject[] prefabsToSpawn;
        [Tooltip("是否在每个被监视对象的位置生成（若 true 则按配置在每个目标位置生成）")]
        public bool spawnAtEachTargetPosition = false;
    [Tooltip("生成时的父对象（若为空使用本脚本挂载对象作为父对象或直接生成到根）")]
    public Transform spawnParent;
    [Tooltip("生成后若要自动销毁生成物，设置秒数；<=0 表示不自动销毁")] 
    public float spawnAutoDestroyAfter = 0f;
    [Tooltip("当所有目标消失后执行生成的延迟（秒）")]
    public float delayAfterAllDisappear = 0f;
    [Tooltip("只触发一次（第一次全部消失时生成），若为 false 则当目标全部恢复后再次消失会重复触发")]
    public bool spawnOnce = true;

    [Header("调试")]
    public bool verboseLogs = false;

    [Header("按名称激活（可选）")]
    [Tooltip("如果启用，脚本将在生成时按名称在当前活动场景中查找对象并激活它们（支持 inactive 对象）。")]
    public bool activateByNameInScene = false;
    [Tooltip("要在场景中激活的对象名称列表；匹配名称完全相同，会激活找到的所有对象。")]
    public string[] spawnNames;
    [Tooltip("是否在所有有效场景中搜索名称（包括 DontDestroyOnLoad 的持久化对象）；若为 false 则只在当前活动场景查找")] 
    public bool searchAllScenesForNames = true;
    [Tooltip("当激活已有对象时是否将其移动到 spawn 指定的位置/旋转；默认 false（只调用 SetActive(true) 不改变位置）")]
    public bool moveActivatedObjects = false;

    [Header("按名称隐藏（可选）")]
    [Tooltip("如果启用，脚本将在触发时按名称在当前活动场景中查找对象并使其消失（支持 inactive 对象）。")]
    public bool deactivateByNameInScene = false;
    [Tooltip("要在场景中隐藏/销毁的对象名称列表；匹配名称完全相同，会影响找到的所有对象。")]
    public string[] hideNames;
    public enum HideMethod { SetInactive, DisableRenderers, DisableColliders, Destroy }
    [Tooltip("隐藏方法：SetInactive会将对象 SetActive(false)，Destroy会销毁对象，其它会禁用对应组件。")]
    public HideMethod hideMethod = HideMethod.SetInactive;

    [Header("运行时查询")]
    [Tooltip("是否在运行时记录已生成的实例，以便通过名称检索（使用 prefab 的 name 作为键）")]
    public bool registerSpawnedByName = true;
    [Tooltip("尝试优先在当前场景中按 prefab 名称查找并激活已有对象；若找到则不实例化 prefab。")]
    public bool preferActivateExisting = true;

    // runtime state
    private bool[] hasSeen;
    private bool[] ignoreNullAfterTimeout;
    private bool arraysInitialized = false;
    private bool hasSpawned = false;
    // registry of spawned instances by name (prefab name without (Clone))
    private Dictionary<string, List<GameObject>> spawnedByName = new Dictionary<string, List<GameObject>>();

    void OnEnable()
    {
        EnsureArrays();
        StartCoroutine(MonitorRoutine());
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    private void EnsureArrays()
    {
        if (watchObjects == null) watchObjects = new GameObject[0];
        // if using name list, ensure watchObjects has same length so indexes map
        if (useWatchNames && watchNames != null)
        {
            if (watchObjects.Length != watchNames.Length)
            {
                watchObjects = new GameObject[watchNames.Length];
            }
        }
        int n = watchObjects.Length;
        if (!arraysInitialized || hasSeen == null || hasSeen.Length != n)
        {
            hasSeen = new bool[n];
            ignoreNullAfterTimeout = new bool[n];
            arraysInitialized = true;
        }
    }

    // Try to resolve watchObjects from watchNames by searching active scene (including inactive objects).
    private void ResolveWatchObjectsFromNames()
    {
        if (!useWatchNames || watchNames == null) return;
        var activeScene = SceneManager.GetActiveScene();
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < watchNames.Length; i++)
        {
            if (string.IsNullOrEmpty(watchNames[i])) { watchObjects[i] = null; continue; }
            // if already assigned and valid, keep it
            var existing = (i < watchObjects.Length) ? watchObjects[i] : null;
            if (existing != null) continue;

            GameObject found = null;
            foreach (var go in all)
            {
                if (go == null) continue;
                // skip assets (prefabs in project view) - only scene objects have a valid scene
                if (!go.scene.IsValid()) continue;
                // if not searching across scenes, restrict to active scene only
                if (!searchAllScenesForNames && go.scene != activeScene) continue;
                if (go.name == watchNames[i]) { found = go; break; }
            }
            if (i < watchObjects.Length) watchObjects[i] = found;
        }
    }

    private IEnumerator MonitorRoutine()
    {
        EnsureArrays();
        // if using names for watch targets, try resolving them now
        ResolveWatchObjectsFromNames();

        // 等待所有目标至少出现一次（可超时）
        if (waitUntilAllPresent && watchObjects.Length > 0)
        {
            float t = 0f;
            bool allSeen = false;
            while (!allSeen)
            {
                allSeen = true;
                for (int i = 0; i < watchObjects.Length; i++)
                {
                    if (watchObjects[i] != null && watchObjects[i].activeInHierarchy)
                    {
                        hasSeen[i] = true;
                        continue;
                    }
                    if (!hasSeen[i]) allSeen = false;
                }

                if (allSeen) break;

                if (waitForPresenceTimeout > 0f)
                {
                    t += Time.deltaTime;
                    if (t >= waitForPresenceTimeout)
                    {
                        // 标记为已见，同时对为 null 的条目标记忽略 null=destroyed
                        for (int i = 0; i < watchObjects.Length; i++)
                        {
                            hasSeen[i] = true;
                            if (watchObjects[i] == null) ignoreNullAfterTimeout[i] = true;
                        }
                        if (verboseLogs) Debug.Log("SpawnWhenAllDisappear: wait-for-presence timeout, start listening but ignore null-after-timeout entries.");
                        break;
                    }
                }

                yield return null;
            }
        }

        while (true)
        {
            if (spawnOnce && hasSpawned) yield break;

            // if using names, try resolving any missing references again (scene might have just loaded)
            if (useWatchNames)
            {
                for (int ri = 0; ri < watchObjects.Length; ri++)
                {
                    if (watchObjects[ri] == null)
                    {
                        ResolveWatchObjectsFromNames();
                        break;
                    }
                }
            }

            if (AreAllDisappeared())
            {
                if (verboseLogs) Debug.Log("SpawnWhenAllDisappear: all targets disappeared, spawning...");

                if (delayAfterAllDisappear > 0f) yield return new WaitForSeconds(delayAfterAllDisappear);

                DoSpawn();
                hasSpawned = true;

                if (spawnOnce) yield break;

                // wait until any target is restored before continuing
                yield return StartCoroutine(WaitForAnyRestore());
            }

            yield return null;
        }
    }

    private bool AreAllDisappeared()
    {
        if (watchObjects == null || watchObjects.Length == 0) return false;
        for (int i = 0; i < watchObjects.Length; i++)
        {
            if (!hasSeen[i])
            {
                // if we haven't seen it yet, skip (we may be in timeout mode where hasSeen was forced true)
                continue;
            }

            var go = watchObjects[i];
            // null handling
            if (go == null)
            {
                if (ignoreNullAfterTimeout != null && i < ignoreNullAfterTimeout.Length && ignoreNullAfterTimeout[i])
                {
                    // we explicitly ignore null-as-destroyed for this item
                    continue;
                }
                // otherwise consider null as destroyed (disappeared)
                if (condition == DisappearCondition.Destroyed || condition == DisappearCondition.Any)
                    continue;
                // if user didn't select Destroyed/Any, then null is ambiguous—still treat as disappeared
                continue;
            }

            if ((condition == DisappearCondition.SetInactive || condition == DisappearCondition.Any) && !go.activeInHierarchy)
                continue;

            if ((condition == DisappearCondition.RendererDisabled || condition == DisappearCondition.Any))
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
                    if (!anyEnabled) continue;
                }
            }

            if ((condition == DisappearCondition.ColliderDisabled || condition == DisappearCondition.Any))
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
                    if (!anyEnabled) continue;
                }
            }

            // 如果走到这里，说明该物体尚未被判定为消失
            return false;
        }
        return true;
    }

    private IEnumerator WaitForAnyRestore()
    {
        while (true)
        {
            for (int i = 0; i < watchObjects.Length; i++)
            {
                var go = watchObjects[i];
                if (go == null) continue;
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
            }
            yield return null;
        }
    }

    private void DoSpawn()
    {
        if (prefabsToSpawn == null || prefabsToSpawn.Length == 0)
        {
            if (!activateByNameInScene)
            {
                if (verboseLogs) Debug.LogWarning("SpawnWhenAllDisappear: prefabsToSpawn is empty and activateByNameInScene disabled, nothing to spawn.");
                return;
            }
        }

        // choose parent
        Transform parent = spawnParent != null ? spawnParent : null;

        if (spawnAtEachTargetPosition && watchObjects != null && watchObjects.Length > 0)
        {
            // Case A: spawn per-target positions
            for (int i = 0; i < watchObjects.Length; i++)
            {
                Vector3 pos = transform.position;
                Quaternion rot = transform.rotation;
                var go = watchObjects[i];
                if (go != null)
                {
                    pos = go.transform.position;
                    rot = go.transform.rotation;
                }

                // choose prefab: if same length as watchObjects, map by index. else if single prefab, reuse it. else spawn all prefabs at this pos.
                if (prefabsToSpawn.Length == watchObjects.Length)
                {
                    SpawnPrefabAt(prefabsToSpawn[i], pos, rot, parent);
                }
                else if (prefabsToSpawn.Length == 1)
                {
                    SpawnPrefabAt(prefabsToSpawn[0], pos, rot, parent);
                }
                else
                {
                    for (int p = 0; p < prefabsToSpawn.Length; p++)
                        SpawnPrefabAt(prefabsToSpawn[p], pos, rot, parent);
                }
            }
        }
        else
        {
            // Case B: spawn all prefabs at spawnParent (or at this.transform if parent null)
            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            if (parent != null)
            {
                pos = parent.position;
                rot = parent.rotation;
            }

            foreach (var prefab in prefabsToSpawn)
            {
                SpawnPrefabAt(prefab, pos, rot, parent);
            }
        }

        // 按名称在场景中激活（如果启用）
        if (activateByNameInScene && spawnNames != null && spawnNames.Length > 0)
        {
            foreach (var name in spawnNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                // 如果 spawnAtEachTargetPosition，为每个监视目标的位置尝试激活（按场景中对象位置不一定要移动）
                if (spawnAtEachTargetPosition && watchObjects != null && watchObjects.Length > 0)
                {
                    for (int i = 0; i < watchObjects.Length; i++)
                    {
                        Vector3 pos = transform.position;
                        Quaternion rot = transform.rotation;
                        var go = watchObjects[i];
                        if (go != null)
                        {
                            pos = go.transform.position;
                            rot = go.transform.rotation;
                        }
                        ActivateExistingByName(name, pos, rot, spawnParent);
                    }
                }
                else
                {
                    // 在 spawnParent 或当前脚本位置作用
                    Vector3 pos = transform.position;
                    Quaternion rot = transform.rotation;
                    if (spawnParent != null)
                    {
                        pos = spawnParent.position;
                        rot = spawnParent.rotation;
                    }
                    ActivateExistingByName(name, pos, rot, spawnParent);
                }
            }
        }

        // 按名称在场景中隐藏（如果启用）
        if (deactivateByNameInScene && hideNames != null && hideNames.Length > 0)
        {
            foreach (var name in hideNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                // deactivate at each target position? hiding ignores position; just deactivate matching scene objects
                DeactivateExistingByName(name, hideMethod);
            }
        }
    }

    private void SpawnPrefabAt(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent)
    {
        if (prefab == null)
        {
            if (verboseLogs) Debug.LogWarning("SpawnWhenAllDisappear: one of prefabsToSpawn is null.");
            return;
        }

    GameObject inst = null;
        // If configured, try to find existing scene objects with same name and activate them instead of instantiating
        if (preferActivateExisting)
        {
            bool anyActivated = false;
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            var activeScene = SceneManager.GetActiveScene();
            foreach (var go in all)
            {
                if (go == null) continue;
                // only scene objects
                if (!go.scene.IsValid()) continue;
                if (!searchAllScenesForNames && go.scene != activeScene) continue;
                if (go.name != prefab.name) continue;
                try
                {
                    if (moveActivatedObjects)
                    {
                        go.transform.SetPositionAndRotation(pos, rot);
                        if (parent != null)
                        {
                            // keep world position, but parent for organization if desired
                            go.transform.SetParent(parent, true);
                        }
                    }
                    go.SetActive(true);
                    anyActivated = true;
                    if (registerSpawnedByName)
                    {
                        string key = prefab.name;
                        if (key.EndsWith("(Clone)")) key = key.Replace("(Clone)", "").Trim();
                        if (!spawnedByName.TryGetValue(key, out var list))
                        {
                            list = new List<GameObject>();
                            spawnedByName[key] = list;
                        }
                        if (!list.Contains(go)) list.Add(go);
                    }
                }
                catch { }
            }
            if (anyActivated)
            {
                if (verboseLogs) Debug.Log($"SpawnWhenAllDisappear: activated existing scene objects named '{prefab.name}' instead of instantiating (searchAllScenes={searchAllScenesForNames}, moveActivatedObjects={moveActivatedObjects}).");
                return;
            }
        }
        try
        {
            // 先在根实例化，设置世界坐标/朝向，避免继承父级缩放/旋转
            inst = Instantiate(prefab);
            inst.transform.SetPositionAndRotation(pos, rot);

            if (parent != null)
                inst.transform.SetParent(parent, true); // 保持世界位置

            // 防止继承父级缩放导致形变
            inst.transform.localScale = Vector3.one;

            // register spawned instance by prefab name (strip possible (Clone) suffix)
            if (registerSpawnedByName)
            {
                string key = prefab.name;
                if (key.EndsWith("(Clone)")) key = key.Replace("(Clone)", "").Trim();
                if (!spawnedByName.TryGetValue(key, out var list))
                {
                    list = new List<GameObject>();
                    spawnedByName[key] = list;
                }
                list.Add(inst);
            }

            if (spawnAutoDestroyAfter > 0f)
            {
                var ps = inst.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    float dur = ps.main.duration;
                    float destroyAfter = Mathf.Max(spawnAutoDestroyAfter, dur);
                    Destroy(inst, destroyAfter);
                }
                else
                {
                    Destroy(inst, spawnAutoDestroyAfter);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"SpawnWhenAllDisappear: failed to spawn prefab {prefab}: {ex}");
            if (inst != null) Destroy(inst);
        }
    }

    // 激活当前活动场景中与 name 匹配的对象（包括 inactive）。
    // 如果找到对象，会调用 SetActive(true)，并（可选）设置位置/旋转（世界）并将其注册到 spawnedByName。
    private void ActivateExistingByName(string name, Vector3 pos, Quaternion rot, Transform parent)
    {
        if (string.IsNullOrEmpty(name)) return;
        // 查找所有 GameObject（包含 inactive）并筛选属于当前活动场景且 name 相等的
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        var activeScene = SceneManager.GetActiveScene();
        bool anyFound = false;
        foreach (var go in all)
        {
            if (go == null) continue;
            // skip assets (prefabs in project view) - only scene objects have a valid scene
            if (!go.scene.IsValid()) continue;
            if (!searchAllScenesForNames && go.scene != activeScene) continue;
            if (go.name != name) continue;

            anyFound = true;
            try
            {
                if (moveActivatedObjects)
                {
                    go.transform.SetPositionAndRotation(pos, rot);
                    if (parent != null)
                        go.transform.SetParent(parent, true);
                }
                go.SetActive(true);

                if (registerSpawnedByName)
                {
                    if (!spawnedByName.TryGetValue(name, out var list))
                    {
                        list = new List<GameObject>();
                        spawnedByName[name] = list;
                    }
                    if (!list.Contains(go)) list.Add(go);
                }
            }
            catch (System.Exception ex)
            {
                if (verboseLogs) Debug.LogWarning($"SpawnWhenAllDisappear: failed to activate object '{name}': {ex}");
            }
        }

        if (verboseLogs && !anyFound) Debug.LogWarning($"SpawnWhenAllDisappear: no scene object named '{name}' found to activate (searchAllScenes={searchAllScenesForNames}).");
    }

    // 按名称在当前活动场景中使对象消失或禁用组件
    private void DeactivateExistingByName(string name, HideMethod method)
    {
        if (string.IsNullOrEmpty(name)) return;
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        var activeScene = SceneManager.GetActiveScene();
        bool anyFound = false;
        foreach (var go in all)
        {
            if (go == null) continue;
            if (!go.scene.IsValid()) continue;
            if (!searchAllScenesForNames && go.scene != activeScene) continue;
            if (go.name != name) continue;

            anyFound = true;
            try
            {
                switch (method)
                {
                    case HideMethod.SetInactive:
                        go.SetActive(false);
                        break;
                    case HideMethod.DisableRenderers:
                        var rends = go.GetComponentsInChildren<Renderer>(true);
                        foreach (var r in rends) if (r != null) r.enabled = false;
                        break;
                    case HideMethod.DisableColliders:
                        var cols = go.GetComponentsInChildren<Collider>(true);
                        foreach (var c in cols) if (c != null) c.enabled = false;
                        var cols2 = go.GetComponentsInChildren<Collider2D>(true);
                        foreach (var c2 in cols2) if (c2 != null) c2.enabled = false;
                        break;
                    case HideMethod.Destroy:
                        // remove from registry if present
                        if (registerSpawnedByName)
                        {
                            if (spawnedByName.TryGetValue(name, out var list))
                                list.RemoveAll(x => x == null || x == go);
                        }
                        Destroy(go);
                        break;
                }
            }
            catch (System.Exception ex)
            {
                if (verboseLogs) Debug.LogWarning($"SpawnWhenAllDisappear: failed to deactivate object '{name}': {ex}");
            }
        }

        if (verboseLogs && !anyFound) Debug.LogWarning($"SpawnWhenAllDisappear: no scene object named '{name}' found to deactivate (searchAllScenes={searchAllScenesForNames}).");
    }

    /// <summary>
    /// 返回第一个匹配名称的已生成实例（按 prefab 的 name 匹配）。若没有返回 null。
    /// </summary>
    public GameObject GetFirstSpawnedByName(string prefabName)
    {
        if (!registerSpawnedByName || string.IsNullOrEmpty(prefabName)) return null;
        if (!spawnedByName.TryGetValue(prefabName, out var list) || list == null) return null;
        // prune nulls
        list.RemoveAll(x => x == null);
        return list.Count > 0 ? list[0] : null;
    }

    /// <summary>
    /// 返回匹配名称的所有已生成实例（非 null）。如果没有返回空数组。
    /// </summary>
    public GameObject[] GetAllSpawnedByName(string prefabName)
    {
        if (!registerSpawnedByName || string.IsNullOrEmpty(prefabName)) return new GameObject[0];
        if (!spawnedByName.TryGetValue(prefabName, out var list) || list == null) return new GameObject[0];
        list.RemoveAll(x => x == null);
        return list.ToArray();
    }

    /// <summary>
    /// 尝试查找第一个已生成实例。
    /// </summary>
    public bool TryGetFirstSpawnedByName(string prefabName, out GameObject found)
    {
        found = GetFirstSpawnedByName(prefabName);
        return found != null;
    }
}
