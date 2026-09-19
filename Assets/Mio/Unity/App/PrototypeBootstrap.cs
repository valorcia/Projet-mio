using Mio.Core.Economy;
using Mio.Core.Flow;
using Mio.Core.Harness;
using Mio.Core.Metrics;
using Mio.Core.Profile;
using Mio.Core.Session;
using Mio.Unity.Config;
using Mio.Unity.Feedback;
using Mio.Unity.Input;
using Mio.Unity.View;
using UnityEngine;
using UnityEngine.UI;

namespace Mio.Unity.App
{
    /// <summary>
    /// The composition root, and the whole scene, built in code.
    ///
    /// A scene contains exactly one of these and nothing else. That keeps the
    /// prototypes in source rather than in fragile scene YAML, makes them
    /// diffable and mergeable, and means a new rule set is a new config asset
    /// rather than an afternoon of wiring in the inspector.
    ///
    /// This is also the only place that decides which concrete store, sinks and
    /// view are used; nothing below it knows.
    /// </summary>
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("Which rule set to run, and every number that shapes it.")]
        [SerializeField] private PrototypeConfigAsset _config;

        [Header("Session")]
        [Tooltip("Seconds the result panel ignores taps, so the winning tap does not restart.")]
        [SerializeField] private float _replayLockout = 0.6f;

        [Tooltip("Write one JSON line per session to persistentDataPath.")]
        [SerializeField] private bool _logMetricsToFile = true;

        [SerializeField] private int _targetFrameRate = 60;

        private IPrototypeRules _rules;
        private PrototypeRunner _runner;
        private PrototypeView _view;
        private HudView _hud;
        private FeedbackRouter _feedback;
        private PunchAnimator _punch;
        private PlayFieldInput _input;
        private PlayerWallet _wallet;
        private PrototypePalette _palette;

        private float _duration;
        private float _replayArmedAt;
        private bool _resolved;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[MIO] PrototypeBootstrap has no config asset; nothing to run.", this);
                enabled = false;
                return;
            }

            // Portrait-first, and never let the screen dim during a playtest.
            Application.targetFrameRate = _targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            EventSystemFactory.EnsureExists();

            var layers = BuildCanvas();
            BuildSystems(layers);
            BuildSession(layers);
        }

        private void Start()
        {
            // No menu, no start button: the session is already running when the
            // scene opens.
            BeginSession(replay: false);
        }

        private struct Layers
        {
            public RectTransform Shake;
            public RectTransform Game;
            public RectTransform Particles;
            public RectTransform Hud;
            public RectTransform Input;
        }

        private Layers BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            // Match width: the field is always 1080 units across, so thumb
            // distances are identical on every phone.
            scaler.matchWidthOrHeight = 0f;

            var canvasRect = (RectTransform)canvasGo.transform;

            var background = UiFactory.Rect(canvasRect, "Background", Palette.Background);
            UiFactory.Fill((RectTransform)background.transform);

            // A fixed 9:16 box letterboxed into the canvas. Everything gameplay
            // touches lives inside it, so a taller phone changes the letterbox
            // and nothing else, and a normalised coordinate means the same
            // thing on every device.
            var playField = UiFactory.Node(canvasRect, "PlayField");
            UiFactory.Fill(playField);
            var fitter = playField.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = UiFactory.FieldAspect;

            var shake = UiFactory.Node(playField, "Shake");
            UiFactory.Fill(shake);

            var game = UiFactory.Node(shake, "Game");
            UiFactory.Fill(game);

            var particles = UiFactory.Node(shake, "Particles");
            UiFactory.Fill(particles);

            // The HUD sits outside the shake so the meter stays readable while
            // the play field is being thrown around.
            var hud = UiFactory.Node(playField, "Hud");
            UiFactory.Fill(hud);

            var inputSurface = UiFactory.Rect(playField, "InputSurface", Color.clear, raycast: true);
            var inputRect = (RectTransform)inputSurface.transform;
            UiFactory.Fill(inputRect);
            inputRect.SetAsLastSibling();

            return new Layers
            {
                Shake = shake,
                Game = game,
                Particles = particles,
                Hud = hud,
                Input = inputRect
            };
        }

        private void BuildSystems(Layers layers)
        {
            _punch = gameObject.AddComponent<PunchAnimator>();

            var feedbackGo = new GameObject("Feedback", typeof(BurstPool), typeof(FeedbackRouter));
            feedbackGo.transform.SetParent(transform, false);

            _feedback = feedbackGo.GetComponent<FeedbackRouter>();
            _feedback.Initialise(_config.Feedback, Palette, layers.Particles, layers.Shake);
            _feedback.CueEmitted += OnCue;

            _input = layers.Input.gameObject.AddComponent<PlayFieldInput>();
            _input.Command += OnInput;

            _hud = gameObject.AddComponent<HudView>();
            _hud.Build(layers.Hud, Palette);

            // The only place a concrete store is named.
            _wallet = new PlayerWallet(new PlayerPrefsProfileStore());
            _hud.SetWallet(_wallet.Balance);
            _wallet.Changed += (_, balance) => _hud.SetWallet(balance);
        }

        private void BuildSession(Layers layers)
        {
            IMetricsSink sink = _logMetricsToFile
                ? new CompositeMetricsSink(new ConsoleMetricsSink(), new JsonlMetricsSink())
                : new ConsoleMetricsSink();

            _rules = CreateRules(layers.Game);

            _runner = new PrototypeRunner(_rules, sink, _config.BuildRewardTable(), _wallet);
            _runner.SessionFinished += OnSessionFinished;

            _view.Initialise(layers.Game, Palette, _punch, _feedback);
        }

        private IPrototypeRules CreateRules(RectTransform gameLayer)
        {
            switch (_config)
            {
                case TestRulesetConfigAsset harness:
                {
                    var rules = new TestRuleset(harness.Build());
                    var view = gameLayer.gameObject.AddComponent<HarnessView>();
                    view.Bind(rules);
                    _view = view;
                    return rules;
                }

                case FlowConfigAsset flow:
                {
                    var rules = new FlowRules(flow.Build());
                    var view = gameLayer.gameObject.AddComponent<FlowView>();
                    view.Bind(rules);
                    _view = view;
                    return rules;
                }

                default:
                    throw new System.NotSupportedException(
                        $"Unknown config type {_config.GetType().Name}");
            }
        }

        /// <summary>
        /// Resolved once: a fallback palette must be a single shared instance,
        /// not a fresh ScriptableObject per caller.
        /// </summary>
        private PrototypePalette Palette
        {
            get
            {
                if (_palette != null) return _palette;

                if (_config.Palette != null)
                {
                    _palette = _config.Palette;
                }
                else
                {
                    // A missing palette should not mean a black screen and a
                    // NullReferenceException in the middle of a playtest.
                    Debug.LogWarning("[MIO] Config has no palette; using defaults.", this);
                    _palette = ScriptableObject.CreateInstance<PrototypePalette>();
                }

                return _palette;
            }
        }

        private void BeginSession(bool replay)
        {
            _resolved = false;

            _feedback.ResetState();
            _punch.Clear();
            _input.ResetClock();

            var seed = _config.NextSeed();
            if (replay) _runner.RequestReplay(seed, _feedback);
            else _runner.Begin(seed, _feedback);

            _duration = CurrentDuration();

            _view.OnSessionBegan();
            _hud.OnSessionBegan();
        }

        /// <summary>
        /// Session length is rule-set specific and deliberately not on
        /// IPrototypeRules, which the spec keeps to five members. The HUD asks
        /// the concrete rule set instead.
        /// </summary>
        private float CurrentDuration() => CurrentTimeRemaining();

        private float CurrentTimeRemaining()
        {
            switch (_rules)
            {
                case TestRuleset harness: return harness.TimeRemaining;
                case FlowRules flow: return flow.TimeRemaining;
                default: return 0f;
            }
        }

        private void Update()
        {
            if (_runner == null) return;

            // Once the result panel is up the simulation is frozen, including
            // after an Abandon, which leaves the rules themselves still running.
            if (!_resolved) _runner.Tick(Time.deltaTime, _feedback);

            _view.Refresh();
            _hud.Refresh(_rules, CurrentTimeRemaining(), _duration);
        }

        private void OnInput(InputCommand command)
        {
            if (_resolved)
            {
                // Tap anywhere to play again, once the result has had a beat to
                // land.
                if (command.Phase == InputPhase.Began && Time.unscaledTime >= _replayArmedAt)
                {
                    BeginSession(replay: true);
                }

                return;
            }

            _runner.SubmitInput(command, _feedback);
        }

        private void OnCue(FeedbackCue cue) => _view.OnCue(cue);

        private void OnSessionFinished(MetricReport report, ResourceBundle reward)
        {
            // The runner has already banked the reward in the wallet.
            _resolved = true;
            _replayArmedAt = Time.unscaledTime + _replayLockout;

            _hud.ShowResult(report, reward);
        }

        private void OnApplicationPause(bool paused)
        {
            // Backgrounding mid-session would otherwise bank a bogus session
            // duration once the app returns.
            if (paused && !_resolved) _runner?.Abandon();
        }

        private void OnDestroy()
        {
            if (_feedback != null) _feedback.CueEmitted -= OnCue;
            if (_input != null) _input.Command -= OnInput;
            if (_runner != null) _runner.SessionFinished -= OnSessionFinished;
        }
    }
}
