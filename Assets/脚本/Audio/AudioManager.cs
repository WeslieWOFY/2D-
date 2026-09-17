using UnityEngine;

/// <summary>
/// 全局音频管理器 —— 泛型单例、跨场景持久化对象（DontDestroyOnLoad）。
///
/// 职责：
///   1. 播放背景音乐（BGM）：具体何时播放哪一首由外部（各场景）调用决定；
///   2. 播放音效（SFX），保留原有的蓄力音效接口；
///   3. 维护序列化音量（_musicVolume / _sfxVolume），可在 Inspector 中调整，也支持运行时设置。
///
/// 用法：
///   AudioManager.Instance.PlayBGM(menuBgm);                // 用序列化音量播放背景乐（循环）
///   AudioManager.Instance.PlayBGM(menuBgm, 0.6f);          // 指定音量
///   AudioManager.Instance.StopBGM();                       // 停止当前背景乐
///   AudioManager.Instance.PlaySFX(sfx);                    // 用序列化音量播放
///   AudioManager.Instance.PlaySFX(sfx, 0.8f);              // 指定最终音量
///   AudioManager.Instance.PlayxuliSFX();                   // 蓄力循环音效（序列化音量）
///   AudioManager.Instance.PlayxuliSFX(0.8f);               // 蓄力循环音效（指定音量）
///   AudioManager.Instance.StopxuliSFX();
///   AudioManager.Instance.SetMusicVolume(0.5f);            // 运行时改音乐音量
///   AudioManager.Instance.SetSfxVolume(0.5f);              // 运行时改音效音量
///
/// 说明：音源字段留空会自动创建，因此把它挂到任意空物体上即可直接使用。
/// </summary>
public class AudioManager : PersistentSingleton<AudioManager>
{
    [Header("音源（留空会自动创建）")]
    [Tooltip("通用音效播放器")]
    [SerializeField] private AudioSource sFxPlayer;
    [Tooltip("蓄力循环音效播放器")]
    [SerializeField] private AudioSource xuli;
    [Tooltip("背景音乐播放器")]
    [SerializeField] private AudioSource musicPlayer;
    [Tooltip("蓄力音效片段")]
    [SerializeField] protected AudioClip xuliSFX;

    [Header("音量（序列化，可在 Inspector 中调整）")]
    [Range(0f, 1f)]
    [SerializeField] private float _musicVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float _sfxVolume = 1f;

    [Header("静音（勾选后实际静音，但保留音量数值）")]
    [SerializeField] private bool _musicMuted;
    [SerializeField] private bool _sfxMuted;

    /// <summary>当前正在播放的背景音乐，用于去重</summary>
    private AudioClip _currentBgm;

    protected override void Awake()
    {
        base.Awake();

        // 音源缺失时自动创建，保证挂到任意物体上都能用
        EnsureAudioSources();
        ApplyVolumes();
    }

    protected override void Start()
    {
        base.Start();

        if (xuli != null)
            xuli.clip = xuliSFX;
    }

    // ==================== 背景音乐 ====================

    /// <summary>播放背景音乐（循环）。已在播放同一首时不打断重播。</summary>
    public void PlayBGM(AudioClip clip)
    {
        PlayBGM(clip, MusicVolumeEffective);
    }

    /// <summary>播放背景音乐，可指定音量</summary>
    public void PlayBGM(AudioClip clip, float volume)
    {
        if (clip == null || musicPlayer == null) return;

        // 已经在播同一首则跳过，避免外部重复调用导致从头重播
        if (_currentBgm == clip && musicPlayer.isPlaying) return;

        musicPlayer.clip = clip;
        musicPlayer.loop = true;
        musicPlayer.volume = Mathf.Clamp01(volume);
        musicPlayer.Play();
        _currentBgm = clip;
    }

    /// <summary>停止当前背景音乐</summary>
    public void StopBGM()
    {
        if (musicPlayer == null) return;
        musicPlayer.Stop();
        _currentBgm = null;
    }

    // ==================== 音效 ====================

    /// <summary>播放音效（使用序列化音效音量，静音时为 0）</summary>
    public void PlaySFX(AudioClip audioClip)
    {
        PlaySFX(audioClip, SfxVolumeEffective);
    }

    /// <summary>播放音效，传入的 volume 为最终音量</summary>
    public void PlaySFX(AudioClip audioClip, float volume)
    {
        if (audioClip == null || sFxPlayer == null) return;
        sFxPlayer.PlayOneShot(audioClip, volume);
    }

    /// <summary>播放蓄力循环音效（使用序列化音效音量，静音时为 0）</summary>
    public void PlayxuliSFX()
    {
        PlayxuliSFX(SfxVolumeEffective);
    }

    /// <summary>播放蓄力循环音效，volume 为最终音量</summary>
    public void PlayxuliSFX(float volume)
    {
        if (xuli == null) return;
        xuli.volume = Mathf.Clamp01(volume);
        xuli.loop = true;
        xuli.Play();
    }

    /// <summary>停止蓄力循环音效</summary>
    public void StopxuliSFX()
    {
        if (xuli == null) return;
        xuli.Stop();
        xuli.loop = false;
    }

    // ==================== 音量维护 ====================

    /// <summary>运行时设置音乐音量（0~1），保留该数值并实时应用到当前 BGM</summary>
    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        if (musicPlayer != null)
            musicPlayer.volume = MusicVolumeEffective;
    }

    /// <summary>运行时设置音效音量（0~1），作用于后续播放的音效</summary>
    public void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
    }

    /// <summary>静音/恢复音乐（静音时实际音量为 0，但保留 _musicVolume 数值）</summary>
    public void SetMusicMuted(bool muted)
    {
        _musicMuted = muted;
        if (musicPlayer != null)
            musicPlayer.volume = MusicVolumeEffective;
    }

    /// <summary>静音/恢复音效（静音时实际音量为 0，但保留 _sfxVolume 数值）</summary>
    public void SetSfxMuted(bool muted)
    {
        _sfxMuted = muted;
        // 立即作用于正在播放的蓄力循环音效
        if (xuli != null)
            xuli.volume = SfxVolumeEffective;
    }

    /// <summary>当前音乐音量（保留值）</summary>
    public float MusicVolume => _musicVolume;

    /// <summary>当前音效音量（保留值）</summary>
    public float SfxVolume => _sfxVolume;

    /// <summary>音乐静音状态</summary>
    public bool MusicMuted => _musicMuted;

    /// <summary>音效静音状态</summary>
    public bool SfxMuted => _sfxMuted;

    /// <summary>实际生效的音乐音量（静音时为 0）</summary>
    public float MusicVolumeEffective => _musicMuted ? 0f : _musicVolume;

    /// <summary>实际生效的音效音量（静音时为 0）</summary>
    public float SfxVolumeEffective => _sfxMuted ? 0f : _sfxVolume;

    // ==================== 内部实现 ====================

    private void EnsureAudioSources()
    {
        if (sFxPlayer == null)
        {
            sFxPlayer = gameObject.AddComponent<AudioSource>();
            sFxPlayer.playOnAwake = false;
        }
        if (musicPlayer == null)
        {
            musicPlayer = gameObject.AddComponent<AudioSource>();
            musicPlayer.playOnAwake = false;
            musicPlayer.loop = true;
        }
        if (xuli == null)
        {
            xuli = gameObject.AddComponent<AudioSource>();
            xuli.playOnAwake = false;
        }
    }

    private void ApplyVolumes()
    {
        if (musicPlayer != null)
            musicPlayer.volume = MusicVolumeEffective;
    }
}
