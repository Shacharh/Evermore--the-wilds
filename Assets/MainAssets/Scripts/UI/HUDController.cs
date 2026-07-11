using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds and drives the battle HUD entirely via UI Toolkit.
///
/// Layout:
///   Top-left     — MENU button
///   Bottom-right — circular AP ring + END TURN button (side by side)
///   Left edge    — vertical roster panel: player monsters
///   Right edge   — vertical roster panel: enemy monsters
/// </summary>
public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [Header("References (auto-found if empty)")]
    [SerializeField] private PlayerTurnController playerTurnController;

    // ── UI Elements ───────────────────────────────────────────────────────────
    private CircularProgress _apCircle;
    private Button           _endTurnBtn;
    private VisualElement    _playerRoster;
    private VisualElement    _enemyRoster;
    private bool             _eventsWired;
    private bool             _apInitialized;

    // Live monster cards: monster → the card's HP-fill element + label
    private readonly Dictionary<Monster, MonsterCard> _cards = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        Monster.OnRosterChanged += PopulateRosters;
    }

    private void Start()
    {
        var oldCanvas = GetComponentInParent<Canvas>();
        if (oldCanvas != null) oldCanvas.enabled = false;

        BuildUI();
        SetEndTurnInteractable(false);
        StartCoroutine(RetryConnect());
        StartCoroutine(RetryPopulateRosters());
        StartCoroutine(PollStatuses());
    }

    private void LateUpdate()
    {
        if (_eventsWired && playerTurnController == null)
        {
            _eventsWired = false;
            _apInitialized = false;
            StartCoroutine(RetryConnect());
            return;
        }
        if (!_eventsWired || playerTurnController == null) return;

        bool shouldBeActive = playerTurnController.IsActive;
        if (_endTurnBtn != null && _endTurnBtn.enabledSelf != shouldBeActive)
            SetEndTurnInteractable(shouldBeActive);

        if (shouldBeActive && _apCircle != null && !_apInitialized)
        {
            _apInitialized = true;
            _apCircle.SetValue(playerTurnController.CurrentAP, playerTurnController.MaxAP, true);
        }
    }

    private void OnDestroy()
    {
        Monster.OnRosterChanged -= PopulateRosters;
        if (playerTurnController == null) return;
        playerTurnController.onAPChanged.RemoveListener(OnAPChanged);
        playerTurnController.onTurnStart.RemoveListener(OnPlayerTurnStart);
        playerTurnController.onTurnEnd  .RemoveListener(OnPlayerTurnEnd);

        foreach (var (monster, card) in _cards)
            if (monster != null) monster.OnHPChanged -= card.OnHPChanged;
    }

    // ── Connection ────────────────────────────────────────────────────────────

    private IEnumerator RetryConnect()
    {
        while (!_eventsWired)
        {
            TryConnectController();
            yield return new WaitForSeconds(0.25f);
        }
    }

    private void TryConnectController()
    {
        if (playerTurnController == null)
            playerTurnController = FindFirstObjectByType<PlayerTurnController>();
        if (playerTurnController == null || _eventsWired) return;
        _eventsWired = true;

        playerTurnController.onAPChanged.AddListener(OnAPChanged);
        playerTurnController.onTurnStart.AddListener(OnPlayerTurnStart);
        playerTurnController.onTurnEnd  .AddListener(OnPlayerTurnEnd);
        if (_endTurnBtn != null)
            _endTurnBtn.clicked += playerTurnController.OnEndTurnButtonPressed;

        if (playerTurnController.IsActive)
        {
            _apInitialized = true;
            _apCircle?.SetValue(playerTurnController.CurrentAP, playerTurnController.MaxAP, true);
            SetEndTurnInteractable(true);
        }
    }

    // ── AP Callbacks ──────────────────────────────────────────────────────────

    private void OnAPChanged(int newAP)
    {
        _apInitialized = true;
        _apCircle?.SetValue(newAP, playerTurnController.MaxAP, true);
    }

    private void OnPlayerTurnStart() => SetEndTurnInteractable(true);
    private void OnPlayerTurnEnd()   => SetEndTurnInteractable(false);

    private void SetEndTurnInteractable(bool on)
    {
        if (_endTurnBtn != null) _endTurnBtn.SetEnabled(on);
    }

    // Refresh status condition icons every half-second (statuses only change on turn boundaries)
    private IEnumerator PollStatuses()
    {
        var wait = new WaitForSeconds(0.5f);
        while (true)
        {
            foreach (var (_, card) in _cards)
                card.RefreshStatuses();
            yield return wait;
        }
    }

    // ── Roster Population ─────────────────────────────────────────────────────

    private IEnumerator RetryPopulateRosters()
    {
        // Wait until at least one monster is alive in the scene
        Monster[] all = null;
        while (all == null || all.Length == 0)
        {
            all = FindObjectsByType<Monster>(FindObjectsSortMode.None);
            yield return new WaitForSeconds(0.3f);
        }

        // Give spawner one more frame to finish setting IsEnemy flags
        yield return null;
        PopulateRosters();
    }

    public static void RefreshRosters()
        => Instance?.PopulateRosters();

    private void PopulateRosters()
    {
        _playerRoster?.Clear();
        _enemyRoster?.Clear();
        _cards.Clear();

        var all = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        foreach (var monster in all)
        {
            if (!monster.IsAlive) continue;
            var panel = monster.IsEnemy ? _enemyRoster : _playerRoster;
            if (panel == null) continue;

            var card = new MonsterCard(monster);
            _cards[monster] = card;
            monster.OnHPChanged += card.OnHPChanged;
            panel.Add(card.Root);
        }
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s   = UIStyleConfig.Load();
        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = s?.panelSettings;
        doc.sortingOrder  = 100;

        var root = doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;

        // ── Menu button (top-left) ────────────────────────────────────────
        var menuBtn = new Button(() => PauseMenu.Instance?.Toggle());
        menuBtn.text = "MENU";
        menuBtn.style.position = Position.Absolute;
        menuBtn.style.left = 24; menuBtn.style.top = 24;
        menuBtn.style.width = 160; menuBtn.style.height = 50;
        StyleHUDButton(menuBtn, s?.buttonSprite, new Color(0.12f, 0.12f, 0.20f, 1f));
        root.Add(menuBtn);

        // ── Bottom-right: circular AP + End Turn button ───────────────────
        var bottomRight = new VisualElement();
        bottomRight.pickingMode = PickingMode.Ignore;
        bottomRight.style.position      = Position.Absolute;
        bottomRight.style.right         = 24;
        bottomRight.style.bottom        = 24;
        bottomRight.style.flexDirection = FlexDirection.Row;
        bottomRight.style.alignItems    = Align.Center;

        // Circular AP ring
        _apCircle = new CircularProgress();
        _apCircle.pickingMode = PickingMode.Ignore;
        _apCircle.style.width  = 84;
        _apCircle.style.height = 84;
        _apCircle.style.marginRight = 12;
        _apCircle.SetValue(0, 0, false);

        // End Turn button
        _endTurnBtn = new Button();
        _endTurnBtn.text = "END TURN";
        _endTurnBtn.style.width  = 200;
        _endTurnBtn.style.height = 54;
        StyleHUDButton(_endTurnBtn, s?.buttonSprite, new Color(0.12f, 0.12f, 0.20f, 1f));

        bottomRight.Add(_apCircle);
        bottomRight.Add(_endTurnBtn);
        root.Add(bottomRight);

        // ── Left roster panel (player monsters) ──────────────────────────
        _playerRoster = BuildRosterContainer(isLeft: true);
        root.Add(_playerRoster);

        // ── Right roster panel (enemy monsters) ──────────────────────────
        _enemyRoster = BuildRosterContainer(isLeft: false);
        root.Add(_enemyRoster);
    }

    private static VisualElement BuildRosterContainer(bool isLeft)
    {
        var panel = new VisualElement();
        panel.pickingMode = PickingMode.Ignore;
        panel.style.position      = Position.Absolute;
        panel.style.top           = 80;
        panel.style.bottom        = 420;
        if (isLeft)  panel.style.left  = 12;
        else         panel.style.right = 12;
        panel.style.flexDirection  = FlexDirection.Column;
        panel.style.justifyContent = Justify.Center;
        return panel;
    }

    private static void StyleHUDButton(Button btn, Sprite sprite, Color fallback)
    {
        btn.style.fontSize                = 20;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.color                   = Color.white;
        btn.style.borderTopWidth          = 0; btn.style.borderBottomWidth = 0;
        btn.style.borderLeftWidth         = 0; btn.style.borderRightWidth  = 0;
        btn.style.borderTopLeftRadius     = 0; btn.style.borderTopRightRadius     = 0;
        btn.style.borderBottomLeftRadius  = 0; btn.style.borderBottomRightRadius  = 0;
        UIStyleConfig.ApplySprite(btn, sprite, fallback);
    }

    // ── Monster Card ──────────────────────────────────────────────────────────

    private class MonsterCard
    {
        public VisualElement Root { get; }

        private readonly Monster _monster;
        private VisualElement    _hpFill;
        private Label            _hpLabel;
        private VisualElement    _statusRow;

        private static readonly Color PlayerTeam = new Color(0.25f, 0.85f, 1f,   1f);
        private static readonly Color EnemyTeam  = new Color(1f,   0.30f, 0.30f, 1f);
        private static readonly Color HPFull     = new Color(0.20f, 0.85f, 0.35f, 1f);
        private static readonly Color HPLow      = new Color(0.90f, 0.25f, 0.25f, 1f);

        public MonsterCard(Monster monster)
        {
            _monster = monster;
            Root = Build();
        }

        private VisualElement Build()
        {
            var data    = _monster.Data;
            bool enemy  = _monster.IsEnemy;
            var  accent = enemy ? EnemyTeam : PlayerTeam;

            // ── Card outer ────────────────────────────────────────────────
            var card = new VisualElement();
            card.pickingMode = PickingMode.Ignore;
            card.style.width  = 120;
            card.style.marginBottom = 8;
            card.style.borderTopLeftRadius    = 6;
            card.style.borderTopRightRadius   = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.overflow = Overflow.Hidden;
            // Tint the whole card with the team colour
            var cardBg = enemy
                ? new Color(0.25f, 0.05f, 0.05f, 0.92f)   // red tint — enemy
                : new Color(0.04f, 0.18f, 0.28f, 0.92f);  // cyan tint — ally
            card.style.backgroundColor = cardBg;
            // Solid accent border on the outer edge
            card.style.borderTopColor    = accent;
            card.style.borderTopWidth    = 2;
            card.style.borderBottomColor = accent;
            card.style.borderBottomWidth = 2;
            if (enemy) { card.style.borderRightColor = accent; card.style.borderRightWidth = 3; }
            else       { card.style.borderLeftColor  = accent; card.style.borderLeftWidth  = 3; }

            // ── Portrait ──────────────────────────────────────────────────
            var portrait = new VisualElement();
            portrait.pickingMode = PickingMode.Ignore;
            portrait.style.height = 80;
            portrait.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            if (data?.portrait != null)
                portrait.style.backgroundImage = new StyleBackground(data.portrait);
            else
                portrait.style.backgroundColor = new Color(0.12f, 0.12f, 0.18f, 1f);

            // Type icon — top-right corner of portrait
            var typeLib = TypeIconLibrary.Instance;
            if (typeLib != null && data != null)
            {
                var typeSprite = typeLib.GetIcon(data.elementType);
                if (typeSprite != null)
                {
                    var typeIcon = new VisualElement();
                    typeIcon.pickingMode = PickingMode.Ignore;
                    typeIcon.style.position = Position.Absolute;
                    typeIcon.style.top = 4; typeIcon.style.right = 4;
                    typeIcon.style.width = 22; typeIcon.style.height = 22;
                    typeIcon.style.backgroundImage = new StyleBackground(typeSprite);
                    portrait.Add(typeIcon);
                }
            }

            card.Add(portrait);

            // ── Info strip ───────────────────────────────────────────────
            var info = new VisualElement();
            info.pickingMode = PickingMode.Ignore;
            info.style.paddingLeft = 6; info.style.paddingRight = 6;
            info.style.paddingTop = 4;  info.style.paddingBottom = 4;

            // Name + level row
            var nameRow = new VisualElement();
            nameRow.pickingMode = PickingMode.Ignore;
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.justifyContent = Justify.SpaceBetween;
            nameRow.style.marginBottom = 3;

            var nameLabel = new Label(data?.displayName ?? _monster.gameObject.name);
            nameLabel.pickingMode = PickingMode.Ignore;
            nameLabel.style.fontSize = 11;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = Color.white;

            var lvLabel = new Label($"Lv.{_monster.Level}");
            lvLabel.pickingMode = PickingMode.Ignore;
            lvLabel.style.fontSize = 10;
            lvLabel.style.color = new Color(0.75f, 0.75f, 0.75f, 1f);

            nameRow.Add(nameLabel);
            nameRow.Add(lvLabel);
            info.Add(nameRow);

            // HP bar track
            var hpTrack = new VisualElement();
            hpTrack.pickingMode = PickingMode.Ignore;
            hpTrack.style.height = 6;
            hpTrack.style.backgroundColor = new Color(0.15f, 0.15f, 0.20f, 1f);
            hpTrack.style.borderTopLeftRadius    = 3;
            hpTrack.style.borderTopRightRadius   = 3;
            hpTrack.style.borderBottomLeftRadius = 3;
            hpTrack.style.borderBottomRightRadius = 3;
            hpTrack.style.overflow = Overflow.Hidden;
            hpTrack.style.marginBottom = 3;

            _hpFill = new VisualElement();
            _hpFill.pickingMode = PickingMode.Ignore;
            _hpFill.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
            _hpFill.style.width  = new StyleLength(new Length(100f, LengthUnit.Percent));
            _hpFill.style.backgroundColor = HPFull;
            hpTrack.Add(_hpFill);
            info.Add(hpTrack);

            // HP numbers
            _hpLabel = new Label($"{_monster.CurrentHP} / {_monster.MaxHP}");
            _hpLabel.pickingMode = PickingMode.Ignore;
            _hpLabel.style.fontSize = 9;
            _hpLabel.style.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            _hpLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _hpLabel.style.marginBottom = 3;
            info.Add(_hpLabel);

            // Status icons row
            _statusRow = new VisualElement();
            _statusRow.pickingMode = PickingMode.Ignore;
            _statusRow.style.flexDirection = FlexDirection.Row;
            _statusRow.style.flexWrap = Wrap.Wrap;
            info.Add(_statusRow);

            card.Add(info);

            RefreshHP(_monster.CurrentHP, _monster.MaxHP);
            RefreshStatuses();

            return card;
        }

        public void OnHPChanged(int current, int max)
        {
            RefreshHP(current, max);

            // Grey out card if dead
            if (current <= 0)
                Root.style.opacity = 0.4f;
        }

        private void RefreshHP(int current, int max)
        {
            if (_hpFill == null || _hpLabel == null) return;
            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            _hpFill.style.width = new StyleLength(new Length(ratio * 100f, LengthUnit.Percent));
            _hpFill.style.backgroundColor = Color.Lerp(HPLow, HPFull, ratio);
            _hpLabel.text = $"{current} / {max}";
        }

        // Called from HUD's coroutine poll to refresh status icons
        public void RefreshStatuses()
        {
            if (_statusRow == null || _monster == null) return;
            _statusRow.Clear();

            foreach (var status in _monster.ActiveStatuses)
            {
                if (status.data == null) continue;
                var icon = new VisualElement();
                icon.pickingMode = PickingMode.Ignore;
                icon.style.width  = 16; icon.style.height = 16;
                icon.style.marginRight = 2;
                icon.style.backgroundColor = StatusColor(status.data.ID);
                icon.style.borderTopLeftRadius    = 3;
                icon.style.borderTopRightRadius   = 3;
                icon.style.borderBottomLeftRadius = 3;
                icon.style.borderBottomRightRadius = 3;

                // If you have status sprites in the future, set backgroundImage here.
                // For now, colour-coded squares.
                var lbl = new Label(StatusInitial(status.data.ID));
                lbl.pickingMode = PickingMode.Ignore;
                lbl.style.fontSize = 8;
                lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                lbl.style.color = Color.white;
                lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                lbl.style.position = Position.Absolute;
                lbl.style.left = 0; lbl.style.right = 0;
                lbl.style.top = 0;  lbl.style.bottom = 0;
                icon.Add(lbl);

                _statusRow.Add(icon);
            }
        }

        private static Color StatusColor(AttackEnum.StatusEffect id)
        {
            return id switch
            {
                AttackEnum.StatusEffect.Freeze => new Color(0.3f, 0.7f, 1f),
                AttackEnum.StatusEffect.Shock  => new Color(1f, 0.9f, 0.1f),
                AttackEnum.StatusEffect.Burn   => new Color(1f, 0.4f, 0.1f),
                AttackEnum.StatusEffect.Poison => new Color(0.5f, 0.9f, 0.3f),
                AttackEnum.StatusEffect.Sleep  => new Color(0.6f, 0.5f, 0.9f),
                _                              => new Color(0.6f, 0.3f, 0.8f),
            };
        }

        private static string StatusInitial(AttackEnum.StatusEffect id)
        {
            return id switch
            {
                AttackEnum.StatusEffect.Freeze => "Fr",
                AttackEnum.StatusEffect.Shock  => "Sh",
                AttackEnum.StatusEffect.Burn   => "Br",
                AttackEnum.StatusEffect.Poison => "Po",
                AttackEnum.StatusEffect.Sleep  => "Sl",
                _                              => "?",
            };
        }
    }
}
