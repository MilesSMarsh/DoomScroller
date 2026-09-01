using System;
using UnityEngine;

namespace DoomScroller.Input
{
    public enum SwipeDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    // Plain C# so tap/swipe recognition is testable without a running scene.
    public class TouchGestureService
    {
        private readonly float _swipeMinDistance;
        private readonly float _tapMaxDuration;

        private Vector2 _startPosition;
        private float _startTime;
        private bool _isTracking;

        public event Action<Vector2> Tapped;
        public event Action<SwipeDirection, Vector2> Swiped;

        public TouchGestureService(float swipeMinDistance = 50f, float tapMaxDuration = 0.3f)
        {
            _swipeMinDistance = swipeMinDistance;
            _tapMaxDuration = tapMaxDuration;
        }

        public void BeginTouch(Vector2 position, float time)
        {
            _startPosition = position;
            _startTime = time;
            _isTracking = true;
        }

        public void EndTouch(Vector2 position, float time)
        {
            if (!_isTracking)
            {
                return;
            }

            _isTracking = false;

            var delta = position - _startPosition;
            var duration = time - _startTime;

            if (delta.magnitude >= _swipeMinDistance)
            {
                Swiped?.Invoke(GetDirection(delta), delta);
            }
            else if (duration <= _tapMaxDuration)
            {
                Tapped?.Invoke(position);
            }
        }

        private static SwipeDirection GetDirection(Vector2 delta)
        {
            return Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? (delta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left)
                : (delta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down);
        }
    }
}
