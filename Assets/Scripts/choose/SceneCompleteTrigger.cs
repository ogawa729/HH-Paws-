using UnityEngine;

/// <summary>
/// 场景完成触发器 - 当满足条件时完成当前关卡
/// </summary>
public class SceneCompleteTrigger : MonoBehaviour
{
    [Header("触发方式")]
    public bool triggerOnCollision = true;  // 碰撞触发
    public bool triggerOnClick = false;     // 点击触发
    public string playerTag = "Player";     // 玩家标签

    void OnTriggerEnter(Collider other)
    {
        if (triggerOnCollision && other.CompareTag(playerTag))
        {
            CompleteLevel();
        }
    }

    void OnMouseDown()
    {
        if (triggerOnClick)
        {
            CompleteLevel();
        }
    }

    // 完成关卡（也可以被其他脚本调用）
    public void CompleteLevel()
    {
        Debug.Log("[SceneComplete] 关卡完成触发");
        SceneFlowManager.Instance.CompleteCurrentLevel();
    }
}
