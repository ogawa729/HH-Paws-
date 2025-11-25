using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏数据管理器 - 使用 PlayerPrefs 保存数据
/// </summary>
public class GameDataManager : MonoBehaviour
{
    private static GameDataManager instance;
    public static GameDataManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("GameDataManager");
                instance = go.AddComponent<GameDataManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [System.Serializable]
    public class LevelData
    {
        public string levelName;           // 关卡名称
        public int playCount;              // 游玩次数
        public float totalPlayTime;        // 总游玩时间（秒）
        public bool tutorialCompleted;     // 新手引导是否完成
        public bool isCompleted;           // 关卡是否完成过
    }

    private Dictionary<string, LevelData> levelDataDict = new Dictionary<string, LevelData>();
    
    // 全局数据
    private int totalPlayCount = 0;        // 总游玩次数
    private float totalPlayTime = 0f;      // 总游玩时间
    private bool isFirstTimePlaying = true; // 是否第一次游玩游戏

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        LoadAllData();
    }

    // 加载所有数据
    void LoadAllData()
    {
        totalPlayCount = PlayerPrefs.GetInt("TotalPlayCount", 0);
        totalPlayTime = PlayerPrefs.GetFloat("TotalPlayTime", 0f);
        isFirstTimePlaying = PlayerPrefs.GetInt("IsFirstTimePlaying", 1) == 1;
        
        Debug.Log($"[GameData] 加载数据 - 总游玩次数: {totalPlayCount}, 总时间: {totalPlayTime}秒, 首次游玩: {isFirstTimePlaying}");
    }

    // 保存所有数据
    public void SaveAllData()
    {
        PlayerPrefs.SetInt("TotalPlayCount", totalPlayCount);
        PlayerPrefs.SetFloat("TotalPlayTime", totalPlayTime);
        PlayerPrefs.SetInt("IsFirstTimePlaying", isFirstTimePlaying ? 1 : 0);
        PlayerPrefs.Save();
        
        Debug.Log($"[GameData] 保存数据 - 总游玩次数: {totalPlayCount}");
    }

    // 获取关卡数据
    public LevelData GetLevelData(string levelName)
    {
        if (!levelDataDict.ContainsKey(levelName))
        {
            LevelData data = new LevelData
            {
                levelName = levelName,
                playCount = PlayerPrefs.GetInt($"Level_{levelName}_PlayCount", 0),
                totalPlayTime = PlayerPrefs.GetFloat($"Level_{levelName}_TotalTime", 0f),
                tutorialCompleted = PlayerPrefs.GetInt($"Level_{levelName}_TutorialDone", 0) == 1,
                isCompleted = PlayerPrefs.GetInt($"Level_{levelName}_Completed", 0) == 1
            };
            levelDataDict[levelName] = data;
        }
        return levelDataDict[levelName];
    }

    // 保存关卡数据
    void SaveLevelData(LevelData data)
    {
        PlayerPrefs.SetInt($"Level_{data.levelName}_PlayCount", data.playCount);
        PlayerPrefs.SetFloat($"Level_{data.levelName}_TotalTime", data.totalPlayTime);
        PlayerPrefs.SetInt($"Level_{data.levelName}_TutorialDone", data.tutorialCompleted ? 1 : 0);
        PlayerPrefs.SetInt($"Level_{data.levelName}_Completed", data.isCompleted ? 1 : 0);
        PlayerPrefs.Save();
    }

    // 记录关卡开始
    public void RecordLevelStart(string levelName)
    {
        LevelData data = GetLevelData(levelName);
        data.playCount++;
        totalPlayCount++;
        
        SaveLevelData(data);
        SaveAllData();
        
        Debug.Log($"[GameData] 关卡 {levelName} 开始 - 第 {data.playCount} 次游玩");
    }

    // 记录关卡完成
    public void RecordLevelComplete(string levelName, float playTime)
    {
        LevelData data = GetLevelData(levelName);
        data.totalPlayTime += playTime;
        data.isCompleted = true;
        totalPlayTime += playTime;
        
        SaveLevelData(data);
        SaveAllData();
        
        Debug.Log($"[GameData] 关卡 {levelName} 完成 - 用时 {playTime}秒");
    }

    // 标记新手引导完成
    public void MarkTutorialCompleted(string levelName)
    {
        LevelData data = GetLevelData(levelName);
        data.tutorialCompleted = true;
        SaveLevelData(data);
        
        Debug.Log($"[GameData] 关卡 {levelName} 新手引导已完成");
    }

    // 检查是否需要显示新手引导
    public bool ShouldShowTutorial(string levelName)
    {
        // 如果不是第一次玩游戏，不显示
        if (!isFirstTimePlaying)
        {
            Debug.Log($"[GameData] 不显示新手引导 - 不是首次游玩游戏");
            return false;
        }
        
        // 如果这个关卡的新手引导已完成，不显示
        LevelData data = GetLevelData(levelName);
        if (data.tutorialCompleted)
        {
            Debug.Log($"[GameData] 不显示新手引导 - 关卡 {levelName} 引导已完成");
            return false;
        }
        
        Debug.Log($"[GameData] 显示新手引导 - 关卡 {levelName}");
        return true;
    }

    // 标记不再是第一次游玩
    public void MarkNotFirstTime()
    {
        isFirstTimePlaying = false;
        SaveAllData();
    }

    // 获取总游玩次数
    public int GetTotalPlayCount()
    {
        return totalPlayCount;
    }

    // 获取总游玩时间
    public float GetTotalPlayTime()
    {
        return totalPlayTime;
    }

    // 重置所有数据（调试用）
    public void ResetAllData()
    {
        PlayerPrefs.DeleteAll();
        levelDataDict.Clear();
        totalPlayCount = 0;
        totalPlayTime = 0f;
        isFirstTimePlaying = true;
        Debug.Log("[GameData] 所有数据已重置");
    }
}
