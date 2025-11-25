using UnityEngine;

/// <summary>
/// 角色成长系统 - 根据游玩次数改变角色外观
/// </summary>
public class CharacterGrowthSystem : MonoBehaviour
{
    [System.Serializable]
    public class GrowthStage
    {
        public int requiredPlayCount;      // 需要的游玩次数
        public GameObject characterModel;  // 角色模型
        public string stageName;           // 阶段名称
    }

    [Header("成长阶段")]
    public GrowthStage[] growthStages;     // 成长阶段数组

    [Header("设置")]
    public bool updateOnStart = true;      // 启动时更新

    void Start()
    {
        if (updateOnStart)
        {
            UpdateCharacterStage();
        }
    }

    // 更新角色阶段
    public void UpdateCharacterStage()
    {
        int playCount = GameDataManager.Instance.GetTotalPlayCount();
        
        // 找到当前应该显示的阶段
        GrowthStage currentStage = null;
        foreach (GrowthStage stage in growthStages)
        {
            if (playCount >= stage.requiredPlayCount)
            {
                currentStage = stage;
            }
            else
            {
                break;
            }
        }

        // 隐藏所有模型
        foreach (GrowthStage stage in growthStages)
        {
            if (stage.characterModel != null)
            {
                stage.characterModel.SetActive(false);
            }
        }

        // 显示当前阶段的模型
        if (currentStage != null && currentStage.characterModel != null)
        {
            currentStage.characterModel.SetActive(true);
            Debug.Log($"[CharacterGrowth] 角色成长到: {currentStage.stageName} (游玩次数: {playCount})");
        }
    }

    // 获取当前阶段名称
    public string GetCurrentStageName()
    {
        int playCount = GameDataManager.Instance.GetTotalPlayCount();
        
        foreach (GrowthStage stage in growthStages)
        {
            if (playCount >= stage.requiredPlayCount)
            {
                return stage.stageName;
            }
        }
        
        return "初始";
    }
}
