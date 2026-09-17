using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单场景管理器 —— 负责初始游戏界面：
///   1. 背景图片按顺序淡入淡出循环播放；
///   2. "开始游戏" 与 "游戏说明" 按钮；
///   3. 游戏说明弹窗（弹出文本，可关闭）；
///   4. 点击"开始游戏"通过全局事件通知 SceneChangerManager 切换关卡。
///
/// 跳转到哪个场景由序列化字段 _targetSceneName 指定，在 Inspector 中修改即可。
/// 依赖：GlobalEventBus（常驻）、SceneChangerManager（常驻）。
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("背景轮播")]
    [Tooltip("按播放顺序排列的背景图片（Image 组件）")]
    [SerializeField] private Image[] _backgroundImages;
    [Tooltip("单张图片淡入时长（秒）")]
    [SerializeField] private float _fadeInDuration = 1.2f;
    [Tooltip("图片淡入后停留时长（秒）")]
    [SerializeField] private float _holdDuration = 2.5f;
    [Tooltip("单张图片淡出时长（秒）")]
    [SerializeField] private float _fadeOutDuration = 1.2f;
    [Tooltip("勾选后交叉淡入淡出（旧图淡出的同时新图淡入），更流畅")]
    [SerializeField] private bool _crossfade = true;
    [Tooltip("是否自动开始轮播")]
    [SerializeField] private bool _autoPlay = true;

    [Header("按钮")]
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _howToButton;
    [SerializeField] private Button _quitButton;

    [Header("游戏说明弹窗")]
    [Tooltip("说明弹窗根物体，默认隐藏")]
    [SerializeField] private GameObject _instructionsPanel;
    [Tooltip("说明文字（可在 Inspector 里直接改内容）")]
    [SerializeField] private Text _instructionsText;
    [SerializeField] private Button _closeInstructionsButton;
    [Tooltip("说明弹窗打开时隐藏的设置按钮（右上角），关闭后恢复")]
    [SerializeField] private GameObject _settingsButton;

    [Header("跳转关卡")]
    [Tooltip("点击开始游戏后切换到的场景名（需在 Build Settings 中注册）")]
    [SerializeField] private string _targetSceneName = "M4-3";

    [Header("主菜单背景音乐")]
    [Tooltip("进入主菜单时播放的背景音乐，由 AudioManager 统一管理")]
    [SerializeField] private AudioClip _menuBgm;

    [Header("加载过渡")]
    [Tooltip("点击开始游戏后显示的加载层（含切换后的背景与底部进度条）")]
    [SerializeField] private GameObject _loadingOverlay;
    [Tooltip("加载进度条填充图（Image 的 Type 需设为 Filled）")]
    [SerializeField] private Image _loadingFill;
    [Tooltip("进度条从 0 平滑爬满所需时长（秒），同时也是加载画面的最短展示时长")]
    [SerializeField] private float _fillDuration = 3f;

    [Header("调试")]
    [SerializeField] private bool _enableDebugLog = false;

    private Coroutine _backgroundRoutine;
    private Coroutine _loadingRoutine;
    private bool _isLoading;

    private void Start()
    {
        BindButtons();
        HideInstructions();
        PlayBackgroundLoop();
        PlayMenuBgm();
    }

    /// <summary>进入主菜单时播放背景音乐（交给全局 AudioManager 播放）</summary>
    private void PlayMenuBgm()
    {
        if (_menuBgm == null || AudioManager.Instance == null) return;
        AudioManager.Instance.PlayBGM(_menuBgm);
    }

    private void OnDestroy()
    {
        if (_backgroundRoutine != null)
            StopCoroutine(_backgroundRoutine);
        if (_loadingRoutine != null)
            StopCoroutine(_loadingRoutine);
    }

    // ==================== 背景轮播 ====================

    private void PlayBackgroundLoop()
    {
        if (_backgroundImages == null || _backgroundImages.Length == 0)
        {
            if (_enableDebugLog)
                Debug.Log("[MainMenuManager] 未配置背景图片，跳过背景轮播。");
            return;
        }

        // 初始化透明度：第一张直接显示，其余隐藏
        for (int i = 0; i < _backgroundImages.Length; i++)
        {
            if (_backgroundImages[i] == null) continue;
            SetImageAlpha(_backgroundImages[i], i == 0 ? 1f : 0f);
        }

        if (_autoPlay)
            _backgroundRoutine = StartCoroutine(BackgroundLoop());
    }

    private IEnumerator BackgroundLoop()
    {
        int current = 0;
        while (true)
        {
            int next = (current + 1) % _backgroundImages.Length;
            Image from = _backgroundImages[current];
            Image to = _backgroundImages[next];

            if (from == null || to == null)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            if (_crossfade)
            {
                // 交叉淡入淡出：旧图淡出的同时新图淡入
                yield return CrossFade(from, to);
            }
            else
            {
                // 顺序淡入淡出：先淡出旧图，再淡入新图
                yield return FadeImage(from, 1f, 0f, _fadeOutDuration);
                yield return FadeImage(to, 0f, 1f, _fadeInDuration);
            }

            if (_holdDuration > 0f)
                yield return new WaitForSeconds(_holdDuration);

            current = next;
        }
    }

    /// <summary>交叉淡入淡出：两张图同时过渡</summary>
    private IEnumerator CrossFade(Image from, Image to)
    {
        float duration = Mathf.Max(_fadeInDuration, _fadeOutDuration);
        if (duration <= 0f)
        {
            SetImageAlpha(from, 0f);
            SetImageAlpha(to, 1f);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;
            // 各自按自己的时长换算进度，保证淡入淡出先后关系正确
            float inK = Mathf.Clamp01(k * (duration / Mathf.Max(0.001f, _fadeInDuration)));
            float outK = Mathf.Clamp01(k * (duration / Mathf.Max(0.001f, _fadeOutDuration)));
            SetImageAlpha(to, Mathf.Lerp(0f, 1f, inK));
            SetImageAlpha(from, Mathf.Lerp(1f, 0f, outK));
            yield return null;
        }
        SetImageAlpha(to, 1f);
        SetImageAlpha(from, 0f);
    }

    /// <summary>单张图片透明度平滑插值</summary>
    private IEnumerator FadeImage(Image image, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetImageAlpha(image, to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetImageAlpha(image, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetImageAlpha(image, to);
    }

    private void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
    }

    // ==================== 按钮绑定 ====================

    private void BindButtons()
    {
        if (_startButton != null)
            _startButton.onClick.AddListener(OnStartGame);
        if (_howToButton != null)
            _howToButton.onClick.AddListener(OnShowInstructions);
        if (_quitButton != null)
            _quitButton.onClick.AddListener(OnQuitGame);
        if (_closeInstructionsButton != null)
            _closeInstructionsButton.onClick.AddListener(OnCloseInstructions);

        if (_enableDebugLog)
            Debug.Log("[MainMenuManager] 按钮事件绑定完成。");
    }

    // ==================== 按钮逻辑 ====================

    /// <summary>点击"开始游戏"：隐藏按钮、切换背景、进度条跑满后进入下一场景</summary>
    private void OnStartGame()
    {
        if (_isLoading) return; // 防止重复点击
        StartLoading();
    }

    /// <summary>开始加载过渡</summary>
    private void StartLoading()
    {
        _isLoading = true;

        if (_enableDebugLog)
            Debug.Log($"[MainMenuManager] 开始加载，目标场景 \"{_targetSceneName}\"");

        // 1. 隐藏所有菜单按钮（开始/说明/退出/设置），加载界面只留背景和进度条
        if (_startButton != null) _startButton.gameObject.SetActive(false);
        if (_howToButton != null) _howToButton.gameObject.SetActive(false);
        if (_quitButton != null) _quitButton.gameObject.SetActive(false);
        if (_settingsButton != null) _settingsButton.gameObject.SetActive(false);

        // 2. 停止背景轮播；显示加载层（含切换后的背景，位于 Canvas 最上层盖住原背景）
        StopBackgroundLoop();
        if (_loadingOverlay != null)
            _loadingOverlay.SetActive(true);

        // 3. 重置进度条并从 0 开始跑（强制 Filled 填充式，fillAmount 从 0 长到 1）
        if (_loadingFill != null)
        {
            _loadingFill.type = Image.Type.Filled;   // 确保是填充式
            _loadingFill.fillMethod = Image.FillMethod.Horizontal;
            _loadingFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _loadingFill.fillAmount = 0f;
        }

        _loadingRoutine = StartCoroutine(LoadingRoutine());
    }

    /// <summary>
    /// 真实异步加载：进度条反映 LoadSceneAsync 的真实加载进度，
    /// 并按 _fillDuration 秒平滑爬升（绝不超出真实进度），满格后才激活进入下一场景。
    /// </summary>
    private IEnumerator LoadingRoutine()
    {
        if (SceneChangerManager.Instance == null)
        {
            Debug.LogError("[MainMenuManager] SceneChangerManager 不存在，无法异步加载场景！");
            yield break;
        }

        float realProgress = 0f;

        // 交给全局 SceneChangerManager 异步加载（先不激活，等进度条满）
        bool started = SceneChangerManager.Instance.LoadSceneAsync(_targetSceneName, p => realProgress = p);
        if (!started)
        {
            Debug.LogError($"[MainMenuManager] 异步加载未能启动（目标场景 \"{_targetSceneName}\"），请检查 Build Settings。");
            yield break;
        }

        // 平滑爬升：显示进度按 _fillDuration 秒线性涨到满，但取 min，绝不超过真实加载进度
        float displayed = 0f;
        float t = 0f;
        while (displayed < 0.999f)
        {
            t += Time.deltaTime;
            displayed = Mathf.Min(t / Mathf.Max(0.001f, _fillDuration), Mathf.Clamp01(realProgress));

            if (_loadingFill != null)
                _loadingFill.fillAmount = displayed;
            yield return null;
        }

        // 进度条满 → 激活已加载好的场景，真正进入下一场景
        if (_loadingFill != null)
            _loadingFill.fillAmount = 1f;

        if (_enableDebugLog)
            Debug.Log($"[MainMenuManager] 进度条满，进入下一场景 -> \"{_targetSceneName}\"");

        SceneChangerManager.Instance.CompleteSceneLoad();
    }

    /// <summary>停止背景轮播协程</summary>
    private void StopBackgroundLoop()
    {
        if (_backgroundRoutine != null)
        {
            StopCoroutine(_backgroundRoutine);
            _backgroundRoutine = null;
        }
    }

    /// <summary>点击"游戏说明"：弹出说明面板，同时隐藏设置按钮</summary>
    private void OnShowInstructions()
    {
        if (_instructionsPanel != null)
            _instructionsPanel.SetActive(true);
        if (_settingsButton != null)
            _settingsButton.SetActive(false);
    }

    /// <summary>点击"关闭"：隐藏说明面板</summary>
    private void OnCloseInstructions()
    {
        HideInstructions();
    }

    private void HideInstructions()
    {
        if (_instructionsPanel != null)
            _instructionsPanel.SetActive(false);
        if (_settingsButton != null)
            _settingsButton.SetActive(true); // 说明关闭后恢复设置按钮
    }

    /// <summary>点击"退出游戏"：退出游戏</summary>
    private void OnQuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
