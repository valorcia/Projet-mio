using System.Collections.Generic;
using Mio.Core.Common;
using Mio.Core.Session;

namespace Mio.Core.PopChain
{
    /// <summary>
    /// POP CHAIN: tap any blob touching a twin. The group bursts, the board
    /// falls in, and popping again quickly keeps a multiplier alive.
    ///
    /// One finger, one verb: tap. The board is always full and always has a
    /// legal move, so there is nothing to explain and nothing to get stuck on.
    /// </summary>
    public sealed class PopChainRules : IPrototypeRules
    {
        private readonly PopChainConfig _config;
        private readonly List<int> _group = new List<int>();

        private PopChainBoard _board;
        private DeterministicRng _rng;

        private float _elapsed;
        private float _lastPopTime;
        private int _chainMultiplier = 1;
        private int _longestChain;
        private int _cellsCleared;
        private int _pops;
        private int _badTaps;

        public PopChainRules(PopChainConfig config)
        {
            _config = config ?? new PopChainConfig();
        }

        public PrototypeId Id => PrototypeId.PopChain;
        public PrototypeStatus Status { get; private set; } = PrototypeStatus.Idle;
        public int Score { get; private set; }
        public int SuccessfulActions => _pops;
        public int FailedActions => _badTaps;

        public float Progress01 =>
            _config.TargetScore <= 0 ? 1f : MathK.Clamp01(Score / (float)_config.TargetScore);

        public float TimeRemaining => MathK.Clamp(_config.Duration - _elapsed, 0f, _config.Duration);

        public PopChainBoard Board => _board;
        public int ChainMultiplier => _chainMultiplier;

        /// <summary>Seconds left to pop again before the multiplier resets.</summary>
        public float ChainTimeRemaining =>
            _chainMultiplier <= 1
                ? 0f
                : MathK.Clamp(_lastPopTime + _config.ChainWindow - _elapsed, 0f, _config.ChainWindow);

        public void Begin(int seed, IFeedbackChannel feedback)
        {
            _rng = new DeterministicRng(seed);
            _board = new PopChainBoard(_config.Width, _config.Height);
            _board.Reshuffle(_rng, _config.ColorCount, _config.MinGroupSize);

            _elapsed = 0f;
            _lastPopTime = float.NegativeInfinity;
            _chainMultiplier = 1;
            _longestChain = 1;
            _cellsCleared = 0;
            _pops = 0;
            _badTaps = 0;
            Score = 0;
            Status = PrototypeStatus.Playing;

            feedback.Emit(new FeedbackCue(FeedbackCueKind.Begin, new Vec2(0.5f, 0.5f)));
        }

        public void Tick(float deltaTime, IFeedbackChannel feedback)
        {
            if (Status != PrototypeStatus.Playing || deltaTime <= 0f) return;

            _elapsed += deltaTime;

            if (_chainMultiplier > 1 && _elapsed - _lastPopTime > _config.ChainWindow)
            {
                _chainMultiplier = 1;
            }

            if (Score >= _config.TargetScore)
            {
                Status = PrototypeStatus.Won;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Win, new Vec2(0.5f, 0.5f)));
                return;
            }

            if (_elapsed >= _config.Duration)
            {
                Status = PrototypeStatus.Lost;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Lose, new Vec2(0.5f, 0.5f)));
            }
        }

        public void HandleInput(in InputCommand command, IFeedbackChannel feedback)
        {
            if (Status != PrototypeStatus.Playing) return;

            // Resolve on press, not release: the burst must land under the
            // finger the moment it touches, or the whole prototype feels dead.
            if (command.Phase != InputPhase.Began) return;

            if (!TryGetCell(command.Position, out var cx, out var cy))
            {
                return;
            }

            var found = _board.FindGroup(cx, cy);

            if (found.Count < _config.MinGroupSize)
            {
                _badTaps++;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Fail, CellCenter(cx, cy), 0.3f));
                return;
            }

            // FindGroup reuses its buffer and we are about to call back into the
            // board, so take a copy before mutating anything.
            _group.Clear();
            for (var i = 0; i < found.Count; i++) _group.Add(found[i]);

            Pop(cx, cy, feedback);
        }

        private void Pop(int cx, int cy, IFeedbackChannel feedback)
        {
            var size = _group.Count;
            var chained = _chainMultiplier > 1 || _elapsed - _lastPopTime <= _config.ChainWindow;

            if (chained && _pops > 0)
            {
                _chainMultiplier = MathK.Clamp(_chainMultiplier + 1, 1, _config.MaxChainMultiplier);
                if (_chainMultiplier > _longestChain) _longestChain = _chainMultiplier;
            }
            else
            {
                _chainMultiplier = 1;
            }

            _lastPopTime = _elapsed;
            _pops++;
            _cellsCleared += size;
            Score += _config.ScorePerCell * size * _chainMultiplier;

            var at = CellCenter(cx, cy);

            _board.Clear(_group);
            _board.ApplyGravity();
            _board.Refill(_rng, _config.ColorCount);

            if (!_board.HasMove(_config.MinGroupSize))
            {
                _board.Reshuffle(_rng, _config.ColorCount, _config.MinGroupSize);
            }

            // Intensity scales with group size so a big clear reads as bigger
            // without the rules knowing anything about particles.
            var intensity = MathK.Clamp01(size / 8f);
            feedback.Emit(new FeedbackCue(FeedbackCueKind.Pop, at, intensity, size));

            if (_chainMultiplier > 1)
            {
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Chain, at, intensity, _chainMultiplier));
            }

            feedback.Emit(new FeedbackCue(FeedbackCueKind.Progress, at, Progress01));
        }

        /// <summary>Maps a play-field point to a board cell.</summary>
        public bool TryGetCell(Vec2 position, out int cx, out int cy)
        {
            cx = 0;
            cy = 0;

            var spanX = _config.FieldMaxX - _config.FieldMinX;
            var spanY = _config.FieldMaxY - _config.FieldMinY;
            if (spanX <= 0f || spanY <= 0f) return false;

            var u = (position.X - _config.FieldMinX) / spanX;
            var v = (position.Y - _config.FieldMinY) / spanY;
            if (u < 0f || u >= 1f || v < 0f || v >= 1f) return false;

            cx = MathK.Clamp((int)(u * _board.Width), 0, _board.Width - 1);
            cy = MathK.Clamp((int)(v * _board.Height), 0, _board.Height - 1);
            return true;
        }

        /// <summary>Centre of a cell in play-field space, for the view and for cues.</summary>
        public Vec2 CellCenter(int cx, int cy)
        {
            var spanX = _config.FieldMaxX - _config.FieldMinX;
            var spanY = _config.FieldMaxY - _config.FieldMinY;

            return new Vec2(
                _config.FieldMinX + (cx + 0.5f) / _board.Width * spanX,
                _config.FieldMinY + (cy + 0.5f) / _board.Height * spanY);
        }

        public void CollectCustomMetrics(IDictionary<string, double> into)
        {
            into["pop.pops"] = _pops;
            into["pop.badTaps"] = _badTaps;
            into["pop.cellsCleared"] = _cellsCleared;
            into["pop.longestChain"] = _longestChain;
            into["pop.avgGroupSize"] = _pops == 0 ? 0d : _cellsCleared / (double)_pops;
        }
    }
}
