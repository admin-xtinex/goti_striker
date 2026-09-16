using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using PitStriker.Gameplay;

namespace PitStriker.UI
{
    public partial class MenuManager
    {
        public ScreenType CurrentScreen { get; private set; }
        private ScreenType _rulesReturn = ScreenType.Home;
        private Image _screenBackdrop;
        private GameObject _confirmation;
        private Text _confirmTitle, _confirmBody;
        private Action _confirmedAction;
        private RectTransform _safeFrame;
        private GameObject _splashPanel;
        private CanvasGroup _splashPanelCanvasGroup;
        private CanvasGroup _splashLogoCanvasGroup;
        private RectTransform _splashLogoRect;
        private bool _skipSplash = false;
        private static bool _splashHasPlayed = false;
        private static readonly Color Ink = new Color(.055f, .13f, .13f, .97f);
        private static readonly Color Card = new Color(.10f, .21f, .21f, 1);
        private static readonly Color Gold = new Color(.78f, .46f, .13f, 1);
        private static readonly Color Green = new Color(.12f, .43f, .34f, 1);
        private static readonly Color Cream = new Color(.98f, .94f, .82f, 1);

        private void BuildModernUI()
        {
            var canvas = GetComponent<Canvas>();
            if (!canvas) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300; // Above HUD and its victory sub-canvas.
            var scaler = GetComponent<CanvasScaler>();
            if (!scaler) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1;
            if (!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();
            var events = FindAnyObjectByType<EventSystem>();
            if (!events) events = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var legacyInput = events.GetComponent<StandaloneInputModule>();
            if (legacyInput) { legacyInput.enabled = false; Destroy(legacyInput); }
            if (!events.GetComponent<InputSystemUIInputModule>()) events.gameObject.AddComponent<InputSystemUIInputModule>();

            // Replace serialized legacy screens as well as incomplete runtime menus.
            foreach (Transform child in transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var hud = FindAnyObjectByType<HUDManager>(FindObjectsInactive.Include);
            _hudRoot = hud ? hud.gameObject : null;
            if (_hudRoot)
                foreach (var t in _hudRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name == "TopBar_Navigation" || t.name == "Btn_HUD_Pause") t.gameObject.SetActive(false);

            _screenBackdrop = CreatePanel("MenuBackdrop", transform, new Color(.02f,.07f,.07f,.80f)).GetComponent<Image>();
            _safeFrame = new GameObject("SafeMenuFrame", typeof(RectTransform)).GetComponent<RectTransform>();
            _safeFrame.SetParent(transform, false);
            _safeFrame.sizeDelta = new Vector2(1080, 640);

                        _homePanel = Page("Home");
            Label(_homePanel.transform, "VILLAGE MARBLES", -240, 216, 430, 32, 18, Gold);
            Label(_homePanel.transform, "GOTI STRIKER", -240, 145, 460, 84, 52, Cream);
            Label(_homePanel.transform, "A little aim. A clever strike.", -240, 65, 460, 42, 24, Cream);
            Label(_homePanel.transform, "Play against the computer or pass the phone\nto friends. Two to four players, one village lane.", -240, 8, 460, 70, 19, new Color(.72f,.83f,.80f));
            var coursePill = Box("CoursePill", _homePanel.transform, -240, -82, 460, 72, Card);
            Label(coursePill.transform, "CURRENT COURSE", 0, 16, 430, 22, 13, Gold);
            _homeCourseText = Label(coursePill.transform, "Village Lane   -   3 Pits   -   Par 6", 0, -10, 430, 28, 19, Cream);
            Label(_homePanel.transform, "LOCAL PLAY   -   2-4 PLAYERS", -240, -230, 460, 32, 16, new Color(.72f,.83f,.80f));
            var homeCard = Box("PlayCard", _homePanel.transform, 278, 0, 430, 530, Card);
            Label(homeCard.transform, "MAIN MENU", 0, 220, 370, 36, 20, Gold);
            _homePlayButton = ActionButton("Btn_Play", homeCard.transform, "PLAY", 0, 142, 356, 76, Gold);
            _homeMapsButton = ActionButton("Btn_Maps", homeCard.transform, "MAPS", 0, 62, 356, 60, Green);
            _homeRulesButton = ActionButton("Btn_Rules", homeCard.transform, "HOW TO PLAY", 0, -12, 356, 60, Card);
            _homeSettingsButton = ActionButton("Btn_Settings", homeCard.transform, "SETTINGS", 0, -86, 356, 60, Card);
            _homeExitButton = ActionButton("Btn_Exit", homeCard.transform, "EXIT", 0, -164, 356, 52, Ink);

            _choosePlayersPanel = Page("MatchSetup");
            Label(_choosePlayersPanel.transform, "MATCH SETUP", 0, 272, 850, 44, 30, Cream);
            Label(_choosePlayersPanel.transform, "Configure course, participants, and controller assignments.", 0, 238, 940, 26, 17, new Color(.72f,.83f,.80f));
            var courseBanner = Box("CourseBanner", _choosePlayersPanel.transform, 0, 192, 760, 52, Card);
            Box("CourseAccent", courseBanner.transform, -376, 0, 8, 52, Gold);
            _matchCourseNameText = Label(courseBanner.transform, "MAP   -   VILLAGE LANE", -90, 8, 480, 24, 18, Gold);
            _matchCourseSpecsText = Label(courseBanner.transform, "3 Pits   -   Par 6   -   Earth Track", -90, -12, 480, 20, 14, Cream);
            _btnChangeCourse = ActionButton("Btn_ChangeCourse", courseBanner.transform, "CHANGE COURSE >", 260, 0, 200, 40, Green);
            _btn2Players = ActionButton("Btn_2P", _choosePlayersPanel.transform, "2 players", -252, 130, 230, 48, Card);
            _btn3Players = ActionButton("Btn_3P", _choosePlayersPanel.transform, "3 players", 0, 130, 230, 48, Card);
            _btn4Players = ActionButton("Btn_4P", _choosePlayersPanel.transform, "4 players", 252, 130, 230, 48, Card);
            _img2Players = _btn2Players.image; _img3Players = _btn3Players.image; _img4Players = _btn4Players.image;
            _playerSlotRows = new GameObject[4]; _playerSlotNameTexts = new Text[4];
            _playerSlotRoleTexts = new Text[4]; _playerSlotToggleButtons = new Button[4];
            Color[] colors = { new Color(.15f,.65f,1), new Color(1,.3f,.25f), new Color(.2f,.8f,.4f), new Color(1,.75f,.2f) };
            for (int i = 0; i < 4; i++)
            {
                var row = Box("Slot_P" + (i+1), _choosePlayersPanel.transform, 0, 66-i*52, 760, 46, Card);
                Box("MarbleColor", row.transform, -350, 0, 10, 28, colors[i]);
                _playerSlotNameTexts[i] = Label(row.transform, "Player " + (i+1), -185, 0, 260, 36, 20, Cream);
                var button = ActionButton("ToggleRoleBtn", row.transform, "COMPUTER  >", 210, 0, 280, 38, Green);
                button.interactable = i != 0;
                _playerSlotRows[i] = row;
                _playerSlotToggleButtons[i] = button;
                _playerSlotRoleTexts[i] = button.GetComponentInChildren<Text>();
            }
            _backToHomeButton = ActionButton("Btn_BackToHome", _choosePlayersPanel.transform, "Back", -252, -238, 230, 58, Card);
            _startMatchButton = ActionButton("Btn_StartMatch", _choosePlayersPanel.transform, "START MATCH", 130, -238, 476, 58, Gold);

            BuildMapsAndSettingsPages();

_rulesModal = Page("HowToPlay");
            Label(_rulesModal.transform, "How to play", 0, 255, 850, 60, 36, Cream);
            string[] headings = { "01   Aim & strike", "02   Win the toss", "03   Reach the pits", "04   Earn another shot" };
            string[] bodies = {
                "Swipe to aim and launch your marble.\nUse the power control for a measured strike.",
                "Throw towards Pit 3. The closest marble\ngets the first turn in the match.",
                "Sink your marble in order: Pit 1, then 2,\nthen 3. First to finish wins.",
                "Sink your target pit or hit another marble\nfor a bonus shot. Up to three shots per turn." };
            for (int i=0; i<4; i++)
            {
                float x = i%2 == 0 ? -252 : 252, y = i<2 ? 112 : -68;
                var card = Box("Rule"+i, _rulesModal.transform, x, y, 474, 156, Card);
                Label(card.transform, headings[i], 0, 40, 430, 42, 25, Cream);
                Label(card.transform, bodies[i], 0, -24, 430, 70, 20, Cream);
            }
            _closeRulesButton = ActionButton("Btn_CloseRules", _rulesModal.transform, "Back", 0, -252, 320, 64, Gold);

            _pauseModal = Page("Pause");
            Label(_pauseModal.transform, "Match paused", 0, 232, 800, 64, 40, Cream);
            Label(_pauseModal.transform, "Take your time. Your turn will be here.", 0, 175, 850, 40, 22, Cream);
            _pauseResumeButton = ActionButton("Btn_Resume", _pauseModal.transform, "RESUME MATCH", 0, 86, 450, 72, Gold);
            _pauseRestartButton = ActionButton("Btn_Restart", _pauseModal.transform, "Restart match", 0, -2, 450, 64, Green);
            var rules = ActionButton("Btn_PauseRules", _pauseModal.transform, "How to play", 0, -82, 450, 64, Card);
            rules.onClick.AddListener(OpenRules);
            _pauseHomeButton = ActionButton("Btn_HomeMenu", _pauseModal.transform, "Main menu", 0, -162, 450, 64, Card);
            _hudPauseButton = BuildHudTopRightButtons();

            _confirmation = Page("ConfirmAction");
            _confirmation.GetComponent<Image>().color = new Color(.025f,.07f,.07f,1);
            _confirmTitle = Label(_confirmation.transform, "Leave this match?", 0, 116, 950, 70, 36, Cream);
            _confirmBody = Label(_confirmation.transform, "", 0, 16, 850, 96, 24, Cream);
            var btnCancel = ActionButton("Btn_Cancel", _confirmation.transform, "Cancel", -140, -110, 240, 64, Card);
            btnCancel.onClick.AddListener(CancelConfirmation);
            var btnConfirm = ActionButton("Btn_Confirm", _confirmation.transform, "Confirm", 140, -110, 240, 64, Gold);
            btnConfirm.onClick.AddListener(() => { var a = _confirmedAction; CancelConfirmation(); a?.Invoke(); });
            _confirmation.SetActive(false);

            // Top-level XTINEX Splash Screen overlay
            _splashPanel = CreatePanel("XTINEX_SplashScreen", transform, Color.black);
            var splashRect = _splashPanel.GetComponent<RectTransform>();
            splashRect.anchorMin = Vector2.zero;
            splashRect.anchorMax = Vector2.one;
            splashRect.sizeDelta = Vector2.zero;
            splashRect.anchoredPosition = Vector2.zero;

            _splashPanelCanvasGroup = _splashPanel.AddComponent<CanvasGroup>();
            _splashPanelCanvasGroup.alpha = 1f; // Solid pitch black covering scene on boot

            var logoObj = new GameObject("XTINEX_Logo");
            logoObj.transform.SetParent(_splashPanel.transform, false);
            _splashLogoRect = logoObj.AddComponent<RectTransform>();
            _splashLogoRect.anchorMin = Vector2.zero;
            _splashLogoRect.anchorMax = Vector2.one;
            _splashLogoRect.sizeDelta = Vector2.zero;
            _splashLogoRect.anchoredPosition = Vector2.zero;

            _splashLogoCanvasGroup = logoObj.AddComponent<CanvasGroup>();
            _splashLogoCanvasGroup.alpha = 0f; // Logo starts hidden on pitch black background

            var logoImg = logoObj.AddComponent<Image>();
            var sprite = Resources.Load<Sprite>("UI/XTINEX_Splash_Screen_16x9");
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>("UI/XTINEX_Splash_Screen_16x9");
                if (tex != null)
                {
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            if (sprite != null)
            {
                logoImg.sprite = sprite;
            }
            logoImg.preserveAspect = true;
            logoImg.raycastTarget = false;

            var skipBtn = _splashPanel.AddComponent<Button>();
            var skipNav = skipBtn.navigation;
            skipNav.mode = Navigation.Mode.None;
            skipBtn.navigation = skipNav;
            skipBtn.onClick.AddListener(() => { _skipSplash = true; });

            BuildOnlineUI();
            _splashPanel.transform.SetAsLastSibling();

            FitSafeArea();
        }

        /// <summary>
        /// In-game top-right controls: a PAUSE pill with an icon and a round settings button.
        /// Anchored to the screen corner inside the safe area (not the centred menu frame). The
        /// pill is the same pause button as before - MenuManager wires and shows it as ever - and
        /// the gear, a child so it shows and hides with it, opens Settings from the pause menu so
        /// "Back" returns there. Units: this canvas's 1280x720 reference.
        /// </summary>
        private Button BuildHudTopRightButtons()
        {
            var corner = HudArt.Rect("HUD_TopRight", transform);
            corner.anchorMin = Vector2.zero; corner.anchorMax = Vector2.one;
            corner.offsetMin = corner.offsetMax = Vector2.zero;
            corner.gameObject.AddComponent<SafeAreaFitter>();

            var pillBorder = new Color(.55f, .76f, .96f, .95f);
            var pill = HudArt.Image("Btn_HUD_Pause", corner, HudArt.RoundedRect(24), new Color(.05f, .10f, .20f, .88f), sliced: true);
            pill.raycastTarget = true;
            HudArt.Place(pill.rectTransform, new Vector2(1f, 1f), new Vector2(-164f, -52f), new Vector2(146f, 54f));
            var pillGlow = HudArt.Image("Glow", pill.transform, HudArt.RoundedRect(24), new Color(.35f, .65f, 1f, .18f), sliced: true);
            pillGlow.rectTransform.anchorMin = Vector2.zero; pillGlow.rectTransform.anchorMax = Vector2.one;
            pillGlow.rectTransform.offsetMin = new Vector2(-4f, -4f); pillGlow.rectTransform.offsetMax = new Vector2(4f, 4f);
            pillGlow.transform.SetAsFirstSibling();
            var outline = HudArt.Image("Outline", pill.transform, HudArt.RoundedRect(24, 2.5f), pillBorder, sliced: true);
            outline.rectTransform.anchorMin = Vector2.zero; outline.rectTransform.anchorMax = Vector2.one;
            outline.rectTransform.offsetMin = outline.rectTransform.offsetMax = Vector2.zero;

            var iconRing = HudArt.Image("IconRing", pill.transform, HudArt.Ring(7f), new Color(.75f, .87f, 1f, .9f));
            HudArt.Place(iconRing.rectTransform, new Vector2(0f, .5f), new Vector2(27f, 0f), new Vector2(36f, 36f));
            var iconFill = HudArt.Image("IconFill", pill.transform, HudArt.Circle(), new Color(.12f, .20f, .34f, 1f));
            HudArt.Place(iconFill.rectTransform, new Vector2(0f, .5f), new Vector2(27f, 0f), new Vector2(31f, 31f));
            var bars = HudArt.Image("IconBars", pill.transform, HudArt.PauseIcon(), Color.white);
            HudArt.Place(bars.rectTransform, new Vector2(0f, .5f), new Vector2(27f, 0f), new Vector2(24f, 24f));
            var label = HudArt.Text("Label", pill.transform, "PAUSE", 21, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            HudArt.Place(label.rectTransform, new Vector2(0f, .5f), new Vector2(96f, 0f), new Vector2(92f, 40f));
            HudArt.Shadow(label, .35f, 1.5f);

            var pause = pill.gameObject.AddComponent<Button>();
            pause.targetGraphic = pill;
            var nav = pause.navigation; nav.mode = Navigation.Mode.None; pause.navigation = nav;

            // Settings, 24 units right of the pill, 20 from the screen edge.
            var gear = HudArt.Image("Btn_HUD_Settings", pill.transform, HudArt.Circle(), new Color(.15f, .24f, .37f, .92f));
            gear.raycastTarget = true;
            HudArt.Place(gear.rectTransform, new Vector2(.5f, .5f), new Vector2(119f, 0f), new Vector2(50f, 50f));
            var gearRing = HudArt.Image("Ring", gear.transform, HudArt.Ring(5f), new Color(.60f, .76f, .95f, .75f));
            HudArt.Place(gearRing.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(50f, 50f));
            var gearIcon = HudArt.Image("Icon", gear.transform, HudArt.Gear(), Color.white);
            HudArt.Place(gearIcon.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(30f, 30f));
            var settings = gear.gameObject.AddComponent<Button>();
            settings.targetGraphic = gear;
            var gnav = settings.navigation; gnav.mode = Navigation.Mode.None; settings.navigation = gnav;
            settings.onClick.AddListener(() => { HandlePauseClicked(); OpenSettings(); });

            return pause;
        }

        private GameObject Page(string name)
        {
            return Box(name, _safeFrame, 0, 0, 1080, 640, Ink);
        }
        private static GameObject Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var obj = CreatePanel(name, parent, color);
            Position(obj, x,y,w,h);
            return obj;
        }
        private static void Position(GameObject obj, float x, float y, float w, float h)
        {
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
            rect.sizeDelta = new Vector2(w,h); rect.anchoredPosition = new Vector2(x,y);
        }
        private static Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color)
        {
            var obj = CreateText("Label", parent, text, size, FontStyle.Normal, color, TextAnchor.MiddleCenter);
            Position(obj,x,y,w,h);
            var t = obj.GetComponent<Text>(); t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }
        private static Text MultilineLabel(Transform parent, string text, float x, float y, float w, float h, int size, Color color, TextAnchor anchor)
        {
            var obj = CreateText("MultilineLabel", parent, text, size, FontStyle.Normal, color, anchor);
            Position(obj, x, y, w, h);
            var t = obj.GetComponent<Text>();
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        private static Button ActionButton(string name, Transform parent, string label, float x, float y, float w, float h, Color color)
        {
            var obj = CreateButton(name,parent,label,color,22);
            Position(obj,x,y,w,h);
            var shadow = obj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0,0,0,.22f); shadow.effectDistance = new Vector2(0,-3);
            var button = obj.GetComponent<Button>();
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            return button;
        }
        private void FitSafeArea()
        {
            if (!_safeFrame || Screen.width == 0 || Screen.height == 0) return;
            var canvas = GetComponent<Canvas>();
            var safe = Screen.safeArea;
            float scale = Mathf.Max(.01f,canvas.scaleFactor);
            _safeFrame.anchoredPosition = (safe.center - new Vector2(Screen.width,Screen.height)*.5f)/scale;
            _safeFrame.localScale = Vector3.one * Mathf.Min((safe.width/scale-32)/1080f,(safe.height/scale-24)/640f);
        }
        private void Update()
        {
            FitSafeArea();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) NavigateBack();
        }

        private void BuildMapsAndSettingsPages()
        {
            _mapsPanel = Page("Maps");
            Label(_mapsPanel.transform, "COURSE SELECTION", 0, 270, 850, 46, 32, Cream);
            Label(_mapsPanel.transform, "Select an available course to play or preview upcoming tracks.", 0, 234, 940, 26, 16, new Color(.72f,.83f,.80f));

            var card1 = Box("MapCard_Village", _mapsPanel.transform, -366, 10, 232, 345, Card);
            Box("Accent1", card1.transform, 0, 170, 232, 4, Gold);
            Label(card1.transform, "MAP 01", 0, 140, 210, 22, 13, Gold);
            Label(card1.transform, "VILLAGE LANE", 0, 114, 210, 28, 19, Cream);
            Label(card1.transform, "[ AVAILABLE ]", 0, 86, 210, 22, 13, Green);
            Label(card1.transform, "3 Pits   -   Par 6\nEarth Track", 0, 50, 210, 34, 14, Cream);
            MultilineLabel(card1.transform, "Village fairway with earth embankments, palms, and stone boundary walls.", 0, -16, 204, 76, 13, new Color(.75f,.86f,.84f), TextAnchor.MiddleCenter);
            _btnSelectMapVillage = ActionButton("Btn_SelectMap_village_lane", card1.transform, "PLAY COURSE", 0, -125, 200, 48, Gold);

            var card2 = Box("MapCard_Coastal", _mapsPanel.transform, -122, 10, 232, 345, new Color(.08f,.17f,.17f,1));
            Box("Accent2", card2.transform, 0, 170, 232, 4, new Color(.35f,.75f,.95f,1));
            Label(card2.transform, "MAP 02", 0, 140, 210, 22, 13, new Color(.35f,.75f,.95f,1));
            Label(card2.transform, "SUNSET COASTAL", 0, 114, 210, 28, 18, Cream);
            Label(card2.transform, "[ COMING SOON ]", 0, 86, 210, 22, 13, new Color(.85f,.65f,.25f,1));
            Label(card2.transform, "3 Pits   -   Par 6\nSand Verge", 0, 50, 210, 34, 14, new Color(.65f,.75f,.72f));
            MultilineLabel(card2.transform, "Sunset boardwalk fairway with sandy verges and a coastal boundary rail.", 0, -16, 204, 76, 13, new Color(.55f,.68f,.65f), TextAnchor.MiddleCenter);
            var btnCoastal = ActionButton("Btn_SelectMap_sunset_coastal", card2.transform, "COMING SOON", 0, -125, 200, 48, Ink);
            btnCoastal.interactable = false;

            var card3 = Box("MapCard_Temple", _mapsPanel.transform, 122, 10, 232, 345, new Color(.08f,.17f,.17f,1));
            Box("Accent3", card3.transform, 0, 170, 232, 4, new Color(.85f,.45f,.85f,1));
            Label(card3.transform, "MAP 03", 0, 140, 210, 22, 13, new Color(.85f,.45f,.85f,1));
            Label(card3.transform, "TEMPLE COURTYARD", 0, 114, 210, 28, 18, Cream);
            Label(card3.transform, "[ COMING SOON ]", 0, 86, 210, 22, 13, new Color(.85f,.65f,.25f,1));
            Label(card3.transform, "3 Pits   -   Par 6\nFlagstone", 0, 50, 210, 34, 14, new Color(.65f,.75f,.72f));
            MultilineLabel(card3.transform, "Ancient flagstone courtyard with carved pillars and bank-shot walls.", 0, -16, 204, 76, 13, new Color(.55f,.68f,.65f), TextAnchor.MiddleCenter);
            var btnTemple = ActionButton("Btn_SelectMap_temple_courtyard", card3.transform, "COMING SOON", 0, -125, 200, 48, Ink);
            btnTemple.interactable = false;

            var card4 = Box("MapCard_Quarry", _mapsPanel.transform, 366, 10, 232, 345, new Color(.08f,.17f,.17f,1));
            Box("Accent4", card4.transform, 0, 170, 232, 4, new Color(.95f,.55f,.35f,1));
            Label(card4.transform, "MAP 04", 0, 140, 210, 22, 13, new Color(.95f,.55f,.35f,1));
            Label(card4.transform, "MOUNTAIN QUARRY", 0, 114, 210, 28, 18, Cream);
            Label(card4.transform, "[ COMING SOON ]", 0, 86, 210, 22, 13, new Color(.85f,.65f,.25f,1));
            Label(card4.transform, "4 Pits   -   Par 8\nQuarry Stone", 0, 50, 210, 34, 14, new Color(.65f,.75f,.72f));
            MultilineLabel(card4.transform, "Highland quarry course with elevation drops and rocky obstacles.", 0, -16, 204, 76, 13, new Color(.55f,.68f,.65f), TextAnchor.MiddleCenter);
            var btnQuarry = ActionButton("Btn_SelectMap_mountain_quarry", card4.transform, "COMING SOON", 0, -125, 200, 48, Ink);
            btnQuarry.interactable = false;

            _closeMapsButton = ActionButton("Btn_CloseMaps", _mapsPanel.transform, "Back to Menu", 0, -242, 320, 56, Gold);

            _settingsPanel = Page("Settings");
            Label(_settingsPanel.transform, "SETTINGS", 0, 266, 850, 48, 34, Cream);
            Label(_settingsPanel.transform, "Audio balances, engine performance, and preferences", 0, 230, 940, 28, 17, new Color(.72f,.83f,.80f));
            _tabAudioBtn = ActionButton("Btn_Tab_Audio", _settingsPanel.transform, "AUDIO", -340, 186, 156, 42, Gold);
            _tabGameplayBtn = ActionButton("Btn_Tab_Gameplay", _settingsPanel.transform, "GAMEPLAY", -170, 186, 156, 42, Card);
            _tabDisplayBtn = ActionButton("Btn_Tab_Display", _settingsPanel.transform, "DISPLAY", 0, 186, 156, 42, Card);
            _tabAccessibilityBtn = ActionButton("Btn_Tab_Accessibility", _settingsPanel.transform, "ACCESS", 170, 186, 156, 42, Card);
            _tabOtherBtn = ActionButton("Btn_Tab_Other", _settingsPanel.transform, "MORE", 340, 186, 156, 42, Card);

            var settingsCard = Box("SettingsContainer", _settingsPanel.transform, 0, -20, 860, 334, Card);

            _audioSettingsPanel = Box("AudioSettingsPanel", settingsCard.transform, 0, 0, 860, 334, new Color(0,0,0,0));
            Label(_audioSettingsPanel.transform, "MASTER VOLUME", -180, 114, 320, 26, 18, Cream);
            _masterVolumeText = Label(_audioSettingsPanel.transform, "100%", 260, 114, 100, 26, 18, Gold);
            _masterVolumeSlider = CreateSlider("Slider_Master", _audioSettingsPanel.transform, 0, 84, 660, 26);
            Label(_audioSettingsPanel.transform, "MUSIC VOLUME", -180, 46, 320, 26, 18, Cream);
            _musicVolumeText = Label(_audioSettingsPanel.transform, "100%", 260, 46, 100, 26, 18, Gold);
            _musicVolumeSlider = CreateSlider("Slider_Music", _audioSettingsPanel.transform, 0, 16, 660, 26);
            Label(_audioSettingsPanel.transform, "SOUND EFFECTS (SFX)", -180, -22, 320, 26, 18, Cream);
            _sfxVolumeText = Label(_audioSettingsPanel.transform, "100%", 260, -22, 100, 26, 18, Gold);
            _sfxVolumeSlider = CreateSlider("Slider_SFX", _audioSettingsPanel.transform, 0, -52, 660, 26);
            _resetAudioBtn = ActionButton("Btn_ResetAudio", _audioSettingsPanel.transform, "RESET AUDIO DEFAULTS", 0, -114, 320, 42, Ink);

            _gameplaySettingsPanel = Box("GameplaySettingsPanel", settingsCard.transform, 0, 0, 860, 334, new Color(0,0,0,0));
            var vibCard = Box("VibCard", _gameplaySettingsPanel.transform, 0, 60, 760, 94, Ink);
            MultilineLabel(vibCard.transform, "HAPTIC FEEDBACK / VIBRATION", -120, 20, 480, 26, 18, Cream, TextAnchor.MiddleLeft);
            MultilineLabel(vibCard.transform, "Tactile vibration on marble strikes, bank collisions, and pocketing.", -120, -15, 480, 40, 14, new Color(.62f,.75f,.72f), TextAnchor.MiddleLeft);
            _hapticsToggleButton = ActionButton("Btn_Haptics", vibCard.transform, "VIBRATION: ENABLED", 260, 0, 185, 52, Green);
            _hapticsToggleText = _hapticsToggleButton.GetComponentInChildren<Text>();
            // Difficulty replaces the old fixed "AIM TRAJECTORY ASSIST" readout. The aim guide is
            // now one of the things this choice drives, alongside launch strength and how sharp
            // the bots are. Offline only — online always plays at Hard.
            var aimCard = Box("AimCard", _gameplaySettingsPanel.transform, 0, -55, 760, 94, Ink);
            MultilineLabel(aimCard.transform, "DIFFICULTY", -250, 26, 220, 26, 18, Cream, TextAnchor.MiddleLeft);
            _difficultyDescText = MultilineLabel(aimCard.transform,
                GameDifficulty.Description(GameDifficulty.CurrentMode) + "  Offline only; online always plays Hard.",
                -250, -12, 360, 44, 13, new Color(.62f,.75f,.72f), TextAnchor.MiddleLeft);

            _btnDiffEasy = ActionButton("Btn_DiffEasy", aimCard.transform, "EASY", 110, 0, 118, 52, Card);
            _btnDiffHard = ActionButton("Btn_DiffHard", aimCard.transform, "HARD", 238, 0, 118, 52, Card);
            _btnDiffPro  = ActionButton("Btn_DiffPro",  aimCard.transform, "PRO",  366, 0, 118, 52, Card);

            _btnDiffEasy.onClick.AddListener(() => SetDifficulty(DifficultyMode.Easy));
            _btnDiffHard.onClick.AddListener(() => SetDifficulty(DifficultyMode.Hard));
            _btnDiffPro.onClick.AddListener(() => SetDifficulty(DifficultyMode.Pro));
            RefreshDifficultyButtons();

            _gameplaySettingsPanel.SetActive(false);

            _displaySettingsPanel = Box("DisplaySettingsPanel", settingsCard.transform, 0, 0, 860, 334, new Color(0,0,0,0));
            Label(_displaySettingsPanel.transform, "GRAPHICS FIDELITY & PERFORMANCE PRESET", 0, 118, 640, 26, 18, Cream);
            _btnQualityLow = ActionButton("Btn_QualityLow", _displaySettingsPanel.transform, "PERFORMANCE\n30 FPS", -245, 52, 225, 68, Card);
            _btnQualityMed = ActionButton("Btn_QualityMed", _displaySettingsPanel.transform, "BALANCED\n60 FPS", 0, 52, 225, 68, Green);
            _btnQualityHigh = ActionButton("Btn_QualityHigh", _displaySettingsPanel.transform, "HIGH FIDELITY\n60 FPS MAX", 245, 52, 225, 68, Card);
            var statsBox = Box("StatsBox", _displaySettingsPanel.transform, 0, -42, 760, 74, Ink);
            _displayInfoText = MultilineLabel(statsBox.transform, "TARGET: 60 FPS   -   Balanced Mode\nDEVICE: Unity Universal Render Pipeline", 0, 0, 720, 64, 14, Cream, TextAnchor.MiddleCenter);
            Label(_displaySettingsPanel.transform, "Changes take effect instantly.", 0, -114, 760, 26, 14, new Color(.62f,.75f,.72f));
            _displaySettingsPanel.SetActive(false);

            _accessibilitySettingsPanel = Box("AccessSettingsPanel", settingsCard.transform, 0, 0, 860, 334, new Color(0,0,0,0));
            var accessBox = Box("AccessBox", _accessibilitySettingsPanel.transform, 0, 0, 760, 270, Ink);
            Label(accessBox.transform, "ACCESSIBILITY (UPCOMING ROADMAP)", 0, 95, 700, 30, 20, Gold);
            MultilineLabel(accessBox.transform, " -  High-contrast marble outlines\n -  Scalable HUD elements\n -  Colorblind assistance palettes\n -  Distinct haptic cues for pit vs foul\n\nReady for future updates without menu restructuring.", 0, -20, 700, 180, 15, Cream, TextAnchor.UpperLeft);
            _accessibilitySettingsPanel.SetActive(false);

            _otherSettingsPanel = Box("OtherSettingsPanel", settingsCard.transform, 0, 0, 860, 334, new Color(0,0,0,0));
            var langBox = Box("LangBox", _otherSettingsPanel.transform, 0, 40, 760, 48, Ink);
            Label(langBox.transform, "LANGUAGE: ENGLISH", 0, 0, 600, 30, 16, Cream);
            var studioBox = Box("StudioBox", _otherSettingsPanel.transform, 0, -40, 760, 68, Ink);
            Label(studioBox.transform, "Pit Striker v1.0.0   -   Studio xtinex", 0, 12, 710, 24, 15, Gold);
            Label(studioBox.transform, "Local + online play   -   Physics predictions run on device", 0, -14, 710, 24, 13, new Color(.62f,.75f,.72f));
            _otherSettingsPanel.SetActive(false);

            _closeSettingsButton = ActionButton("Btn_CloseSettings", _settingsPanel.transform, "Back", 0, -252, 320, 64, Gold);
        }

        private static Slider CreateSlider(string name, Transform parent, float x, float y, float w, float h)
        {
            GameObject sliderObj = new GameObject(name, typeof(RectTransform));
            sliderObj.transform.SetParent(parent, false);
            Position(sliderObj, x, y, w, h);

            GameObject bgObj = CreateImage("Background", sliderObj.transform, new Color(0.04f, 0.10f, 0.10f, 1f));
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.32f);
            bgRect.anchorMax = new Vector2(1, 0.68f);
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.32f);
            fillAreaRect.anchorMax = new Vector2(1, 0.68f);
            fillAreaRect.sizeDelta = new Vector2(-16, 0);
            fillAreaRect.anchoredPosition = Vector2.zero;

            GameObject fill = CreateImage("Fill", fillArea.transform, Gold);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.sizeDelta = Vector2.zero;

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.sizeDelta = new Vector2(-24, 0);
            handleAreaRect.anchoredPosition = Vector2.zero;

            GameObject handle = CreateImage("Handle", handleArea.transform, Cream);
            handle.GetComponent<Image>().raycastTarget = true;
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(28, 28);
            var shadow = handle.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.35f);
            shadow.effectDistance = new Vector2(0, -2);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            var nav = slider.navigation;
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            return slider;
        }

        public void NavigateBack()
        {
            if (_confirmation && _confirmation.activeSelf) { CancelConfirmation(); return; }
            switch (CurrentScreen)
            {
                case ScreenType.InGame: HandlePauseClicked(); break;
                case ScreenType.Pause: HandleResumeClicked(); break;
                case ScreenType.Rules: ShowScreen(_rulesReturn); break;
                case ScreenType.ChoosePlayers: ShowScreen(ScreenType.PlayModeSelect); break;
                case ScreenType.PlayModeSelect: ShowScreen(ScreenType.Home); break;
                case ScreenType.Maps: ShowScreen(ScreenType.Home); break;
                case ScreenType.Settings: ShowScreen(_settingsReturn); break;
                case ScreenType.Home: Confirm("Exit game?", "You can start a new match whenever you return.", HandleExitClicked); break;
            }
        }
        private void OpenRules()
        {
            _rulesReturn = CurrentScreen == ScreenType.Pause ? ScreenType.Pause : ScreenType.Home;
            ShowScreen(ScreenType.Rules);
        }
        private void Confirm(string title, string body, Action action)
        {
            _confirmedAction = action; _confirmTitle.text = title; _confirmBody.text = body;
            _confirmation.SetActive(true); _confirmation.transform.SetAsLastSibling();
        }
        private void CancelConfirmation() { _confirmation.SetActive(false); _confirmedAction = null; }
        private void OnApplicationPause(bool paused) { if (paused && CurrentScreen == ScreenType.InGame) HandlePauseClicked(); }
        private void OnApplicationFocus(bool focused) { if (!focused && CurrentScreen == ScreenType.InGame) HandlePauseClicked(); }
        private void OnDestroy() { if (Instance == this) { Instance = null; Time.timeScale = 1; } }
        private void SaveSetup()
        {
            if (Application.isBatchMode) return; // Automated checks must not replace the user's setup.
            PlayerPrefs.SetInt("UI.PlayerCount",_selectedPlayerCount);
            for(int i=1;i<4;i++) PlayerPrefs.SetInt("UI.Bot"+i,_isAISlot[i]?1:0);
            PlayerPrefs.Save();
        }
        private void LoadSetup()
        {
            _selectedPlayerCount = Mathf.Clamp(PlayerPrefs.GetInt("UI.PlayerCount",2),2,4);
            _isAISlot[0] = false;
            for(int i=1;i<4;i++) _isAISlot[i] = PlayerPrefs.GetInt("UI.Bot"+i,1)==1;
        }

        public IEnumerator PlaySplashScreenRoutine()
        {
            if (_splashPanel == null) yield break;

            // In automated test runs or if already displayed, bypass animation
            if (Application.isBatchMode || _splashHasPlayed || System.IO.File.Exists("Library/MenuFlowChecks.running"))
            {
                _splashPanel.SetActive(false);
                if (PitStriker.Audio.AudioManager.Instance != null)
                {
                    PitStriker.Audio.AudioManager.Instance.PlayStartMusic();
                }
                yield break;
            }

            _splashHasPlayed = true;
            _splashPanel.SetActive(true);
            _splashPanel.transform.SetAsLastSibling();

            // Background remains 100% solid pitch black covering all 3D scene & UI elements
            if (_splashPanelCanvasGroup != null) _splashPanelCanvasGroup.alpha = 1f;
            // Logo begins hidden
            if (_splashLogoCanvasGroup != null) _splashLogoCanvasGroup.alpha = 0f;
            if (_splashLogoRect != null) _splashLogoRect.localScale = new Vector3(0.92f, 0.92f, 1f);

            _skipSplash = false;

            // Phase 1: Smooth Fade In of XTINEX Logo onto black backdrop & subtle scale-in (0.85s)
            float elapsed = 0f;
            float fadeInDuration = 0.85f;
            while (elapsed < fadeInDuration && !_skipSplash)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                if (_splashLogoCanvasGroup != null) _splashLogoCanvasGroup.alpha = smoothT;
                if (_splashLogoRect != null) _splashLogoRect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.00f, smoothT);
                yield return null;
            }

            if (_splashLogoCanvasGroup != null) _splashLogoCanvasGroup.alpha = 1f;

            // Phase 2: Ambient Cinematic Hold with gentle volumetric expansion (1.5s)
            elapsed = 0f;
            float holdDuration = 1.5f;
            while (elapsed < holdDuration && !_skipSplash)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / holdDuration);
                if (_splashLogoRect != null) _splashLogoRect.localScale = Vector3.one * Mathf.Lerp(1.00f, 1.04f, t);
                yield return null;
            }

            // Phase 3: Fade Out the entire black splash overlay to reveal the Home Menu and Game for the first time (0.75s, or 0.25s on skip)
            if (PitStriker.Audio.AudioManager.Instance != null)
            {
                PitStriker.Audio.AudioManager.Instance.PlayStartMusic(fade: true);
            }

            elapsed = 0f;
            float fadeOutDuration = _skipSplash ? 0.25f : 0.75f;
            float startAlpha = _splashPanelCanvasGroup != null ? _splashPanelCanvasGroup.alpha : 1f;
            Vector3 startScale = _splashLogoRect != null ? _splashLogoRect.localScale : Vector3.one;
            Vector3 endScale = startScale * 1.03f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                if (_splashPanelCanvasGroup != null) _splashPanelCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, smoothT);
                if (_splashLogoRect != null) _splashLogoRect.localScale = Vector3.Lerp(startScale, endScale, smoothT);
                yield return null;
            }

            _splashPanel.SetActive(false);
        }
    }
}
