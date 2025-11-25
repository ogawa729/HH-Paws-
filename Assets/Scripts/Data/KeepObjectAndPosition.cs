using UnityEngine;

public class KeepObjectAndPosition : MonoBehaviour
{
    void Awake()
    {
        // 让物体本身（及所有组件、数据、位置）跨场景保留
        DontDestroyOnLoad(gameObject);
    }
}