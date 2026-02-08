using System;

namespace SynQPanel.Rendering
{
    public sealed class FlipAnimator

    {
        private float _progress;
        private bool _animating;
        private DateTime _startTime;

        // Duration of flip in seconds (tweak later if needed)
        private const float Duration = 0.6f;

        public void Trigger()
        {
            _animating = true;
            _progress = 0f;
            _startTime = DateTime.UtcNow;
        }

        public float Update()
        {
            if (!_animating)
                return 1f; // fully settled

            float elapsed =
                (float)(DateTime.UtcNow - _startTime).TotalSeconds;

            _progress = elapsed / Duration;

            if (_progress >= 1f)
            {
                _progress = 1f;
                _animating = false;
            }

            return _progress;
        }

        public bool IsAnimating => _animating;
    }
}
