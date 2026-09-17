using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单设置面板 —— 右上角"设置"按钮弹出：
///   - 音乐音量 / 音效音量 两个拖动条（Slider）→ 实时写入全局 AudioManager；
///   - 音乐静音 / 音效静音 两个勾选框（Toggle）→ 静音后实际音量为 0，
///     但拖动条保留原数值，取消勾选后恢复。
///
/// 依赖：全局 AudioManager（PersistentSingleton）。
/// 用法：挂到场景里的 MenuSettingsPanel 物体上，在 Inspector 里把按钮、面板、滑块、勾选框拖进来。
/// </summary>
public class MenuSettingsPanel : MonoBehaviour
{
    [Header("设置按钮（右上角）")]
    [SerializeField] private Button _settingsButton;

    [Header("设置面板")]
    [Tooltip("设置弹窗根物体，默认隐藏")]
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private Button _closeButton;

    [Header("音乐音量")]
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Toggle _musicMuteToggle;

    [Header("音效音量")]
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Toggle _sfxMuteToggle;

    private void Start()
    {
        BindEvents();
        ClosePanel();
    }

    private void BindEvents()
    {
        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(OpenPanel);
        if (_closeButton != null)
            _closeButton.onClick.AddListener(ClosePanel);

        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[MenuSettingsPanel] 全局 AudioManager 不存在，音量设置不可用。");
            return;
        }

        // 音乐音量：先同步当前值（在挂监听之前，避免触发一次 SetMusicVolume），再绑定拖动事件
        if (_musicSlider != null)
        {
            _musicSlider.value = AudioManager.Instance.MusicVolume;
            _musicSlider.onValueChanged.AddListener(v => AudioManager.Instance.SetMusicVolume(v));
        }

        // 音效音量
        if (_sfxSlider != null)
        {
            _sfxSlider.value = AudioManager.Instance.SfxVolume;
            _sfxSlider.onValueChanged.AddListener(v => AudioManager.Instance.SetSfxVolume(v));
        }

        // 音乐静音：勾选 → 静音 + 勾标记出现；取消 → 恢复 + 勾标记消失
        if (_musicMuteToggle != null)
        {
            _musicMuteToggle.isOn = AudioManager.Instance.MusicMuted;
            _musicMuteToggle.onValueChanged.AddListener(m =>
            {
                AudioManager.Instance.SetMusicMuted(m);
                SetCheckmark(_musicMuteToggle.graphic, m);
            });
            // 校正初始勾标记（场景序列化的 isOn 与勾标记显隐可能不一致）
            SetCheckmark(_musicMuteToggle.graphic, _musicMuteToggle.isOn);
        }

        // 音效静音
        if (_sfxMuteToggle != null)
        {
            _sfxMuteToggle.isOn = AudioManager.Instance.SfxMuted;
            _sfxMuteToggle.onValueChanged.AddListener(m =>
            {
                AudioManager.Instance.SetSfxMuted(m);
                SetCheckmark(_sfxMuteToggle.graphic, m);
            });
            SetCheckmark(_sfxMuteToggle.graphic, _sfxMuteToggle.isOn);
        }
    }

    /// <summary>手动控制勾选标记显隐：show=true 显示勾，show=false 隐藏勾（可靠，不受 Toggle 自带淡入淡出影响）</summary>
    private void SetCheckmark(Graphic checkmark, bool show)
    {
        if (checkmark == null) return;
        checkmark.enabled = show;                       // 直接启用/禁用，勾必消必现
        checkmark.canvasRenderer.SetAlpha(show ? 1f : 0f); // 归正透明度，防残留
    }

    private void OpenPanel()
    {
        if (_settingsPanel != null)
            _settingsPanel.SetActive(true);
    }

    private void ClosePanel()
    {
        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);
    }
}
