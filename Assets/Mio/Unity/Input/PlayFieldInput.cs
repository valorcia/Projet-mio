using System;
using Mio.Core.Common;
using Mio.Core.Session;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mio.Unity.Input
{
    /// <summary>
    /// Turns pointer events into normalised one-finger <see cref="InputCommand"/>s.
    ///
    /// Routed through the EventSystem rather than Input or InputSystem directly,
    /// so the project works under either input backend with no package
    /// dependency and no #if in gameplay code. The editor mouse and a real
    /// finger travel the identical path.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PlayFieldInput : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerMoveHandler,
        IDragHandler
    {
        private RectTransform _rect;
        private Camera _camera;

        /// <summary>Pointer currently owning the gesture; int.MinValue when idle.</summary>
        private int _activePointerId = int.MinValue;

        private float _startTime;

        public event Action<InputCommand> Command;

        /// <summary>True while a finger is down. Useful for view-only affordances.</summary>
        public bool IsPressed => _activePointerId != int.MinValue;

        /// <summary>Latest normalised position, valid while pressed.</summary>
        public Vec2 LastPosition { get; private set; } = new Vec2(0.5f, 0.5f);

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        /// <summary>
        /// Call at the start of each attempt so command timestamps are relative
        /// to the run, matching the metric recorder.
        /// </summary>
        public void ResetClock()
        {
            _startTime = Time.unscaledTime;
            _activePointerId = int.MinValue;
        }

        public void SetCamera(Camera camera) => _camera = camera;

        public void OnPointerDown(PointerEventData eventData)
        {
            // One finger only. A second touch is ignored outright rather than
            // fighting the first for control.
            if (IsPressed) return;

            _activePointerId = eventData.pointerId;
            Send(InputPhase.Began, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            Send(InputPhase.Moved, eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            // Fires below the drag threshold too, which keeps FLOW's steering
            // smooth instead of snapping once the finger travels 10 pixels.
            if (eventData.pointerId != _activePointerId) return;
            Send(InputPhase.Moved, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;

            Send(InputPhase.Ended, eventData);
            _activePointerId = int.MinValue;
        }

        private void OnDisable()
        {
            // The app lost focus or the view was torn down mid-gesture. Tell the
            // rules so a held piece is not stranded in the air.
            if (!IsPressed) return;

            _activePointerId = int.MinValue;
            Command?.Invoke(new InputCommand(
                InputPhase.Canceled,
                LastPosition,
                Time.unscaledTime - _startTime));
        }

        private void Send(InputPhase phase, PointerEventData eventData)
        {
            if (!TryNormalise(eventData.position, out var normalised)) return;

            LastPosition = normalised;

            Command?.Invoke(new InputCommand(
                phase,
                normalised,
                Time.unscaledTime - _startTime));
        }

        /// <summary>
        /// Screen pixels in, play-field space out.
        ///
        /// Unity resolves the screen point against the RectTransform; the
        /// mapping itself is PlayFieldSpace, in Core, so the resolution
        /// independence this relies on is covered by headless tests rather
        /// than only by inspection.
        /// </summary>
        private bool TryNormalise(Vector2 screenPoint, out Vec2 normalised)
        {
            normalised = Vec2.Zero;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, screenPoint, _camera, out var local))
            {
                return false;
            }

            var rect = _rect.rect;

            return PlayFieldSpace.TryNormalise(
                local.x, local.y,
                rect.xMin, rect.yMin,
                rect.width, rect.height,
                out normalised);
        }
    }
}
