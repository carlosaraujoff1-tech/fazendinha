using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Game.Presentation.Controls
{
    /// <summary>
    /// Câmera de toque: um dedo arrasta (pan no plano do chão), dois dedos fazem pinça (zoom),
    /// toque curto vira <see cref="Tapped"/>. No editor o mouse simula toque; a roda do mouse dá zoom.
    /// Toques que começam sobre a UI são ignorados.
    /// </summary>
    public sealed class TouchCameraController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float minDistance = 8f;
        [SerializeField] private float maxDistance = 30f;
        [SerializeField] private float distance = 17f;
        [SerializeField] private float pitch = 50f;
        [SerializeField] private Vector2 panMin = new Vector2(-10, -10);
        [SerializeField] private Vector2 panMax = new Vector2(10, 10);
        [SerializeField] private float tapMaxSeconds = 0.35f;
        [SerializeField] private float tapMaxPixels = 25f;

        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private PointerEventData _pointerData;
        private Vector3 _focus;
        private int _activeFinger = -1;
        private bool _gestureOverUi;
        private bool _moved;
        private float _touchStartTime;
        private Vector2 _touchStartPos;
        private float _lastPinchDistance;

        /// <summary>Toque curto no mundo (posição em pixels).</summary>
        public event Action<Vector2> Tapped;

        public Camera Camera => targetCamera;

        public void SetBounds(Vector2 min, Vector2 max) { panMin = min; panMax = max; ClampFocus(); Apply(); }

        /// <summary>Distância da câmera ao foco (zoom), limitada ao mínimo/máximo.</summary>
        public void SetZoom(float newDistance) { distance = Mathf.Clamp(newDistance, minDistance, maxDistance); Apply(); }

        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;

        public void FocusOn(Vector3 worldPoint) { _focus = new Vector3(worldPoint.x, 0, worldPoint.z); ClampFocus(); Apply(); }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            _focus = new Vector3(transform.position.x, 0, transform.position.z);
            Apply();
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            TouchSimulation.Enable();
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            TouchSimulation.Disable();
#endif
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            var touches = ETouch.activeTouches;

            if (touches.Count >= 2)
            {
                _moved = true; // pinça nunca vira toque curto
                if (_gestureOverUi) return;
                float d = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
                if (touches[1].phase == UnityEngine.InputSystem.TouchPhase.Began || _lastPinchDistance <= 0f)
                {
                    _lastPinchDistance = d;
                    return;
                }
                if (d > 1f)
                {
                    distance = Mathf.Clamp(distance * (_lastPinchDistance / d), minDistance, maxDistance);
                    Apply();
                }
                _lastPinchDistance = d;
                return;
            }
            _lastPinchDistance = 0f;

            if (touches.Count == 1)
            {
                var t = touches[0];
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began || _activeFinger != t.finger.index)
                {
                    _activeFinger = t.finger.index;
                    _gestureOverUi = IsOverUi(t.screenPosition);
                    _moved = false;
                    _touchStartTime = Time.unscaledTime;
                    _touchStartPos = t.screenPosition;
                }
                else if (!_gestureOverUi && (t.phase == UnityEngine.InputSystem.TouchPhase.Moved))
                {
                    if ((t.screenPosition - _touchStartPos).sqrMagnitude > tapMaxPixels * tapMaxPixels) _moved = true;
                    if (_moved) Pan(t.screenPosition - t.delta, t.screenPosition);
                }

                if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended)
                {
                    bool isTap = !_gestureOverUi && !_moved && Time.unscaledTime - _touchStartTime <= tapMaxSeconds;
                    _activeFinger = -1;
                    if (isTap) Tapped?.Invoke(t.screenPosition);
                }
            }
            else
            {
                _activeFinger = -1;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !IsOverUi(mouse.position.ReadValue()))
                {
                    distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * 1.5f, minDistance, maxDistance);
                    Apply();
                }
            }
        }

        private void Pan(Vector2 fromScreen, Vector2 toScreen)
        {
            if (!GroundPoint(fromScreen, out var a) || !GroundPoint(toScreen, out var b)) return;
            _focus += a - b;
            ClampFocus();
            Apply();
        }

        /// <summary>Ponto no chão (y = 0) sob uma posição de tela.</summary>
        public bool GroundPoint(Vector2 screen, out Vector3 point)
        {
            var ray = targetCamera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }

        private void ClampFocus()
        {
            _focus.x = Mathf.Clamp(_focus.x, panMin.x, panMax.x);
            _focus.z = Mathf.Clamp(_focus.z, panMin.y, panMax.y);
        }

        private void Apply()
        {
            if (targetCamera == null) return;
            var rot = Quaternion.Euler(pitch, 0f, 0f);
            targetCamera.transform.SetPositionAndRotation(_focus - rot * Vector3.forward * distance, rot);
        }

        private bool IsOverUi(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (_pointerData == null) _pointerData = new PointerEventData(es);
            _pointerData.position = screen;
            _uiHits.Clear();
            es.RaycastAll(_pointerData, _uiHits);
            return _uiHits.Count > 0;
        }
    }
}
