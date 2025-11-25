using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选关界面管理器
/// </summary>
public class LevelSelectionManager : MonoBehaviour
{
    [Header("UI 引用")]
    public ButtonToggle[] levelButtons;    // 所有关卡按钮
    public Button startButton;             // 开始按钮
    public Text infoText;                  // 提示文本（可选）

    [Header("设置")]
    public string returnSceneName = "MainMenu"; // 完成后返回的场景

    void Start()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

        UpdateInfoText();
    }

    // 开始按钮点击
    void OnStartButtonClicked()
    {
        List<int> selectedLevels = GetSelectedLevels();

        if (selectedLevels.Count == 0)
        {
            Debug.LogWarning("[LevelSelection] 请至少选择一个关卡");
            if (infoText != null)
            {
                infoText.text = "请至少选择一个关卡！";
            }
            return;
        }

        Debug.Log($"[LevelSelection] 开始游玩 {selectedLevels.Count} 个关卡");
        
        // 开始选中的关卡
        SceneFlowManager.Instance.StartSelectedLevels(selectedLevels, returnSceneName);
    }

    // 获取选中的关卡索引列表
    List<int> GetSelectedLevels()
    {
        List<int> selected = new List<int>();

        foreach (ButtonToggle button in levelButtons)
        {
            if (button != null && button.IsSelected())
            {
                selected.Add(button.GetLevelIndex());
            }
        }

        // 按索引排序，确保按顺序游玩
        selected.Sort();

        return selected;
    }

    // 更新提示文本
    void UpdateInfoText()
    {
        if (infoText != null)
        {
            int count = GetSelectedLevels().Count;
            if (count == 0)
            {
                infoText.text = "请选择关卡";
            }
            else
            {
                infoText.text = $"已选择 {count} 个关卡";
            }
        }
    }

    // 按钮点击时调用（在 Inspector 中绑定）
    public void OnLevelButtonClicked()
    {
        UpdateInfoText();
    }
}
