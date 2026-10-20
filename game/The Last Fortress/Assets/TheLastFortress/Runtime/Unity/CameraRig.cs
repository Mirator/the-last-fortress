using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastFortress
{
    /// <summary>
    /// Fixed-orientation orthographic camera: pan (WASD/arrows or right-button drag) and zoom, no rotation.
    /// Dragging "grabs" the board: the ground point under the pointer stays under it. A right press that moves less
    /// than <see cref="DragThresholdPixels"/> is still a plain right click (cancel), see <see cref="RightReleaseWasDrag"/>.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        public const float Yaw = 45f;
        public const float Pitch = 40f;
        public static readonly Vector3 DefaultFocus = new Vector3(10f, 0f, 10f);
        // Plan 014: closer by default so true-scale figures (about 0.22 tile) read; zoom out for the whole board.
        public const float DefaultSize = 5.5f;
        const float MinSize = 2.5f, MaxSize = 14f, PanSpeed = 12f, Distance = 40f;

        Camera cam;
        Vector3 focus = DefaultFocus;
        float size = DefaultSize;
        float shake;
        bool dragArmed, dragging;
        Vector2 dragStart, dragLast;

        /// <summary>Pointer travel before a held right button becomes a pan drag instead of a click.</summary>
        public const float DragThresholdPixels = 6f;

        /// <summary>Cursor shown while dragging (MVP content; null keeps the system cursor).</summary>
        public Texture2D PanCursor { get; set; }

        public bool IsDragging => dragging;

        /// <summary>True on the frame the right button is released after a drag: that release is not a cancel click.</summary>
        public bool RightReleaseWasDrag { get; private set; }

        /// <summary>Settings multiplier for pan speed and zoom step (MVP settings; 1 = prototype behavior).</summary>
        public float Sensitivity { get; set; } = 1f;

        /// <summary>Settings toggle; when false <see cref="Kick"/> does nothing.</summary>
        public bool ShakeEnabled { get; set; } = true;

        public float ShakeAmount => shake;

        /// <summary>Small bounded screen shake (Keep hits). Never moves the logical focus.</summary>
        public void Kick(float amount)
        {
            if (ShakeEnabled) shake = Mathf.Min(0.3f, shake + amount);
        }

        public Camera Camera => cam != null ? cam : cam = GetComponent<Camera>();
        public Vector3 Focus => focus;
        public float Size => size;

        void Awake()
        {
            Apply();
        }

        public void ResetView() => SetView(DefaultFocus, DefaultSize);

        /// <summary>Half-height that frames the whole board with its entries (the pre-plan-014 default).</summary>
        public const float WholeBoardSize = 10.5f;

        /// <summary>Z: the whole board at once, e.g. to see every entry before a night.</summary>
        public void ShowWholeBoard() => SetView(DefaultFocus, WholeBoardSize);

        public void SetView(Vector3 newFocus, float newSize)
        {
            focus = newFocus;
            size = Mathf.Clamp(newSize, MinSize, MaxSize);
            Apply();
        }

        public bool TryScreenToWorld(Vector2 screen, out Vector3 world)
        {
            world = default;
            var ray = Camera.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            world = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
            return true;
        }

        /// <summary>WASD/arrows pan, wheel zooms. Uses unscaled real time so it works while paused.</summary>
        public void Tick(Keyboard keyboard, Mouse mouse, bool pointerOverUi, float dt)
        {
            var move = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1;
            }

            if (move != Vector2.zero)
            {
                var rot = Quaternion.Euler(0, Yaw, 0);
                var right = rot * Vector3.right;
                var forward = rot * Vector3.forward;
                focus += (right * move.x + forward * move.y).normalized * (PanSpeed * Sensitivity * dt * size / DefaultSize);
                ClampFocus();
            }

            RightReleaseWasDrag = false;
            if (mouse != null)
            {
                var pointer = mouse.position.ReadValue();
                if (mouse.rightButton.wasPressedThisFrame && !pointerOverUi)
                {
                    dragArmed = true;
                    dragStart = dragLast = pointer;
                }

                if (dragArmed && mouse.rightButton.isPressed)
                {
                    if (!dragging && (pointer - dragStart).sqrMagnitude >= DragThresholdPixels * DragThresholdPixels) SetDragging(true);
                    if (dragging && pointer != dragLast && TryScreenToWorld(dragLast, out var from) && TryScreenToWorld(pointer, out var to))
                    {
                        focus += new Vector3(from.x - to.x, 0f, from.z - to.z);
                        ClampFocus();
                    }

                    if (dragging) dragLast = pointer; // until the threshold, travel is kept, so the grab starts exact
                }

                if (mouse.rightButton.wasReleasedThisFrame)
                {
                    RightReleaseWasDrag = dragging;
                    CancelDrag();
                }
            }

            if (mouse != null && !pointerOverUi && !dragging)
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) size = Mathf.Clamp(size - Mathf.Sign(scroll) * 0.8f * Sensitivity, MinSize, MaxSize);
            }

            shake = Mathf.MoveTowards(shake, 0f, dt * 0.8f);
            Apply();
        }

        void ClampFocus()
        {
            focus.x = Mathf.Clamp(focus.x, -2f, 22f);
            focus.z = Mathf.Clamp(focus.z, -2f, 22f);
        }

        /// <summary>Ends any drag and restores the system cursor (button released, menu or dialog opened, run reset).</summary>
        public void CancelDrag()
        {
            dragArmed = false;
            SetDragging(false);
        }

        void SetDragging(bool value)
        {
            if (dragging == value) return;
            dragging = value;
            if (PanCursor != null) Cursor.SetCursor(value ? PanCursor : null, value ? new Vector2(PanCursor.width / 2f, PanCursor.height / 2f) : Vector2.zero, CursorMode.Auto);
        }

        void OnDisable() => CancelDrag();

        void Apply()
        {
            var c = Camera;
            c.orthographic = true;
            c.orthographicSize = size;
            var rotation = Quaternion.Euler(Pitch, Yaw, 0);
            transform.rotation = rotation;
            var jitter = shake > 0f ? new Vector3(Mathf.Sin(Time.unscaledTime * 61f), 0f, Mathf.Cos(Time.unscaledTime * 53f)) * shake : Vector3.zero;
            transform.position = focus + jitter - rotation * Vector3.forward * Distance;
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 120f;
        }

        /// <summary>Intersects the pointer ray with the board plane and floors to a cell; false outside the grid.</summary>
        public bool TryScreenToCell(Vector2 screen, out GridPos cell)
        {
            cell = default;
            var ray = Camera.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            var t = -ray.origin.y / ray.direction.y;
            if (t < 0) return false;
            var hit = ray.origin + ray.direction * t;
            return GridCoordinates.TryWorldToGrid(hit.x, hit.z, out cell);
        }

        public Vector2 CellToScreen(GridPos cell, float y = 0f)
        {
            var p = Camera.WorldToScreenPoint(Prims.CellCenter(cell, y));
            return new Vector2(p.x, p.y);
        }
    }
}
