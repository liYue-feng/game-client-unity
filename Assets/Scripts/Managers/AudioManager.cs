using UnityEngine;
using System.Collections.Generic;
using Game.Core;

/// <summary>
/// 音效管理器：管理BGM和SFX播放。
/// 单例模式，支持音量控制、音效池化。
///
/// 音效加载优先级:
/// 1. Resources/Sounds/*.wav （小森平免费音效库，免费可商用）
/// 2. 程序化生成占位音效（正弦波/噪声）
///
/// 下载小森平音效:
///   bash download_sounds.sh  查看下载指引
///   主页: https://taira-komori.net/freesounden.html
///   下载后放入 Assets/Resources/Sounds/ 即可自动加载
///
/// 生成国风BGM推荐用 MiniMax Music 2.6（免费500次/天）:
///   主菜单: 古琴独奏，悠远空灵，水墨画意境
///   战斗:   密集鼓点+琵琶扫弦，紧张急促
///   Boss:   大鼓重击+唢呐高亢，黑暗压迫
/// </summary>
public class AudioManager : MonoBehaviour, IGameService
{
    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogError("[AudioManager] Service is not installed by GameApplication.");
            }

            return _instance;
        }
    }

    public string ServiceName => nameof(AudioManager);

    [Header("音量")]
    [Range(0f, 1f)] public float masterVolume = 0.8f;
    [Range(0f, 1f)] public float bgmVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    private AudioSource _bgmSource;
    private readonly Queue<AudioSource> _sfxPool = new Queue<AudioSource>();
    private const int MaxSfxSources = 16;
    private const string SoundsResourcePath = "Sounds";

    // 音频缓存: clipName → AudioClip
    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    // 记录哪些音效已从Resources加载成功
    private readonly HashSet<string> _loadedFromResources = new HashSet<string>();
    private readonly HashSet<AudioClip> _generatedRuntimeClips = new HashSet<AudioClip>();
    private bool _initialized;
    private bool _initializing;

    /// <summary>已加载的clip名称列表</summary>
    public IReadOnlyCollection<string> LoadedClipNames => _clips.Keys;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    internal static AudioManager Install(Transform parent)
    {
        if (_instance != null)
        {
            return _instance;
        }

        var serviceObject = new GameObject("[AudioManager]");
        serviceObject.transform.SetParent(parent, false);
        return serviceObject.AddComponent<AudioManager>();
    }

    public void Initialize()
    {
        if (_initialized || _initializing)
        {
            return;
        }

        _initializing = true;
        try
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = bgmVolume * masterVolume;

            for (int i = 0; i < MaxSfxSources; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.loop = false;
                src.playOnAwake = false;
                _sfxPool.Enqueue(src);
            }

            LoadAllSounds();
            _initialized = true;
        }
        catch
        {
            CleanupRuntimeState();
            throw;
        }
        finally
        {
            _initializing = false;
        }
    }

    public void Shutdown()
    {
        CleanupRuntimeState();
    }

    private void CleanupRuntimeState()
    {
        if (_bgmSource != null)
        {
            _bgmSource.Stop();
            _bgmSource.clip = null;
        }

        foreach (var source in _sfxPool)
        {
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
        }

        foreach (var clip in _generatedRuntimeClips)
        {
            if (clip != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(clip);
                }
                else
                {
                    DestroyImmediate(clip);
                }
            }
        }

        _sfxPool.Clear();
        _clips.Clear();
        _loadedFromResources.Clear();
        _generatedRuntimeClips.Clear();
        _bgmSource = null;
        _initialized = false;
    }

    internal static void ResetStaticState()
    {
        _instance = null;
    }

    private void OnDestroy()
    {
        Shutdown();
        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }
    }

    /// <summary>
    /// 加载所有音效：优先从 Resources/Sounds/ 加载真实音频，
    /// 没有的用程序化占位音效。
    /// </summary>
    public void LoadAllSounds()
    {
        foreach (var kv in SoundCatalog.Catalog)
        {
            string clipName = kv.Key;
            string suggestedFile = kv.Value.suggestedFile;
            // 去掉 .wav 后缀作为 Resources 路径
            string resourceName = suggestedFile.Replace(".wav", "").Replace(".mp3", "");

            // 尝试从 Resources/Sounds/ 加载
            var clip = Resources.Load<AudioClip>($"{SoundsResourcePath}/{resourceName}");

            if (clip != null)
            {
                _clips[clipName] = clip;
                _loadedFromResources.Add(clipName);
            }
        }

        // 对没有找到真实音频的clip，生成程序化占位音效
        GeneratePlaceholderClips();

        int realCount = _loadedFromResources.Count;
        int totalCount = _clips.Count;
        Debug.Log($"[AudioManager] 音效加载完成: {realCount}/{totalCount} 来自Resources, {totalCount - realCount} 使用占位音效");
        if (realCount == 0)
        {
            Debug.Log("[AudioManager] 提示: 运行 bash download_sounds.sh 查看下载真实音效的指引");
        }
    }

    /// <summary>生成占位音效（仅对未从Resources加载的clip）</summary>
    private void GeneratePlaceholderClips()
    {
        // 战斗
        TryAddPlaceholder("hit", () => GenerateToneClip(440f, 0.08f, 0.3f));
        TryAddPlaceholder("slash", () => GenerateNoiseClip(0.1f, 0.4f));
        TryAddPlaceholder("parry", () => GenerateToneClip(880f, 0.12f, 0.5f));
        TryAddPlaceholder("heavy_hit", () => GenerateToneClip(180f, 0.15f, 0.5f));
        TryAddPlaceholder("punch", () => GenerateNoiseClip(0.06f, 0.3f));
        TryAddPlaceholder("damage_taken", () => GenerateToneClip(300f, 0.15f, 0.3f));

        // 死亡
        TryAddPlaceholder("death", () => GenerateToneClip(220f, 0.3f, 0.4f));
        TryAddPlaceholder("boss_death", () => GenerateToneClip(80f, 0.5f, 0.7f));

        // 移动
        TryAddPlaceholder("dash", () => GenerateSweepClip(200f, 600f, 0.15f));
        TryAddPlaceholder("footstep", () => GenerateNoiseClip(0.03f, 0.15f));

        // UI
        TryAddPlaceholder("ui_click", () => GenerateToneClip(520f, 0.04f, 0.15f));
        TryAddPlaceholder("ui_confirm", () => GenerateToneClip(660f, 0.06f, 0.2f));
        TryAddPlaceholder("ui_cancel", () => GenerateToneClip(200f, 0.06f, 0.15f));
        TryAddPlaceholder("ui_coin", () => GenerateToneClip(1200f, 0.05f, 0.15f));

        // 升级/奖励
        TryAddPlaceholder("levelup", () => GenerateSweepClip(400f, 1200f, 0.25f));
        TryAddPlaceholder("exp_pickup", () => GenerateToneClip(660f, 0.05f, 0.2f));

        // 技能
        TryAddPlaceholder("special_skill", () => GenerateSweepClip(100f, 800f, 0.3f));
        TryAddPlaceholder("buff_activate", () => GenerateSweepClip(500f, 1500f, 0.2f));

        // 环境
        TryAddPlaceholder("ambient_wind", () => GenerateNoiseClip(1.5f, 0.1f));
        TryAddPlaceholder("ambient_rain", () => GenerateNoiseClip(2f, 0.08f));

        // BGM（长循环）
        TryAddPlaceholder("bgm_menu", () => GenerateToneClip(196f, 3f, 0.15f));
        TryAddPlaceholder("bgm_battle", () => GenerateToneClip(130f, 3f, 0.2f));
        TryAddPlaceholder("bgm_boss", () => GenerateToneClip(65f, 3f, 0.3f));

        // 结束
        TryAddPlaceholder("game_over", () => GenerateToneClip(160f, 0.5f, 0.5f));
        TryAddPlaceholder("victory", () => GenerateSweepClip(400f, 1200f, 0.5f));
    }

    private void TryAddPlaceholder(string name, System.Func<AudioClip> generator)
    {
        if (!_clips.ContainsKey(name))
        {
            var clip = generator();
            _clips[name] = clip;
            _generatedRuntimeClips.Add(clip);
        }
    }

    // ========== BGM ==========

    public void PlayBGM(string clipName)
    {
        if (_clips.TryGetValue(clipName, out var clip))
        {
            if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;
            _bgmSource.clip = clip;
            _bgmSource.volume = bgmVolume * masterVolume;
            _bgmSource.Play();
        }
    }

    public void StopBGM()
    {
        _bgmSource.Stop();
    }

    // ========== SFX ==========

    public void PlaySFX(string clipName, float pitchVariation = 0.05f)
    {
        if (!_clips.TryGetValue(clipName, out var clip)) return;

        var src = GetFreeSfxSource();
        if (src == null) return;

        src.clip = clip;
        src.volume = sfxVolume * masterVolume;
        src.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        src.Play();
    }

    private AudioSource GetFreeSfxSource()
    {
        int count = _sfxPool.Count;
        for (int i = 0; i < count; i++)
        {
            var src = _sfxPool.Dequeue();
            _sfxPool.Enqueue(src);
            if (!src.isPlaying) return src;
        }
        return _sfxPool.Peek();
    }

    // ========== 音量控制 ==========

    public void SetMasterVolume(float v)
    {
        masterVolume = v;
        _bgmSource.volume = bgmVolume * masterVolume;
    }

    public void SetBgmVolume(float v)
    {
        bgmVolume = v;
        _bgmSource.volume = bgmVolume * masterVolume;
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = v;
    }

    /// <summary>手动加载外部音频文件</summary>
    public void LoadClip(string name, AudioClip clip)
    {
        _clips[name] = clip;
    }

    /// <summary>检查某个音效是否已从Resources加载真实文件</summary>
    public bool IsLoadedFromResources(string clipName)
    {
        return _loadedFromResources.Contains(clipName);
    }

    // ========== 程序化音频生成（占位用） ==========

    private AudioClip GenerateToneClip(float freq, float duration, float amplitude)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 20f) * amplitude;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env;
        }

        var clip = AudioClip.Create("tone_" + freq, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateSweepClip(float startFreq, float endFreq, float duration)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(startFreq, endFreq, t / duration);
            float env = Mathf.Exp(-t * 8f) * 0.3f;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env;
        }

        var clip = AudioClip.Create("sweep_" + startFreq, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateNoiseClip(float duration, float amplitude)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 15f) * amplitude;
            data[i] = (Random.value * 2f - 1f) * env;
        }

        var clip = AudioClip.Create("noise", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

#if UNITY_EDITOR
/// <summary>编辑器菜单：重新加载音效</summary>
public static class AudioManagerMenu
{
    [UnityEditor.MenuItem("Tools/Reload Sound Catalog")]
    public static void ReloadSounds()
    {
        if (Application.isPlaying && AudioManager.Instance != null)
        {
            AudioManager.Instance.LoadAllSounds();
            Debug.Log("[AudioManager] 音效已重新加载");
        }
        else
        {
            Debug.Log("[AudioManager] 请在 Play 模式下使用此功能");
        }
    }

    [UnityEditor.MenuItem("Tools/Open Sound Download Guide")]
    public static void OpenDownloadGuide()
    {
        Application.OpenURL("https://taira-komori.net/freesounden.html");
    }
}
#endif
