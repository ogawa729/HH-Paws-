using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StorySequenceController : MonoBehaviour
{
    [System.Serializable]
    public class StoryStep
    {
        public string text;                    // 要显示的文本
        public Sprite image;                   // 要显示的图片（可选）
        public GameObject[] objectsToShow;     // 在这一步要显示的物体
        public GameObject[] objectsToHide;     // 在这一步要隐藏的物体
        public string[] objectNamesToShow;     // 通过名字显示的物体（支持未激活的物体）
        public string[] objectNamesToHide;     // 通过名字隐藏的物体
        [Range(0.1f, 10f)]
        public float waitBeforeObjectChange = 1f;  // 文字显示完后，执行物体操作前的等待时间
        [Range(0.1f, 10f)]
        public float waitAfterObjectChange = 1f;   // 物体操作完后，进入下一步前的等待时间
        public AudioClip customVoiceClip;      // 自定义语音音效（可选，留空则使用默认）
    }

    [Header("UI 引用")]
    public Button startButton;                 // 触发按钮
    public TextMeshProUGUI displayText;        // 显示文本的 TextMeshPro 组件
    public Image displayImage;                 // 显示图片的 Image 组件

    [Header("故事步骤")]
    public List<StoryStep> storySteps = new List<StoryStep>();

    [Header("设置")]
    public bool autoAdvance = true;            // 是否自动推进到下一步
    public bool hideButtonDuringSequence = true; // 播放时是否隐藏按钮
    public bool disableButtonAfterClick = true;  // 按钮被按下后是否禁用
    [Range(0.01f, 0.5f)]
    public float typeSpeed = 0.05f;            // 打字速度（每个字符的间隔时间）
    public bool enableDebugLog = true;         // 是否启用调试日志

    [Header("搜索范围")]
    public Transform searchRoot;               // 搜索物体的根节点（留空则搜索整个场景）

    [Header("语音音效设置")]
    public AudioSource audioSource;            // 音频源组件
    public AudioClip[] voiceClips;             // 语音音效数组（可以放多个音效随机播放）
    public bool enableVoice = true;            // 是否启用语音
    [Range(0.5f, 2f)]
    public float voicePitch = 1f;              // 音调（1为正常，越高越尖）
    [Range(0f, 1f)]
    public float voiceVolume = 0.5f;           // 音量
    public int playVoiceEveryNChars = 1;       // 每隔几个字符播放一次音效（1=每个字，2=每两个字）

    private int currentStepIndex = 0;
    private bool isPlaying = false;
    private bool hasBeenTriggered = false;     // 记录按钮是否已被触发过
    private Dictionary<string, GameObject> cachedObjects = new Dictionary<string, GameObject>(); // 缓存找到的物体
    private int charCounter = 0;               // 字符计数器

    void Start()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartSequence);
        }

        // 如果没有指定 AudioSource，自动添加一个
        if (audioSource == null)
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // 显示第一条文字和图片
        ShowFirstStep();
    }

    void ShowFirstStep()
    {
        if (storySteps.Count > 0)
        {
            StoryStep firstStep = storySteps[0];

            // 显示第一张图片
            if (displayImage != null && firstStep.image != null)
            {
                displayImage.gameObject.SetActive(true);
                displayImage.sprite = firstStep.image;
            }

            // 开始打字机效果显示第一条文本
            if (displayText != null && !string.IsNullOrEmpty(firstStep.text))
            {
                displayText.gameObject.SetActive(true);
                StartCoroutine(ShowFirstStepWithDelay(firstStep));
            }
        }
    }

    IEnumerator ShowFirstStepWithDelay(StoryStep step)
    {
        // 先播放文字
        yield return StartCoroutine(TypeText(step.text, step.customVoiceClip));

        // 文字播放完后，等待一段时间
        yield return new WaitForSeconds(step.waitBeforeObjectChange);

        // 执行物体的显示/隐藏（通过引用）
        foreach (GameObject obj in step.objectsToShow)
        {
            if (obj != null && CanSafelyToggle(obj))
            {
                obj.SetActive(true);
                if (enableDebugLog) Debug.Log($"[Story] 显示物体（引用）: {obj.name}");
            }
        }

        foreach (GameObject obj in step.objectsToHide)
        {
            if (obj != null && CanSafelyToggle(obj))
            {
                obj.SetActive(false);
                if (enableDebugLog) Debug.Log($"[Story] 隐藏物体（引用）: {obj.name}");
            }
        }

        // 执行物体的显示/隐藏（通过名字）
        ShowObjectsByName(step.objectNamesToShow);
        HideObjectsByName(step.objectNamesToHide);
    }

    public void StartSequence()
    {
        // 如果已经触发过且设置了禁用，则直接返回
        if (hasBeenTriggered && disableButtonAfterClick) return;
        
        if (isPlaying) return;

        hasBeenTriggered = true;  // 标记为已触发
        currentStepIndex = 1;     // 从第二步开始播放（因为第一步已经显示了）
        
        if (enableDebugLog) Debug.Log("[Story] 开始播放序列");
        
        StartCoroutine(PlaySequence());
    }

    IEnumerator PlaySequence()
    {
        isPlaying = true;

        if (hideButtonDuringSequence && startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }

        while (currentStepIndex < storySteps.Count)
        {
            StoryStep step = storySteps[currentStepIndex];
            
            if (enableDebugLog) Debug.Log($"[Story] 播放步骤 {currentStepIndex + 1}/{storySteps.Count}");

            // 显示图片
            if (displayImage != null && step.image != null)
            {
                displayImage.gameObject.SetActive(true);
                displayImage.sprite = step.image;
            }
            else if (displayImage != null)
            {
                displayImage.gameObject.SetActive(false);
            }

            // 显示文本（打字机效果）
            if (displayText != null && !string.IsNullOrEmpty(step.text))
            {
                displayText.gameObject.SetActive(true);
                yield return StartCoroutine(TypeText(step.text, step.customVoiceClip));
            }

            // 文字显示完后，等待一段时间
            yield return new WaitForSeconds(step.waitBeforeObjectChange);

            // 执行物体的显示/隐藏（通过引用）
            if (enableDebugLog && step.objectsToShow.Length > 0)
            {
                Debug.Log($"[Story] 准备显示 {step.objectsToShow.Length} 个物体（引用）");
            }
            
            foreach (GameObject obj in step.objectsToShow)
            {
                if (obj != null)
                {
                    if (CanSafelyToggle(obj))
                    {
                        obj.SetActive(true);
                        if (enableDebugLog) Debug.Log($"[Story] ✓ 显示物体: {obj.name}, 状态: {obj.activeInHierarchy}");
                    }
                    else
                    {
                        if (enableDebugLog) Debug.LogWarning($"[Story] ✗ 无法显示物体: {obj.name} (安全检查失败)");
                    }
                }
                else
                {
                    if (enableDebugLog) Debug.LogWarning("[Story] ✗ 物体引用为空");
                }
            }

            if (enableDebugLog && step.objectsToHide.Length > 0)
            {
                Debug.Log($"[Story] 准备隐藏 {step.objectsToHide.Length} 个物体（引用）");
            }

            foreach (GameObject obj in step.objectsToHide)
            {
                if (obj != null)
                {
                    if (CanSafelyToggle(obj))
                    {
                        obj.SetActive(false);
                        if (enableDebugLog) Debug.Log($"[Story] ✓ 隐藏物体: {obj.name}");
                    }
                }
            }

            // 执行物体的显示/隐藏（通过名字）
            ShowObjectsByName(step.objectNamesToShow);
            HideObjectsByName(step.objectNamesToHide);

            // 物体操作完后，等待一段时间再进入下一步
            yield return new WaitForSeconds(step.waitAfterObjectChange);

            currentStepIndex++;
        }

        // 序列播放完毕
        if (displayText != null) displayText.gameObject.SetActive(false);
        if (displayImage != null) displayImage.gameObject.SetActive(false);

        // 如果设置了禁用按钮，播放完后不再显示按钮
        if (disableButtonAfterClick)
        {
            if (startButton != null)
            {
                startButton.gameObject.SetActive(false);
            }
        }
        else if (hideButtonDuringSequence && startButton != null)
        {
            startButton.gameObject.SetActive(true);
        }

        isPlaying = false;
        
        if (enableDebugLog) Debug.Log("[Story] 序列播放完成");
    }

    // 通过名字显示物体
    void ShowObjectsByName(string[] objectNames)
    {
        if (objectNames == null || objectNames.Length == 0) return;

        if (enableDebugLog)
        {
            Debug.Log($"[Story] 准备通过名字显示 {objectNames.Length} 个物体");
        }

        foreach (string objName in objectNames)
        {
            if (string.IsNullOrEmpty(objName)) continue;

            GameObject obj = FindObjectByName(objName);
            if (obj != null)
            {
                if (CanSafelyToggle(obj))
                {
                    obj.SetActive(true);
                    if (enableDebugLog) Debug.Log($"[Story] ✓ 显示物体（名字）: {objName}, 状态: {obj.activeInHierarchy}");
                }
                else
                {
                    if (enableDebugLog) Debug.LogWarning($"[Story] ✗ 无法显示物体: {objName} (安全检查失败)");
                }
            }
            else
            {
                if (enableDebugLog) Debug.LogWarning($"[Story] ✗ 找不到名为 '{objName}' 的物体");
            }
        }
    }

    // 通过名字隐藏物体
    void HideObjectsByName(string[] objectNames)
    {
        if (objectNames == null || objectNames.Length == 0) return;

        if (enableDebugLog)
        {
            Debug.Log($"[Story] 准备通过名字隐藏 {objectNames.Length} 个物体");
        }

        foreach (string objName in objectNames)
        {
            if (string.IsNullOrEmpty(objName)) continue;

            GameObject obj = FindObjectByName(objName);
            if (obj != null)
            {
                if (CanSafelyToggle(obj))
                {
                    obj.SetActive(false);
                    if (enableDebugLog) Debug.Log($"[Story] ✓ 隐藏物体（名字）: {objName}");
                }
            }
            else
            {
                if (enableDebugLog) Debug.LogWarning($"[Story] ✗ 找不到名为 '{objName}' 的物体");
            }
        }
    }

    // 通过名字查找物体（包括未激活的物体）
    GameObject FindObjectByName(string objectName)
    {
        // 先从缓存中查找
        if (cachedObjects.ContainsKey(objectName))
        {
            GameObject cached = cachedObjects[objectName];
            if (cached != null)
            {
                if (enableDebugLog) Debug.Log($"[Story] 从缓存找到物体: {objectName}");
                return cached;
            }
        }

        GameObject foundObject = null;

        // 如果指定了搜索根节点，只在该节点下搜索
        if (searchRoot != null)
        {
            if (enableDebugLog) Debug.Log($"[Story] 在 {searchRoot.name} 下搜索物体: {objectName}");
            
            Transform[] allTransforms = searchRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (t.gameObject.name == objectName)
                {
                    foundObject = t.gameObject;
                    if (enableDebugLog) Debug.Log($"[Story] ✓ 找到物体: {objectName}, 路径: {GetGameObjectPath(t.gameObject)}");
                    break;
                }
            }
        }
        else
        {
            if (enableDebugLog) Debug.Log($"[Story] 在整个场景搜索物体: {objectName}");
            
            // 搜索整个场景（包括未激活的物体）
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                // 排除预制体和编辑器资源
                if (obj.hideFlags == HideFlags.None && obj.scene.isLoaded && obj.name == objectName)
                {
                    foundObject = obj;
                    if (enableDebugLog) Debug.Log($"[Story] ✓ 找到物体: {objectName}, 路径: {GetGameObjectPath(obj)}");
                    break;
                }
            }
        }

        // 缓存找到的物体
        if (foundObject != null)
        {
            cachedObjects[objectName] = foundObject;
        }
        else
        {
            if (enableDebugLog) Debug.LogWarning($"[Story] ✗ 未找到物体: {objectName}");
        }

        return foundObject;
    }

    // 获取物体的完整路径（用于调试）
    string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    // 检查物体是否可以安全地切换激活状态
    bool CanSafelyToggle(GameObject obj)
    {
        // 不允许禁用 Canvas
        if (obj.GetComponent<Canvas>() != null)
        {
            if (enableDebugLog) Debug.LogWarning($"[Story] 不能隐藏 Canvas: {obj.name}");
            return false;
        }

        // 不允许禁用包含 PointableCanvas 的物体
        System.Type pointableCanvasType = System.Type.GetType("Rokid.UXR.Interaction.PointableCanvas");
        if (pointableCanvasType != null && obj.GetComponent(pointableCanvasType) != null)
        {
            if (enableDebugLog) Debug.LogWarning($"[Story] 不能隐藏包含 PointableCanvas 的物体: {obj.name}");
            return false;
        }

        // 不允许禁用控制器自身
        if (obj == gameObject)
        {
            if (enableDebugLog) Debug.LogWarning("[Story] 不能隐藏控制器自身");
            return false;
        }

        return true;
    }

    // 打字机效果协程（带语音）
    IEnumerator TypeText(string text, AudioClip customClip = null)
    {
        displayText.text = "";
        charCounter = 0;

        foreach (char c in text)
        {
            displayText.text += c;
            
            // 播放语音音效
            if (enableVoice && audioSource != null)
            {
                charCounter++;
                
                // 根据设置决定是否播放音效
                if (charCounter % playVoiceEveryNChars == 0)
                {
                    PlayVoiceSound(customClip);
                }
            }

            yield return new WaitForSeconds(typeSpeed);
        }
    }

    // 播放语音音效
    void PlayVoiceSound(AudioClip customClip = null)
    {
        if (audioSource == null) return;

        AudioClip clipToPlay = null;

        // 优先使用自定义音效
        if (customClip != null)
        {
            clipToPlay = customClip;
        }
        // 否则从音效数组中随机选择
        else if (voiceClips != null && voiceClips.Length > 0)
        {
            clipToPlay = voiceClips[Random.Range(0, voiceClips.Length)];
        }

        if (clipToPlay != null)
        {
            audioSource.pitch = voicePitch + Random.Range(-0.1f, 0.1f); // 添加轻微的音调变化
            audioSource.volume = voiceVolume;
            audioSource.PlayOneShot(clipToPlay);
        }
    }

    // 手动推进到下一步（如果不想自动推进）
    public void NextStep()
    {
        if (!isPlaying) return;
        StopAllCoroutines();
        currentStepIndex++;
        if (currentStepIndex < storySteps.Count)
        {
            StartCoroutine(PlaySequence());
        }
        else
        {
            isPlaying = false;
        }
    }

    // 重置按钮状态（如果需要重新触发）
    public void ResetButton()
    {
        hasBeenTriggered = false;
        if (startButton != null)
        {
            startButton.gameObject.SetActive(true);
        }
    }

    // 清除缓存（如果场景中的物体发生变化）
    public void ClearCache()
    {
        cachedObjects.Clear();
    }
}

