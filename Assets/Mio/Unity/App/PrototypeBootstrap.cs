using Mio.Core.Economy;
using Mio.Core.Flow;
using Mio.Core.Metrics;
using Mio.Core.Pack;
using Mio.Core.PopChain;
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
    /// The whole scene, built in code.
    ///
    /// A prototype scene contains exactly one of these and nothing else. That
    /// keeps the three prototypes in source rather than in fragile scene YAML,
    /// makes them diffable and mergeable, and means a new prototype is a new
    /// config asset rather than an afternoon of wiring in the inspector.
    /// </summary>
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("Which prototype to run, and every number that shapes it.")]
        [SerializeField] private PrototypeConfigAsset _config;

        [Header("Session")]
        [Tooltip("Seconds the result panel ignores taps, so the winning tap does not restart.")]
        [SerializeField] private float _replayLockout = 0.6f;

        [Tooltip("Write one JSON line per attempt to persistentDataPath.")]
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
            BuildGameplay(layers);
        }

        private void Start()
        {
            // No menu, no start button: the run is already going when the scene
            // opens. This is the M0 bar, so it is the default, not an option.
            BeginRun();
        }

        private struct Layers
        {
            public RectTransform PlayField;
            public RectTransform Shake;
            public RectTransform Game;
            public RectTransform Particles;
            public RectTransform Hud;
            public RectTransform Input;
        }

        private Layers BuildCanvas()
        {
            var palette = Palette;

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

            var background = UiFactory.Rect(canvasRect, "Background", palette.Background);
            UiFactory.Fill((RectTransform)background.transform);

            // A fixed 9:16 box letterboxed into the canvas. Everything gameplay
            // touches lives inside it, so a tall phone changes the letterbox and
            // nothing else.
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

            // The HUD sits outside the shake so the meter stays readable when
            // the board is being thrown around.
            var hud = UiFactory.Node(playField, "Hud");
            UiFactory.Fill(hud);

            var inputSurface = UiFactory.Rect(playField, "InputSurface", Color.clear, raycast: true);
            var inputRect = (RectTransform)inputSurface.transform;
            UiFactory.Fill(inputRect);
            inputRect.SetAsLastSibling();

            return new Layers
            {
                PlayField = playField,
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

            _wallet = new PlayerWallet(new PlayerPrefsProfileStore());
        }

        private void BuildGameplay(Layers layers)
        {
            IMetricsSink sink = _logMetricsToFile
                ? new CompositeMetricsSink(new ConsoleMetricsSink(), new JsonFileMetricsSink())
                : new ConsoleMetricsSink();

            _rules = CreateRules(layers.Game);
            _runner = new PrototypeRunner(_rules, sink, _config.BuildRewardTable());
            _runner.AttemptFinished += OnAttemptFinished;

            _view.Initialise(layers.Game, Palette, _punch);
        }

        private IPrototypeRules CreateRules(RectTransform gameLayer)
        {
            switch (_config)
            {
                case FlowConfigAsset flow:
                {
                    var rules = new FlowRules(flow.Build());
                    var view = gameLayer.gameObject.AddComponent<FlowView>();
                    view.Bind(rules);
                    _view = view;
                    return rules;
                }

                case PopChainConfigAsset pop:
                {
                    var rules = new PopChainRules(pop.Build());
                    var view = gameLayer.gameObject.AddComponent<PopChainView>();
                    view.Bind(rules);
                    _view = view;
                    return rules;
                }

                case PackConfigAsset pack:
                {
                    var rules = new PackRules(pack.Build());
                    var view = gameLayer.gameObject.AddComponent<PackView>();
                    view.Bind(rules);
                    _view = view;
                    return rules;
                }

                default:
                    throw new System.NotSupportedException(
                        $"Unknown prototype config type {_config.GetType().Name}");
            }
        }

        private PrototypePalette _palette;

        /// <summary>
        /// Resolved once: a fallback palette must be a single shared instance,
        /// not a fresh ScriptableObject per caller.
        /// </summary>
        private PrototypePalette Palette
        {
            get
            {
                if (_palette != null) return _palette;

                // A missing palette should not mean a black screen and a
                // NullReferenceException in the middle of a playtest.
                if (_config.Palette != null)
                {
                    _palette = _config.Palette;
                }
                else
                {
                    Debug.LogWarning("[MIO] Config has no palette; using defaults.", this);
                    _palette = ScriptableObject.CreateInstance<PrototypePalette>();
                }

                return _palette;
            }
        }

        private void BeginRun()
        {
            _resolved = false;

            _feedback.ResetState();
            _punch.Clear();
            _input.ResetClock();

            _runner.Begin(_config.NextSeed(), _feedback);
            _duration = _rules.TimeRemaining;

            _view.OnRunBegan();
            _hud.OnRunBegan();
        }

        private void Update()
        {
            if (_runner == null) return;

            // Once the result panel is up the simulation is frozen, including
            // after an Abandon, which leaves the rules themselves still running.
            if (!_resolved) _runner.Tick(Time.deltaTime, _feedback);

            _view.Refresh();
            _hud.Refresh(_rules, _duration);
            _hud.SetExtra(ExtraLine());
        }

        /// <summary>The one prototype-specific readout, kept to a few characters.</summary>
        private string ExtraLine()
        {
            switch (_rules)
            {
                case PopChainRules pop:
                    return pop.ChainMultiplier > 1 ? "x" + pop.ChainMultiplier : string.Empty;

                default:
                    return string.Empty;
            }
        }

        private void OnInput(InputCommand command)
        {
            if (_resolved)
            {
                // Tap anywhere to play again, once the result has had a beat to
                // land.
                if (command.Phase == InputPhase.Began && Time.unscaledTime >= _replayArmedAt)
                {
                    BeginRun();
                }

                return;
            }

            _runner.SubmitInput(command, _feedback);
        }

        private void OnCue(FeedbackCue cue)
        {
            _view.OnCue(cue);
        }

        private void OnAttemptFinished(MetricReport report, ResourceBundle reward)
        {
            _resolved = true;
            _replayArmedAt = Time.unscaledTime + _replayLockout;

            _wallet.Deposit(reward);
            _hud.ShowResult(report, reward);
        }

        private void OnApplicationPause(bool paused)
        {
            // Backgrounding mid-run would otherwise bank a bogus session
            // duration once the app returns.
            if (paused && !_resolved) _runner?.Abandon();
        }

        private void OnDestroy()
        {
            if (_feedback != null) _feedback.CueEmitted -= OnCue;
            if (_input != null) _input.Command -= OnInput;
            if (_runner != null) _runner.AttemptFinished -= OnAttemptFinished;
        }
    }
}
