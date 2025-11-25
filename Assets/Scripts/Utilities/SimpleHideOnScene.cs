using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 简单版：在指定场景中隐藏物体
/// </summary>
public class SimpleHideOnScene : MonoBehaviour
{
    [Header("隐藏设置")]
    [Tooltip("在这些场景中隐藏物体")]
    public string[] hideInScenes;
    
    [Tooltip("要隐藏的物体（留空则隐藏自己）")]
    public GameObject[] objectsToHide;
    
    [Header("调试")]
    public bool enableDebugLog = false;
    
    void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckCurrentScene();
    }
    
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckCurrentScene();
    }
    
    void CheckCurrentScene()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        bool shouldHide = false;
        
        foreach (string sceneName in hideInScenes)
        {
            if (sceneName == currentScene)
            {
                shouldHide = true;
                break;
            }
        }
        
        if (shouldHide)
        {
            if (objectsToHide == null || objectsToHide.Length == 0)
            {
                // 隐藏自己
                gameObject.SetActive(false);
                if (enableDebugLog)
                {
                    Debug.Log($"[SimpleHide] 在场景 {currentScene} 中隐藏 {gameObject.name}");
                }
            }
            else
            {
                // 隐藏指定物体
                foreach (GameObject obj in objectsToHide)
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                        if (enableDebugLog)
                        {
                            Debug.Log($"[SimpleHide] 在场景 {currentScene} 中隐藏 {obj.name}");
                        }
                    }
                }
            }
        }
        else
        {
            // 显示物体
            if (objectsToHide == null || objectsToHide.Length == 0)
            {
                gameObject.SetActive(true);
            }
            else
            {
                foreach (GameObject obj in objectsToHide)
                {
                    if (obj != null)
                    {
                        obj.SetActive(true);
                    }
                }
            }
        }
    }
}
