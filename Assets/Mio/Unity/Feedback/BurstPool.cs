using System.Collections.Generic;
using Mio.Unity.View;
using UnityEngine;
using UnityEngine.UI;

namespace Mio.Unity.Feedback
{
    /// <summary>
    /// Pooled UI particles.
    ///
    /// A real ParticleSystem would need a material and a prefab; M0 needs
    /// neither, and a pooled pile of Images costs nothing on a mid-range phone.
    /// Everything is recycled, so a long session never allocates.
    /// </summary>
    public sealed class BurstPool : MonoBehaviour
    {
        private struct Particle
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
        }

        private readonly List<Particle> _active = new List<Particle>();
        private readonly Stack<Image> _idle = new Stack<Image>();

        private RectTransform _root;
        private int _maxParticles = 160;

        public void Initialise(RectTransform root, int maxParticles = 160)
        {
            _root = root;
            _maxParticles = Mathf.Max(8, maxParticles);
        }

        /// <summary>Fires a radial burst at a point in play-field space.</summary>
        public void Emit(Vector2 position, int count, float speed, float size, Color color, float life = 0.5f)
        {
            if (_root == null || count <= 0) return;

            for (var i = 0; i < count; i++)
            {
                if (_active.Count >= _maxParticles) return;

                // Even angular spread with a jitter, so bursts read as a ring
                // rather than a random smear.
                var angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
                var magnitude = speed * Random.Range(0.55f, 1f);

                var velocity = new Vector2(
                    Mathf.Cos(angle) * magnitude,
                    Mathf.Sin(angle) * magnitude * UiFactory.FieldAspect);

                Spawn(position, velocity, size * Random.Range(0.7f, 1.2f), color, life * Random.Range(0.75f, 1.15f));
            }
        }

        private void Spawn(Vector2 position, Vector2 velocity, float size, Color color, float life)
        {
            Image image;
            if (_idle.Count > 0)
            {
                image = _idle.Pop();
                image.gameObject.SetActive(true);
            }
            else
            {
                image = UiFactory.Rect(_root, "Particle", color);
            }

            image.color = color;

            var rect = (RectTransform)image.transform;
            UiFactory.PlaceSquare(rect, position.x, position.y, size);

            _active.Add(new Particle
            {
                Rect = rect,
                Image = image,
                Velocity = velocity,
                Life = life,
                MaxLife = life,
                Size = size,
                Color = color
            });
        }

        private void Update()
        {
            var dt = Time.deltaTime;

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                p.Life -= dt;

                if (p.Life <= 0f)
                {
                    p.Image.gameObject.SetActive(false);
                    _idle.Push(p.Image);
                    _active.RemoveAt(i);
                    continue;
                }

                var center = p.Rect.anchorMin + (p.Rect.anchorMax - p.Rect.anchorMin) * 0.5f;
                center += p.Velocity * dt;

                // Drag plus a gentle fade and shrink: the burst settles instead
                // of flying off, which keeps the eye on the action.
                p.Velocity *= 1f - Mathf.Clamp01(3.5f * dt);

                var t = p.Life / p.MaxLife;
                var color = p.Color;
                color.a = p.Color.a * t;
                p.Image.color = color;

                UiFactory.PlaceSquare(p.Rect, center.x, center.y, p.Size * Mathf.Lerp(0.35f, 1f, t));

                _active[i] = p;
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _active.Count; i++)
            {
                _active[i].Image.gameObject.SetActive(false);
                _idle.Push(_active[i].Image);
            }

            _active.Clear();
        }
    }
}
