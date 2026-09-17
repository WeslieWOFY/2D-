using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 关卡游戏状态管理器 —— 挂到每个关卡场景中的普通 MonoBehaviour（不常驻，随关卡卸载）。
///
/// 职责：
///   1. 序列化当前关卡的背景音乐（_levelBgm），在 OnEnable 时交给全局 AudioManager 播放；
///   2. 管理关卡游戏状态（Playing / Paused / GameOver），切换时发出事件；
///   3. 监听 BOSS 死亡（LevelEventBus "BossDied"），延迟 _endMenuDelay 秒后弹出结算菜单：
///      屏幕变暗 + 「再玩一次 / 回到主菜单 / 结束游戏」三个按钮。
///
/// 依赖：全局 AudioManager、SceneChangerManager（常驻）；关卡内 LevelEventBus。
/// 用法：
///   GameStateManager.Instance.PlayLevelBgm();      // 主动播放关卡音乐
///   GameStateManager.Instance.PauseGame();          // 暂停
///   GameStateManager.Instance.ResumeGame();         // 恢复
///   GameStateManager.Instance.SetGameOver();        // 游戏结束
/// </summary>
public class GameStateManager : MonoBehaviour
{
    /// <summary>关卡游戏状态</summary>
    public enum GameState
    {
        Playing,    // 正常游玩
        Paused,     // 暂停
        GameOver    // 游戏结束
    }

    // ==================== 单例（仅当前场景内） ====================

    public static GameStateManager Instance { get; private set; }

    [Header("关卡背景音乐")]
    [Tooltip("当前关卡的背景音乐，关卡场景加载时自动播放（OnEnable）")]
    [SerializeField] private AudioClip _levelBgm;

    [Header("游戏状态")]
    [SerializeField] private GameState _currentState = GameState.Playing;

    [Header("BOSS 死亡结算菜单")]
    [Tooltip("结算菜单根物体（含全屏变暗遮罩 + 三个按钮），默认隐藏")]
    [SerializeField] private GameObject _endMenuPanel;
    [SerializeField] private Button _playAgainButton;
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _quitButton;
    [Tooltip("BOSS 死亡后延迟弹菜单的秒数")]
    [SerializeField] private float _endMenuDelay = 2f;

    [Header("调试")]
    [SerializeField] private bool _enableDebugLog = false;

    /// <summary>状态切换事件（参数为切换后的状态）</summary>
    public event Action<GameState> OnStateChanged;

    public GameState CurrentState => _currentState;

    private const string BossDiedEvent = "BossDied";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameStateManager] 场景中存在多个游戏状态管理器，已用新的顶掉旧的。");
        }
        Instance = this;
    }

    private void OnEnable()
    {
        // 关卡加载时即播放当前关卡音乐（用户要求 OnEnable 时播放）
        PlayLevelBgm();
    }

    private void Start()
    {
        // OnEnable 时全局 AudioManager 可能尚未就绪（跨物体 Awake 顺序不定），这里补一次；
        // PlayBGM 对同一首有去重，重复调用无副作用。
        PlayLevelBgm();

        // 订阅 BOSS 死亡：在 Start 订阅保证 LevelEventBus 就绪
        LevelEventBus.On(BossDiedEvent, OnBossDied);
        BindEndMenu();
        if (_endMenuPanel != null)
            _endMenuPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            LevelEventBus.Off(BossDiedEvent, OnBossDied);
        }
    }

    // ==================== 背景音乐 ====================

    /// <summary>播放当前关卡背景音乐（交给全局 AudioManager 播放）</summary>
    public void PlayLevelBgm()
    {
        if (_levelBgm == null || AudioManager.Instance == null) return;

        AudioManager.Instance.PlayBGM(_levelBgm);

        if (_enableDebugLog)
            Debug.Log($"[GameStateManager] 开始播放关卡音乐：{_levelBgm.name}");
    }

    // ==================== 游戏状态 ====================

    /// <summary>切换关卡游戏状态并广播事件</summary>
    public void SetState(GameState newState)
    {
        if (_currentState == newState) return;

        _currentState = newState;
        OnStateChanged?.Invoke(_currentState);

        if (_enableDebugLog)
            Debug.Log($"[GameStateManager] 状态切换 -> {_currentState}");
    }

    public void PauseGame() => SetState(GameState.Paused);

    public void ResumeGame() => SetState(GameState.Playing);

    public void SetGameOver() => SetState(GameState.GameOver);

    // ==================== BOSS 死亡结算 ====================

    /// <summary>BOSS 阵亡：延迟 _endMenuDelay 秒后弹出结算菜单（屏幕变暗）</summary>
    private void OnBossDied()
    {
        StartCoroutine(ShowEndMenuAfterDelay());
    }

    private IEnumerator ShowEndMenuAfterDelay()
    {
        yield return new WaitForSeconds(_endMenuDelay);

        SetState(GameState.GameOver);

        // 全屏变暗遮罩 + 三个按钮
        if (_endMenuPanel != null)
            _endMenuPanel.SetActive(true);

        if (_enableDebugLog)
            Debug.Log("[GameStateManager] BOSS 阵亡，弹出结算菜单");
    }

    private void BindEndMenu()
    {
        if (_playAgainButton != null) _playAgainButton.onClick.AddListener(OnPlayAgain);
        if (_mainMenuButton != null) _mainMenuButton.onClick.AddListener(OnMainMenu);
        if (_quitButton != null) _quitButton.onClick.AddListener(OnQuit);
    }

    /// <summary>再玩一次：玩家血蓝直接回满，异步淡入淡出重载当前关卡</summary>
    private void OnPlayAgain()
    {
        // 血条/蓝条 UI 随场景重载会重新初始化；这里直接把玩家血量、蓝量拉满
        if (GameManager.Instance != null)
        {
            GameManager.Instance.FullyHealPlayer();
            GameManager.Instance.FullyRestoreMana();
        }

        if (_enableDebugLog)
            Debug.Log("[GameStateManager] 再玩一次，回满血蓝，异步淡入淡出重载当前关卡");

        string sceneName = SceneManager.GetActiveScene().name;

        // 优先走全局 SceneChangerManager 的异步 + 黑幕淡入淡出重载；缺失时兜底直接切
        if (SceneChangerManager.Instance != null)
            SceneChangerManager.Instance.LoadSceneAsyncFade(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }

    /// <summary>回到主菜单</summary>
    private void OnMainMenu()
    {
        if (_enableDebugLog)
            Debug.Log("[GameStateManager] 回到主菜单");

        LoadScene("MainMenu");
    }

    /// <summary>结束游戏</summary>
    private void OnQuit()
    {
        if (_enableDebugLog)
            Debug.Log("[GameStateManager] 结束游戏");

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>切场景：优先走全局 SceneChangerManager（统一切场景+校验），缺失时直接切</summary>
    private void LoadScene(string sceneName)
    {
        if (SceneChangerManager.Instance != null)
            SceneChangerManager.Instance.SwitchScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }
}
