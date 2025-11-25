using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))] // 自动添加Button组件，避免遗漏
public class ButtonColorToggle : MonoBehaviour
{
    [Header("颜色配置")]
    public Color normalColor = Color.white; // 初始颜色（未选中状态）
    public Color toggleColor = Color.cyan;  // 切换后颜色（选中状态）
    public bool startWithToggleColor = false; // 是否默认显示切换颜色（默认false：初始为normalColor）

    private Button targetButton;
    private Image buttonImage; // 控制按钮背景颜色
    private bool isToggled = false; // 记录当前是否处于切换状态

    void Start()
    {
        // 获取按钮组件和对应的Image（背景图）
        targetButton = GetComponent<Button>();
        buttonImage = targetButton.image;

        // 初始化颜色和状态
        isToggled = startWithToggleColor;
        UpdateButtonColor();

        // 给按钮绑定点击事件
        targetButton.onClick.AddListener(OnButtonClicked);
    }

    // 按钮点击时触发
    private void OnButtonClicked()
    {
        // 切换状态（true ↔ false）
        isToggled = !isToggled;
        // 更新按钮颜色
        UpdateButtonColor();
    }

    // 根据当前状态更新颜色
    private void UpdateButtonColor()
    {
        if (isToggled)
        {
            buttonImage.color = toggleColor;
        }
        else
        {
            buttonImage.color = normalColor;
        }
    }

    // 外部调用接口（可选：如需其他脚本控制颜色切换）
    public void ToggleColor(bool forceToggle = false)
    {
        if (forceToggle)
        {
            isToggled = true;
        }
        else
        {
            isToggled = !isToggled;
        }
        UpdateButtonColor();
    }

    // 外部获取当前状态（可选）
    public bool IsToggled()
    {
        return isToggled;
    }
}