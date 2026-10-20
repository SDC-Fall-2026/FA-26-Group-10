using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HotPotato
{
    public sealed class PotatoInput
    {
        public const float ThrowThreshold = 0.22f;
        public const float FullChargeTime = 0.95f;
        public Vector2 Move { get; private set; }
        public Vector2 StickOrigin { get; private set; }
        public bool StickActive => moveFinger >= 0;
        public bool TagPressed { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool ThrowReleased { get; private set; }
        public bool PrimaryHeld { get; private set; }
        public float Charge { get; private set; }
        public bool HasAim { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public bool PausePressed { get; private set; }
        public bool StartPressed { get; private set; }
        public bool RestartPressed { get; private set; }
        public bool MutePressed { get; private set; }
        public bool FallPressed { get; private set; }
        int moveFinger = -1;
        int tagFinger = -1;
        int primarySource; // 1 mouse, 2 touch, 3 keyboard, 4 controller
        Vector2 gestureOrigin;
        float started;
        readonly System.Collections.Generic.List<RaycastResult> hits = new System.Collections.Generic.List<RaycastResult>();

        bool OverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            return hits.Count > 0;
        }

        public void Reset()
        {
            moveFinger = tagFinger = -1;
            primarySource = 0;
            PrimaryHeld = HasAim = false;
            Charge = 0;
            Move = Vector2.zero;
        }

        void Begin(int source)
        {
            if (primarySource != 0) return;
            primarySource = source;
            started = Time.unscaledTime;
            PrimaryHeld = true;
            Charge = 0;
        }

        void Release(int source, bool canceled = false)
        {
            if (primarySource != source) return;
            float held = Time.unscaledTime - started;
            Charge = Mathf.Clamp01((held - ThrowThreshold) / (FullChargeTime - ThrowThreshold));
            if (!canceled)
            {
                ThrowReleased = held >= ThrowThreshold;
                TagPressed |= !ThrowReleased;
            }
            primarySource = 0;
            PrimaryHeld = false;
        }

        public void Read(PotatoView view, Vector3 player, bool playing)
        {
            Move = Vector2.zero;
            TagPressed = JumpPressed = ThrowReleased = PausePressed = StartPressed = RestartPressed = MutePressed = FallPressed = false;
            var key = Keyboard.current;
            var pad = Gamepad.current;
            if (key != null)
            {
                PausePressed = key.escapeKey.wasPressedThisFrame || key.pKey.wasPressedThisFrame;
                StartPressed = key.enterKey.wasPressedThisFrame;
                RestartPressed = key.rKey.wasPressedThisFrame;
                MutePressed = key.mKey.wasPressedThisFrame;
                FallPressed = key.iKey.wasPressedThisFrame;
            }
            if (pad != null) { PausePressed |= pad.startButton.wasPressedThisFrame; StartPressed |= pad.buttonSouth.wasPressedThisFrame; }
            if (!playing) { Reset(); return; }
            if (key != null)
            {
                Move = new Vector2((key.dKey.isPressed || key.rightArrowKey.isPressed ? 1 : 0) - (key.aKey.isPressed || key.leftArrowKey.isPressed ? 1 : 0),
                    (key.wKey.isPressed || key.upArrowKey.isPressed ? 1 : 0) - (key.sKey.isPressed || key.downArrowKey.isPressed ? 1 : 0));
                TagPressed = key.leftShiftKey.wasPressedThisFrame || key.rightShiftKey.wasPressedThisFrame;
                JumpPressed = key.spaceKey.wasPressedThisFrame;
                if (key.eKey.wasPressedThisFrame) Begin(3);
                if (key.eKey.wasReleasedThisFrame) Release(3);
            }
            

            if (pad != null)
            {
                if (pad.leftStick.ReadValue().sqrMagnitude > 0.02f) Move = pad.leftStick.ReadValue();
                Vector2 aim = pad.rightStick.ReadValue();
                if (aim.sqrMagnitude > 0.1f) { AimPoint = player + new Vector3(aim.x, 0, aim.y) * 5; HasAim = true; }
                JumpPressed |= pad.buttonSouth.wasPressedThisFrame;
                TagPressed |= pad.buttonWest.wasPressedThisFrame;
                if (pad.rightShoulder.wasPressedThisFrame) Begin(4);
                if (pad.rightShoulder.wasReleasedThisFrame) Release(4);
            }
            var screen = Touchscreen.current;
            bool touching = false;
            if (screen != null)
            {
                for (int i = 0; i < screen.touches.Count; i++)
                {
                    var touch = screen.touches[i];
                    Vector2 position = touch.position.ReadValue();
                    if (touch.press.isPressed || touch.press.wasReleasedThisFrame) touching = true;
                    if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        if (moveFinger == i) { moveFinger = -1; Move = Vector2.zero; }
                        if (tagFinger == i) { Release(2, true); tagFinger = -1; }
                        continue;
                    }
                    if (touch.press.wasPressedThisFrame && !OverUI(position))
                    {
                        if (position.x < Screen.width * 0.44f && moveFinger < 0) { moveFinger = i; StickOrigin = position; }
                        else if (tagFinger < 0 && primarySource == 0) { tagFinger = i; gestureOrigin = position; Begin(2); }
                    }
                    if (moveFinger == i)
                    {
                        Move = Vector2.ClampMagnitude((position - StickOrigin) / Mathf.Max(45, Screen.height * 0.075f), 1);
                        if (!touch.press.isPressed) { moveFinger = -1; Move = Vector2.zero; }
                    }
                    if (tagFinger == i)
                    {
                        Gesture(view, player, position);
                        if (touch.press.wasReleasedThisFrame) { Release(2, OverUI(position)); tagFinger = -1; }
                    }
                }
            }
            var mouse = Mouse.current;
            if (!touching && mouse != null)
            {
                Vector2 position = mouse.position.ReadValue();
                if (mouse.delta.ReadValue().sqrMagnitude > 0.01f || primarySource == 1)
                {
                    if (primarySource == 1) Gesture(view, player, position);
                    else { AimPoint = view.GroundPoint(position); HasAim = true; }
                }
                if (mouse.leftButton.wasPressedThisFrame && !OverUI(position))
                { gestureOrigin = position; Begin(1); AimPoint = view.GroundPoint(position); HasAim = true; }
                if (mouse.leftButton.wasReleasedThisFrame && primarySource == 1)
                { Gesture(view, player, position); Release(1, OverUI(position)); }
            }
            if (PrimaryHeld) Charge = Mathf.Clamp01((Time.unscaledTime - started - ThrowThreshold) / (FullChargeTime - ThrowThreshold));
            Move = Vector2.ClampMagnitude(Move, 1);
        }

        void Gesture(PotatoView view, Vector3 player, Vector2 end)
        {
            AimPoint = Time.unscaledTime - started < ThrowThreshold && (end - gestureOrigin).magnitude > Mathf.Max(24, Screen.height * 0.035f)
                ? player + (view.GroundPoint(end) - view.GroundPoint(gestureOrigin)).normalized * 5
                : view.GroundPoint(end);
            HasAim = true;
        }
    }
}
