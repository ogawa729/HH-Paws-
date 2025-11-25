using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按钮切换控制器 - 用于选关界面的多选按钮
/// </summary>
public class ButtonToggle : MonoBehaviour
{
    [Header("UI 引用")]
    public Image buttonImage;              // 按钮图片
    public Color normalColor = Color.white;    // 正常颜色
    public Color selectedColor = Color.yellow; // 选中颜色

    [Header("关卡信息")]
    public int levelIndex;                 // 关卡索引（对应 SceneFlowManager 中的关卡）

    private bool isSelected = false;

    void Start()
    {
        if (buttonImage == null)
        {
            buttonImage = GetComponent<Image>();
        }

        UpdateVisual();
    }

    // 切换选中状态
    public void Toggle()
    {
        isSelected = !isSelected;
        UpdateVisual();
        
        Debug.Log($"[ButtonToggle] 关卡 {levelIndex} 选中状态: {isSelected}");
    }

    // 设置选中状态
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisual();
    }

    // 获取选中状态
    public bool IsSelected()
    {
        return isSelected;
    }

    // 更新视觉效果
    void UpdateVisual()
    {
        if (buttonImage != null)
        {
            buttonImage.color = isSelected ? selectedColor : normalColor;
        }
    }

    // 获取关卡索引
    public int GetLevelIndex()
    {
        return levelIndex;
    }
}
