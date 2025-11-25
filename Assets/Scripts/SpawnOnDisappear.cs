using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 当指定目标物体“消失”后（被设为 inactive / 被销毁 / Renderer 禁用 / Collider 禁用），生成指定的预制体。
/// 使用方法：把脚本挂在任意常驻对象上，在 Inspector 中设置 triggerObject（触发目标）、condition、要生成的 prefabs 和 spawnPoints（可选）。
/// </summary>
public class SpawnOnDisappear : MonoBehaviour
{
    public enum DisappearCondition
    {
        SetInactive,    // GameObject.activeInHierarchy == false
        Destroyed,      // triggerObject == null
        RendererDisabled, // 所有 Renderer.enabled 都为 false
        ColliderDisabled, // 所有 Collider.enabled 都为 false
        Any             // 上面任意一种
    }

    // 在指定父对象下递归查找子物体（支持 inactive）
    private Transform FindChildRecursiveLocal(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == name) return c;
            var found = FindChildRecursiveLocal(c, name);
            if (found != null) return found;
        }
        return null;
    }

    [Header("触发设置")]
    [Tooltip("要监视的目标对象（被认为“消失”后触发生成逻辑）")]
    public GameObject triggerObject;
    [Tooltip("判断目标“消失”的条件")]
    public DisappearCondition condition = DisappearCondition.Any;
    [Tooltip("是否在开始监听前等待 triggerObject 在场景中第一次出现（避免在场景刚加载时因为引用尚未建立而误判为已销毁）。默认 true。")]
    public bool waitUntilTriggerPresent = true;
    [Tooltip("当 waitUntilTriggerPresent 为 true 时，最多等待的秒数；0 表示无限等待直到对象出现。")]
    public float waitForTriggerTimeout = 0f;

    [Header("生成设置")]
    [Tooltip("要生成的预制体列表（按顺序生成）")]
    public GameObject[] prefabsToSpawn;
    [Tooltip("可选：每个预制体对应的生成点（可为空）。如果为空则使用 triggerObject 的位置/旋转作为生成位姿。")]
    public Transform[] spawnPoints;
    [Tooltip("可选：将生成点计算为另一个物体的表面。优先使用 surfaceCollider（更精确），否则使用 surfaceRaycastTransform + raycast 向下命中表面。")]
    public Collider surfaceCollider;
    [Tooltip("如果未指定 surfaceCollider，可指定一个 Transform 并从其上方向下 Raycast 来确定表面位置（单位米）。")]
    public Transform surfaceRaycastTransform;
    [Tooltip("从 surfaceRaycastTransform 或 triggerObject 的上方起始射线的高度（米），用于向下 Raycast 找到表面")]
    public float raycastStartHeight = 2.0f;
    [Tooltip("当在表面命中点上生成时相对于表面法线的偏移（世界单位），避免 Z-fighting 或穿透。")]
    public float surfaceNormalOffset = 0.02f;
    [Tooltip("如果为 true，生成的对象会设为 surfaceCollider.transform（或 surfaceRaycastTransform）为父对象，以便随表面物体移动而移动。")]
    public bool parentSpawnToSurface = true;
    [Tooltip("生成时保持父级世界位置不变（如果为 true 则在设 parent 后不调整世界坐标），通常设为 false 以便 spawned 跟随表面准确贴合。")]
    public bool preserveWorldPositionWhenParenting = false;
    [Tooltip("Raycast 的 LayerMask（当使用 Raycast 查找表面时）")]
    public LayerMask surfaceRaycastMask = ~0;
    [Tooltip("可选：生成后父对象；为空则不设父或使用 spawnPoint 的父级")]
    public Transform parentForSpawn;

    [Header("显示设置")]
    [Tooltip("当 triggerObject 被判定为消失后，激活这些已有的场景/层级对象（SetActive(true)）。可留空。")]
    public GameObject[] objectsToShowOnDisappear;
    [Tooltip("当 triggerObject 被判定为消失后，通过名字激活这些对象（支持在 Inspector 输入名称）。脚本会先在 deskRoot 下查找（若配置），否则在当前活动场景中查找。")]
    public string[] objectNamesToShowOnDisappear;
    [Tooltip("可选：若场景中有一个 deskRoot（菜单桌面）并希望优先在其下查找名字匹配的对象，可在此拖入该 GameObject。留空则直接在当前活动场景中查找。")]
    public GameObject deskRoot;
    [Tooltip("仅触发一次（触发后禁用脚本），否则持续监听并在每次满足条件时生成")]
    public bool spawnOnce = true;
    [Tooltip("在检测到消失后等待的秒数（可以用于延迟生成）")]
    public float delayAfterDisappear = 0f;

    [Header("调试/日志")]
    [Tooltip("启用后会在 Console 打印详细诊断日志")]
    public bool verboseLogs = true;

    private bool hasSpawned = false;
    // runtime: whether we've seen the trigger object at least once since monitoring began
    private bool hasSeenTrigger = false;
    // 如果等待超时但对象仍未出现，则设置这个标志以在后续判断中忽略 null==Destroyed 的判定
    private bool ignoreNullDestroyedAfterTimeout = false;

    void Start()
    {
        // 立即检查一次（防止对象已在 Start 前消失）
        StartCoroutine(MonitorRoutine());
    }

    private IEnumerator MonitorRoutine()
    {
        // 等待 triggerObject 首次出现（可选）以避免在场景加载初期误判
        if (waitUntilTriggerPresent)
        {
            float t = 0f;
            while (!hasSeenTrigger)
            {
                if (triggerObject != null)
                {
                    hasSeenTrigger = true;
                    break;
                }
                    if (waitForTriggerTimeout > 0f)
                    {
                        t += Time.deltaTime;
                        if (t >= waitForTriggerTimeout)
                        {
                            // 超时后仍未见到 triggerObject：停止等待并继续监听（但此时 triggerObject 可能为 null）
                            // 记录为已见到以开始监听，但同时在后续判断中忽略 null==Destroyed 的判定，避免立即触发
                            hasSeenTrigger = true;
                            ignoreNullDestroyedAfterTimeout = true;
                            break;
                        }
                    }
                yield return null;
            }
        }

        // 开始常规监听循环
        while (true)
        {
            if (spawnOnce && hasSpawned) yield break;

            // 动态更新是否已经见到 triggerObject（如果之前未见）
            if (!hasSeenTrigger && triggerObject != null) hasSeenTrigger = true;

            bool disappeared = CheckCondition();
            if (disappeared)
            {
                if (verboseLogs) Debug.Log($"[SpawnOnDisappear] condition met ({condition}) for {GetObjectName(triggerObject)}");
                if (delayAfterDisappear > 0f)
                    yield return new WaitForSeconds(delayAfterDisappear);

                // 先显示配置中要显示的已有对象（如果有）
                ShowConfiguredObjects();

                // 再进行 prefab 生成
                SpawnAll();

                if (spawnOnce)
                {
                    hasSpawned = true;
                    yield break;
                }
                else
                {
                    // 如果不是只触发一次，等待下一次目标恢复并再次消失。
                    // 我们在这里等待目标恢复（或被重建）再继续监听，防止无限快速触发。
                    yield return StartCoroutine(WaitForRestore());
                }
            }

            yield return null;
        }
    }

    // 检测消失条件
    private bool CheckCondition()
    {
        // 如果目标为 null，则视为 Destroyed（但如果我们在等待超时后仍未见到对象，可以选择忽略这种判定）
        if (triggerObject == null)
        {
            if (ignoreNullDestroyedAfterTimeout) return false;
            return condition == DisappearCondition.Destroyed || condition == DisappearCondition.Any;
        }

        // active 状态
        if (condition == DisappearCondition.SetInactive || condition == DisappearCondition.Any)
        {
            if (!triggerObject.activeInHierarchy) return true;
        }

        // Renderer 状态（所有 renderer 都 disabled 则判定为消失）
        if (condition == DisappearCondition.RendererDisabled || condition == DisappearCondition.Any)
        {
            var rends = triggerObject.GetComponentsInChildren<Renderer>(true);
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

        // Collider 状态（所有 collider 都 disabled 则判定为消失）
        if (condition == DisappearCondition.ColliderDisabled || condition == DisappearCondition.Any)
        {
            var cols = triggerObject.GetComponentsInChildren<Collider>(true);
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
            else
            {
                // 如果没有 Collider，不能以 ColliderDisabled 判定为消失（除非 condition == Any and other checks pass)
            }
        }

        return false;
    }

    private IEnumerator WaitForRestore()
    {
        // 等待目标恢复到“存在且 active 且至少有一个 renderer/collider enabled”的状态
        while (true)
        {
            if (triggerObject == null)
            {
                // 如果为 null，则等待短暂时间再继续（或可以尝试通过名字查找重建的实例）
                yield return null;
            }
            else
            {
                if (triggerObject.activeInHierarchy)
                {
                    var rends = triggerObject.GetComponentsInChildren<Renderer>(true);
                    var cols = triggerObject.GetComponentsInChildren<Collider>(true);
                    bool rendererOk = rends.Length == 0 ? true : false;
                    foreach (var r in rends) { if (r != null && r.enabled) { rendererOk = true; break; } }
                    bool colliderOk = cols.Length == 0 ? true : false;
                    foreach (var c in cols) { if (c != null && c.enabled) { colliderOk = true; break; } }

                    if (rendererOk && colliderOk) yield break; // restored
                }
            }
            yield return null;
        }
    }

    private void SpawnAll()
    {
        if (prefabsToSpawn == null || prefabsToSpawn.Length == 0)
        {
            if (verboseLogs) Debug.LogWarning("[SpawnOnDisappear] no prefabs configured to spawn.");
            return;
        }

        for (int i = 0; i < prefabsToSpawn.Length; i++)
        {
            var prefab = prefabsToSpawn[i];
            if (prefab == null) continue;

            // 计算生成位姿（优先：spawnPoints -> surfaceCollider/surfaceRaycast -> triggerObject -> this.transform）
            Vector3 pos;
            Quaternion rot;
            Transform spawnPoint = (spawnPoints != null && i < spawnPoints.Length) ? spawnPoints[i] : null;

            if (spawnPoint != null)
            {
                pos = spawnPoint.position;
                rot = spawnPoint.rotation;
            }
            else
            {
                // 尝试通过表面计算位姿
                bool gotPose = ComputeSurfacePose(out pos, out rot);
                if (!gotPose)
                {
                    // 回退：使用 triggerObject 或此脚本的 transform
                    if (triggerObject != null)
                    {
                        pos = triggerObject.transform.position;
                        rot = triggerObject.transform.rotation;
                    }
                    else
                    {
                        pos = transform.position;
                        rot = transform.rotation;
                    }
                }
            }

            GameObject spawned = null;
            if (parentForSpawn != null)
            {
                spawned = Instantiate(prefab, pos, rot, parentForSpawn);
            }
            else
            {
                spawned = Instantiate(prefab, pos, rot);
            }

            // 如果配置了 parentSpawnToSurface，则把 spawned 设为 surface 的子对象（优先 surfaceCollider.transform）
            if (parentSpawnToSurface)
            {
                Transform parent = null;
                if (surfaceCollider != null) parent = surfaceCollider.transform;
                else if (surfaceRaycastTransform != null) parent = surfaceRaycastTransform;

                if (parent != null)
                {
                    if (preserveWorldPositionWhenParenting)
                    {
                        spawned.transform.SetParent(parent, true);
                    }
                    else
                    {
                        // 为了让 spawned 在 parent 下仍然位于精确的世界坐标，先设置父级，然后修正本地位置
                        spawned.transform.SetParent(parent, false);
                        spawned.transform.position = pos;
                        spawned.transform.rotation = rot;
                    }
                }
            }

            if (verboseLogs) Debug.Log($"[SpawnOnDisappear] Spawned {prefab.name} at {pos}");
        }
    }

    // 激活配置的已有对象（例如场景中已经存在但初始为隐藏的物体）
    private void ShowConfiguredObjects()
    {
        if (objectsToShowOnDisappear == null || objectsToShowOnDisappear.Length == 0) return;
        // 先激活通过引用指定的对象
        foreach (var go in objectsToShowOnDisappear)
        {
            if (go == null) continue;
            try
            {
                if (!go.activeInHierarchy) go.SetActive(true);
                if (verboseLogs) Debug.Log($"[SpawnOnDisappear] Activated existing object: {go.name}");
            }
            catch (System.Exception ex)
            {
                if (verboseLogs) Debug.LogWarning($"[SpawnOnDisappear] Failed to Activate {GetObjectName(go)}: {ex.Message}");
            }
        }

        // 再按名字激活对象（支持在 Inspector 中输入名字）
        if (objectNamesToShowOnDisappear != null && objectNamesToShowOnDisappear.Length > 0)
        {
            foreach (var name in objectNamesToShowOnDisappear)
            {
                if (string.IsNullOrEmpty(name)) continue;

                GameObject found = null;

                // 如果有 deskRoot（持久化桌面），优先在其下查找（支持 inactive 子对象）
                if (deskRoot != null)
                {
                    var t = FindChildRecursiveLocal(deskRoot.transform, name);
                    if (t != null) found = t.gameObject;
                }

                // 若未在 deskRoot 找到，则在当前活动场景的根对象下查找（支持 inactive）
                if (found == null)
                {
                    var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                    foreach (var r in roots)
                    {
                        var t = FindChildRecursiveLocal(r.transform, name);
                        if (t != null) { found = t.gameObject; break; }
                    }
                }

                if (found != null)
                {
                    try
                    {
                        if (!found.activeInHierarchy) found.SetActive(true);
                        if (verboseLogs) Debug.Log($"[SpawnOnDisappear] Activated object by name: {found.name}");
                    }
                    catch (System.Exception ex)
                    {
                        if (verboseLogs) Debug.LogWarning($"[SpawnOnDisappear] Failed to activate by name '{name}': {ex.Message}");
                    }
                }
                else
                {
                    if (verboseLogs) Debug.LogWarning($"[SpawnOnDisappear] Could not find object named '{name}' to activate.");
                }
            }
        }
    }

    // 计算面上位姿：先使用 surfaceCollider 的射线/ClosestPoint 来确定点和法线，若没有则使用 surfaceRaycastTransform + raycast
    private bool ComputeSurfacePose(out Vector3 outPos, out Quaternion outRot)
    {
        outPos = Vector3.zero;
        outRot = Quaternion.identity;

        // 1) 使用 surfaceCollider 优先
        if (surfaceCollider != null)
        {
            // 尝试从 Collider.bounds.center 上方发一条向下的射线以获取精确命中点和法线
            Vector3 above = surfaceCollider.bounds.center + Vector3.up * (Mathf.Max(surfaceCollider.bounds.extents.y, 0.5f) + raycastStartHeight);
            RaycastHit hit;
            if (Physics.Raycast(above, Vector3.down, out hit, raycastStartHeight * 3f, surfaceRaycastMask))
            {
                // 确保命中的是我们指定的 collider（或其子 collider）
                if (hit.collider == surfaceCollider || hit.collider.transform.IsChildOf(surfaceCollider.transform))
                {
                    Vector3 p = hit.point + hit.normal * surfaceNormalOffset;
                    outPos = p;
                    outRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    return true;
                }
            }

            // 如果 Raycast 未命中 collider（例如薄片/特殊形状），使用 ClosestPoint 作为回退
            Vector3 sample = surfaceCollider.bounds.center + Vector3.up * raycastStartHeight;
            Vector3 closest = surfaceCollider.ClosestPoint(sample);
            if (closest != Vector3.zero)
            {
                // 估算法线：从稍上方投射一条短射线到表面以尝试获取法线
                RaycastHit hit2;
                if (Physics.Raycast(closest + Vector3.up * 0.05f, Vector3.down, out hit2, 0.1f, surfaceRaycastMask))
                {
                    outPos = hit2.point + hit2.normal * surfaceNormalOffset;
                    outRot = Quaternion.FromToRotation(Vector3.up, hit2.normal);
                }
                else
                {
                    outPos = closest + Vector3.up * surfaceNormalOffset;
                    outRot = Quaternion.identity;
                }
                return true;
            }
        }

        // 2) 使用 surfaceRaycastTransform 作为起点向下 Raycast
        if (surfaceRaycastTransform != null)
        {
            Vector3 origin = surfaceRaycastTransform.position + Vector3.up * raycastStartHeight;
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, raycastStartHeight * 3f, surfaceRaycastMask))
            {
                Vector3 p = hit.point + hit.normal * surfaceNormalOffset;
                outPos = p;
                outRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                return true;
            }
        }

        // 3) 若 triggerObject 存在，可尝试在其上方向下 Raycast（使用 triggerObject 的位置作为参考）
        if (triggerObject != null)
        {
            Vector3 origin = triggerObject.transform.position + Vector3.up * raycastStartHeight;
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, raycastStartHeight * 3f, surfaceRaycastMask))
            {
                Vector3 p = hit.point + hit.normal * surfaceNormalOffset;
                outPos = p;
                outRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                return true;
            }
        }

        return false;
    }

    private string GetObjectName(GameObject go)
    {
        return go == null ? "<null>" : go.name;
    }
}
