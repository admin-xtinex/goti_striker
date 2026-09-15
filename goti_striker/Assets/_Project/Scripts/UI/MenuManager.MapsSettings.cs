using UnityEngine;
using UnityEngine.UI;
using PitStriker.Gameplay;   // GameDifficulty / DifficultyMode for the difficulty picker

namespace PitStriker.UI
{
    /// <summary>
    /// DEV-2: MAPS + SETTINGS screens rebuilt from reference stills/video on f3b021a.
    /// No MapManager/SettingsManager (those live outside the allowed pre-f3b021a window).
    /// Persistence via PlayerPrefs; music volume via AudioManager when present.
    /// </summary>
    public partial class MenuManager
    {
        private enum SettingsCategory { Audio, Gameplay, Display, Accessibility, Other }

        [SerializeField] private GameObject _mapsPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private Button _homeMapsButton;
        [SerializeField] private Button _homeSettingsButton;
        [SerializeField] private Button _closeMapsButton;
        [SerializeField] private Button _closeSettingsButton;
        [SerializeField] private Button _btnSelectMapVillage;
        [SerializeField] private Button _btnChangeCourse;
        [SerializeField] private Text _homeCourseText;
        [SerializeField] private Text _matchCourseNameText;
        [SerializeField] private Text _matchCourseSpecsText;

        [SerializeField] private GameObject _audioSettingsPanel;
        [SerializeField] private GameObject _gameplaySettingsPanel;
        [SerializeField] private GameObject _displaySettingsPanel;
        [SerializeField] private GameObject _accessibilitySettingsPanel;
        [SerializeField] private GameObject _otherSettingsPanel;
        [SerializeField] private Button _tabAudioBtn;
        [SerializeField] private Button _tabGameplayBtn;
        [SerializeField] private Button _tabDisplayBtn;
        [SerializeField] private Button _tabAccessibilityBtn;
        [SerializeField] private Button _tabOtherBtn;
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;
        [SerializeField] private Text _masterVolumeText;
        [SerializeField] private Text _musicVolumeText;
        [SerializeField] private Text _sfxVolumeText;
        [SerializeField] private Button _resetAudioBtn;
        [SerializeField] private Button _hapticsToggleButton;
        [SerializeField] private Text _hapticsToggleText;
        [SerializeField] private Button _btnQualityLow;
        [SerializeField] private Button _btnQualityMed;
        [SerializeField] private Button _btnQualityHigh;
        [SerializeField] private Text _displayInfoText;

        // Difficulty picker (Gameplay tab) — replaces the old static aim-assist readout.
        [SerializeField] private Button _btnDiffEasy;
        [SerializeField] private Button _btnDiffHard;
        [SerializeField] private Button _btnDiffPro;
        [SerializeField] private Text _difficultyDescText;

        private ScreenType _settingsReturn = ScreenType.Home;
        private SettingsCategory _activeSettingsCategory = SettingsCategory.Audio;
        private string _selectedCourseId = "village_lane";
        private string _selectedCourseName = "Village Lane";
        private const string PrefCourseId = "UI.SelectedCourseId";
        private const string PrefCourseName = "UI.SelectedCourseName";
        private const string PrefMaster = "Settings.MasterVolume";
        private const string PrefMusic = "Settings.MusicVolume";
        private const string PrefSfx = "Settings.SfxVolume";
        private const string PrefHaptics = "Settings.Haptics";
        private const string PrefQuality = "Settings.GraphicsQuality";

        public void OpenMaps()
        {
            ShowScreen(ScreenType.Maps);
        }

        public void OpenSettings()
        {
            _settingsReturn = CurrentScreen == ScreenType.Pause ? ScreenType.Pause : ScreenType.Home;
            SwitchSettingsCategory(SettingsCategory.Audio);
            ShowScreen(ScreenType.Settings);
        }

        private void SwitchSettingsCategory(SettingsCategory category)
        {
            _activeSettingsCategory = category;
            if (_audioSettingsPanel != null) _audioSettingsPanel.SetActive(category == SettingsCategory.Audio);
            if (_gameplaySettingsPanel != null) _gameplaySettingsPanel.SetActive(category == SettingsCategory.Gameplay);
            if (_displaySettingsPanel != null) _displaySettingsPanel.SetActive(category == SettingsCategory.Display);
            if (_accessibilitySettingsPanel != null) _accessibilitySettingsPanel.SetActive(category == SettingsCategory.Accessibility);
            if (_otherSettingsPanel != null) _otherSettingsPanel.SetActive(category == SettingsCategory.Other);
            SetTabColor(_tabAudioBtn, category == SettingsCategory.Audio);
            SetTabColor(_tabGameplayBtn, category == SettingsCategory.Gameplay);
            SetTabColor(_tabDisplayBtn, category == SettingsCategory.Display);
            SetTabColor(_tabAccessibilityBtn, category == SettingsCategory.Accessibility);
            SetTabColor(_tabOtherBtn, category == SettingsCategory.Other);
            UpdateSettingsUI();
        }

        private void SetTabColor(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = active ? Gold : Card;
        }

        /// <summary>
        /// Applies a difficulty choice. Offline only — an online match ignores this and plays at
        /// Hard, so the setting is stored but has no effect until the player returns to offline.
        /// </summary>
        private void SetDifficulty(DifficultyMode mode)
        {
            GameDifficulty.CurrentMode = mode;   // persisted by the config itself
            RefreshDifficultyButtons();
        }

        private void RefreshDifficultyButtons()
        {
            DifficultyMode current = GameDifficulty.CurrentMode;
            SetTabColor(_btnDiffEasy, current == DifficultyMode.Easy);
            SetTabColor(_btnDiffHard, current == DifficultyMode.Hard);
            SetTabColor(_btnDiffPro, current == DifficultyMode.Pro);

            if (_difficultyDescText != null)
            {
                _difficultyDescText.text = GameDifficulty.Description(current)
                                         + "  Offline only; online always plays Hard.";
            }
        }

        private void BindMapsSettingsButtons()
        {
            if (_homeMapsButton != null)
            {
                _homeMapsButton.onClick.RemoveAllListeners();
                _homeMapsButton.onClick.AddListener(OpenMaps);
            }
            if (_homeSettingsButton != null)
            {
                _homeSettingsButton.onClick.RemoveAllListeners();
                _homeSettingsButton.onClick.AddListener(OpenSettings);
            }
            if (_closeMapsButton != null)
            {
                _closeMapsButton.onClick.RemoveAllListeners();
                _closeMapsButton.onClick.AddListener(() => ShowScreen(ScreenType.Home));
            }
            if (_closeSettingsButton != null)
            {
                _closeSettingsButton.onClick.RemoveAllListeners();
                _closeSettingsButton.onClick.AddListener(() => ShowScreen(_settingsReturn));
            }
            if (_btnSelectMapVillage != null)
            {
                _btnSelectMapVillage.onClick.RemoveAllListeners();
                _btnSelectMapVillage.onClick.AddListener(() => SelectCourse("village_lane", "Village Lane"));
            }
            if (_btnChangeCourse != null)
            {
                _btnChangeCourse.onClick.RemoveAllListeners();
                _btnChangeCourse.onClick.AddListener(OpenMaps);
            }
            if (_tabAudioBtn != null) { _tabAudioBtn.onClick.RemoveAllListeners(); _tabAudioBtn.onClick.AddListener(() => SwitchSettingsCategory(SettingsCategory.Audio)); }
            if (_tabGameplayBtn != null) { _tabGameplayBtn.onClick.RemoveAllListeners(); _tabGameplayBtn.onClick.AddListener(() => SwitchSettingsCategory(SettingsCategory.Gameplay)); }
            if (_tabDisplayBtn != null) { _tabDisplayBtn.onClick.RemoveAllListeners(); _tabDisplayBtn.onClick.AddListener(() => SwitchSettingsCategory(SettingsCategory.Display)); }
            if (_tabAccessibilityBtn != null) { _tabAccessibilityBtn.onClick.RemoveAllListeners(); _tabAccessibilityBtn.onClick.AddListener(() => SwitchSettingsCategory(SettingsCategory.Accessibility)); }
            if (_tabOtherBtn != null) { _tabOtherBtn.onClick.RemoveAllListeners(); _tabOtherBtn.onClick.AddListener(() => SwitchSettingsCategory(SettingsCategory.Other)); }

            if (_masterVolumeSlider != null) { _masterVolumeSlider.onValueChanged.RemoveAllListeners(); _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged); }
            if (_musicVolumeSlider != null) { _musicVolumeSlider.onValueChanged.RemoveAllListeners(); _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged); }
            if (_sfxVolumeSlider != null) { _sfxVolumeSlider.onValueChanged.RemoveAllListeners(); _sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged); }
            if (_resetAudioBtn != null) { _resetAudioBtn.onClick.RemoveAllListeners(); _resetAudioBtn.onClick.AddListener(ResetAudioDefaults); }
            if (_hapticsToggleButton != null) { _hapticsToggleButton.onClick.RemoveAllListeners(); _hapticsToggleButton.onClick.AddListener(ToggleHaptics); }
            if (_btnQualityLow != null) { _btnQualityLow.onClick.RemoveAllListeners(); _btnQualityLow.onClick.AddListener(() => SetGraphicsQuality(0)); }
            if (_btnQualityMed != null) { _btnQualityMed.onClick.RemoveAllListeners(); _btnQualityMed.onClick.AddListener(() => SetGraphicsQuality(1)); }
            if (_btnQualityHigh != null) { _btnQualityHigh.onClick.RemoveAllListeners(); _btnQualityHigh.onClick.AddListener(() => SetGraphicsQuality(2)); }
        }

        private void LoadCoursePrefs()
        {
            _selectedCourseId = PlayerPrefs.GetString(PrefCourseId, "village_lane");
            _selectedCourseName = PlayerPrefs.GetString(PrefCourseName, "Village Lane");
            RefreshCourseLabels();
        }

        private void SelectCourse(string id, string displayName)
        {
            _selectedCourseId = id;
            _selectedCourseName = displayName;
            PlayerPrefs.SetString(PrefCourseId, id);
            PlayerPrefs.SetString(PrefCourseName, displayName);
            PlayerPrefs.Save();
            RefreshCourseLabels();
            ShowScreen(ScreenType.Home);
        }

        private void RefreshCourseLabels()
        {
            string pill = $"{_selectedCourseName} - 3 Pits - Par 6";
            if (_homeCourseText != null) _homeCourseText.text = pill;
            if (_matchCourseNameText != null) _matchCourseNameText.text = $"MAP - {_selectedCourseName.ToUpperInvariant()}";
            if (_matchCourseSpecsText != null) _matchCourseSpecsText.text = "3 Pits - Par 6 - Earth Track";
        }

        private void UpdateSettingsUI()
        {
            float master = PlayerPrefs.GetFloat(PrefMaster, 1f);
            float music = PlayerPrefs.GetFloat(PrefMusic, 0.5f);
            float sfx = PlayerPrefs.GetFloat(PrefSfx, 1f);
            if (_masterVolumeSlider != null) _masterVolumeSlider.SetValueWithoutNotify(master);
            if (_musicVolumeSlider != null) _musicVolumeSlider.SetValueWithoutNotify(music);
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.SetValueWithoutNotify(sfx);
            if (_masterVolumeText != null) _masterVolumeText.text = $"{Mathf.RoundToInt(master * 100f)}%";
            if (_musicVolumeText != null) _musicVolumeText.text = $"{Mathf.RoundToInt(music * 100f)}%";
            if (_sfxVolumeText != null) _sfxVolumeText.text = $"{Mathf.RoundToInt(sfx * 100f)}%";

            bool haptics = PlayerPrefs.GetInt(PrefHaptics, 1) == 1;
            if (_hapticsToggleText != null) _hapticsToggleText.text = haptics ? "VIBRATION: ENABLED" : "VIBRATION: OFF";
            if (_hapticsToggleButton != null)
            {
                var img = _hapticsToggleButton.GetComponent<Image>();
                if (img != null) img.color = haptics ? Green : Card;
            }

            int q = PlayerPrefs.GetInt(PrefQuality, 1);
            SetTabColor(_btnQualityLow, q == 0);
            SetTabColor(_btnQualityMed, q == 1);
            SetTabColor(_btnQualityHigh, q == 2);
            if (_displayInfoText != null)
            {
                string mode = q == 0 ? "Performance 30 FPS" : q == 2 ? "High Fidelity 60 FPS" : "Balanced 60 FPS";
                _displayInfoText.text = $"TARGET: {mode}\nDEVICE: Unity Universal Render Pipeline";
            }
        }

        private void OnMasterVolumeChanged(float value)
        {
            PlayerPrefs.SetFloat(PrefMaster, value);
            PlayerPrefs.Save();
            AudioListener.volume = value;
            if (_masterVolumeText != null) _masterVolumeText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        private void OnMusicVolumeChanged(float value)
        {
            PlayerPrefs.SetFloat(PrefMusic, value);
            PlayerPrefs.Save();
            if (PitStriker.Audio.AudioManager.Instance != null)
                PitStriker.Audio.AudioManager.Instance.MusicVolume = value;
            if (_musicVolumeText != null) _musicVolumeText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        private void OnSfxVolumeChanged(float value)
        {
            PlayerPrefs.SetFloat(PrefSfx, value);
            PlayerPrefs.Save();
            if (_sfxVolumeText != null) _sfxVolumeText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        private void ResetAudioDefaults()
        {
            OnMasterVolumeChanged(1f);
            OnMusicVolumeChanged(0.5f);
            OnSfxVolumeChanged(1f);
            UpdateSettingsUI();
        }

        private void ToggleHaptics()
        {
            bool next = PlayerPrefs.GetInt(PrefHaptics, 1) != 1;
            PlayerPrefs.SetInt(PrefHaptics, next ? 1 : 0);
            PlayerPrefs.Save();
            UpdateSettingsUI();
        }

        private void SetGraphicsQuality(int level)
        {
            level = Mathf.Clamp(level, 0, 2);
            PlayerPrefs.SetInt(PrefQuality, level);
            PlayerPrefs.Save();
            QualitySettings.SetQualityLevel(Mathf.Clamp(level, 0, QualitySettings.names.Length - 1), true);
            Application.targetFrameRate = level == 0 ? 30 : 60;
            UpdateSettingsUI();
        }

        private void ApplyPersistedAudioOnBoot()
        {
            float master = PlayerPrefs.GetFloat(PrefMaster, 1f);
            float music = PlayerPrefs.GetFloat(PrefMusic, 0.5f);
            AudioListener.volume = master;
            if (PitStriker.Audio.AudioManager.Instance != null)
                PitStriker.Audio.AudioManager.Instance.MusicVolume = music;
        }
    }
}

