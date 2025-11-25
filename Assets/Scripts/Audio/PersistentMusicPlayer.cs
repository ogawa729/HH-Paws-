using System.Collections;
using UnityEngine;

/// <summary>
/// 持久化音乐播放器：单例 + DontDestroyOnLoad
/// - 把脚本挂到一个 GameObject 上（推荐命名为 PersistentMusicPlayer），并添加 AudioSource
/// - 会在 Awake 时确保只有一个实例存活（多余的会被销毁）
/// - 支持在 Inspector 配置 AudioClip、循环、音量、是否在 Awake 播放
/// - 提供公开 API：Play(clip), Stop(), SetVolume()
/// 使用示例：PersistentMusicPlayer.Instance.Play(myClip, true);
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class PersistentMusicPlayer : MonoBehaviour
{
    public static PersistentMusicPlayer Instance { get; private set; }

    [Header("Audio settings")]
    [Tooltip("默认播放的 AudioClip（可留空，运行时用 API 设置也可）")]
    public AudioClip defaultClip;
    [Tooltip("是否在 Awake 时自动播放 defaultClip（如果存在）")]
    public bool playOnAwake = true;
    [Tooltip("是否循环播放")]
    public bool loop = true;
    [Range(0f,1f)]
    public float volume = 1f;

    [Header("Behavior")]
    [Tooltip("是否在场景切换时一直保留该 GameObject（总是 true）")]
    public bool persistAcrossScenes = true;

    AudioSource audioSource;
    Coroutine fadeCoroutine;

    void Awake()
    {
        // 单例逻辑：如果已有实例且不是自己，则销毁自己
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false; // 控制由脚本管理
        audioSource.loop = loop;
        audioSource.volume = Mathf.Clamp01(volume);

        if (defaultClip != null && playOnAwake)
        {
            audioSource.clip = defaultClip;
            audioSource.loop = loop;
            audioSource.Play();
        }
    }

    void OnValidate()
    {
        // 编辑器里保持 AudioSource in-sync
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.loop = loop;
            audioSource.volume = Mathf.Clamp01(volume);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 立即播放给定 clip（替换当前 clip）。如果 clip 为 null，则停止播放。
    /// </summary>
    public void Play(AudioClip clip, bool loopClip = true, float startVolume = 1f)
    {
        if (clip == null)
        {
            Stop();
            return;
        }

        audioSource.clip = clip;
        audioSource.loop = loopClip;
        audioSource.volume = Mathf.Clamp01(startVolume);
        audioSource.Play();
    }

    /// <summary>
    /// 停止播放（立即）。
    /// </summary>
    public void Stop()
    {
        if (audioSource.isPlaying) audioSource.Stop();
    }

    /// <summary>
    /// 设置音量（立即）。
    /// </summary>
    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        if (audioSource != null) audioSource.volume = volume;
    }

    /// <summary>
    /// 渐入切换到新 clip（可选淡入/淡出时长）
    /// </summary>
    public void PlayWithFade(AudioClip clip, float fadeDuration = 1f, bool loopClip = true)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(CoPlayWithFade(clip, fadeDuration, loopClip));
    }

    IEnumerator CoPlayWithFade(AudioClip clip, float fadeDuration, bool loopClip)
    {
        float startVol = audioSource.isPlaying ? audioSource.volume : 0f;
        float t = 0f;

        // fade out current
        if (audioSource.isPlaying && fadeDuration > 0f)
        {
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVol, 0f, t / fadeDuration);
                yield return null;
            }
        }

        audioSource.Stop();

        // switch clip
        audioSource.clip = clip;
        audioSource.loop = loopClip;
        audioSource.Play();

        // fade in
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, volume, t / fadeDuration);
            yield return null;
        }

        audioSource.volume = volume;
        fadeCoroutine = null;
    }

    /// <summary>
    /// 静态便捷方法：若实例存在则调用 Play，否则返回 false。
    /// </summary>
    public static bool TryPlay(AudioClip clip, bool loopClip = true, float startVolume = 1f)
    {
        if (Instance == null) return false;
        Instance.Play(clip, loopClip, startVolume);
        return true;
    }

    public static void TryStop()
    {
        if (Instance == null) return;
        Instance.Stop();
    }
}
