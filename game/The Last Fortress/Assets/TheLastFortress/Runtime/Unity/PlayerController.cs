using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TheLastFortress
{
    /// <summary>
    /// Player intent: build tool, selection, hover. Reads the Input System directly; devices can be injected by tests.
    /// UI hits are detected with an explicit EventSystem raycast so clicks never leak into the world.
    /// </summary>
    public sealed class PlayerController
    {
        readonly PrototypeBootstrap game;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        PointerEventData pointerData;
        EventSystem pointerDataOwner;

        public Mouse MouseOverride;
        public Keyboard KeyboardOverride;

        public BuildingKind? Tool { get; private set; }

        /// <summary>Cosmetic facing (90-degree steps) for the next Tower/House/Farm placed; R rotates it during Day.</summary>
        public int GhostOrientation { get; private set; }
        public Building Selected { get; private set; }
        public GridPos? HoverCell { get; private set; }
        public bool PointerOverUi { get; private set; }
        public Vector2 PointerPosition { get; private set; }
        public PlacementRejection LastRejection { get; private set; }

        /// <summary>Scripted hover (screenshots); overrides the pointer while set.</summary>
        public GridPos? HoverOverride;

        public Mouse Mouse => MouseOverride != null && MouseOverride.added ? MouseOverride : Mouse.current;
        public Keyboard Keyboard => KeyboardOverride != null && KeyboardOverride.added ? KeyboardOverride : Keyboard.current;

        public PlayerController(PrototypeBootstrap game)
        {
            this.game = game;
        }

        public void Reset()
        {
            Tool = null;
            Selected = null;
            HoverCell = null;
            HoverOverride = null;
            LastRejection = PlacementRejection.None;
            GhostOrientation = 0;
        }

        public bool SetTool(BuildingKind kind)
        {
            if (game.Sim.State != RunState.Day || game.Sim.StressMode) return false;
            if (!IsBuildable(kind)) return false;
            Tool = kind;
            Selected = null;
            return true;
        }

        /// <summary>A scene card kind that the current run also defines (a legacy six-night save has no release kinds).</summary>
        bool IsBuildable(BuildingKind kind)
        {
            if (game.Sim.Definitions.For(kind) == null) return false;
            foreach (var k in game.PurchasableKinds)
                if (k == kind) return true;
            return false;
        }

        static readonly Key[] DigitKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        static readonly Key[] NumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };

        public void CancelTool() => Tool = null;

        public void Select(Building b) => Selected = b;

        public void Tick(float dt)
        {
            var sim = game.Sim;
            var mouse = Mouse;
            var keyboard = Keyboard;

            if (Tool.HasValue && sim.State != RunState.Day) Tool = null;
            if (Selected != null && (Selected.Removed || sim.GetBuilding(Selected.Id) != Selected)) Selected = null;

            PointerPosition = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            PointerOverUi = mouse != null && IsOverUi(PointerPosition);
            if (HoverOverride.HasValue) HoverCell = HoverOverride;
            else if (mouse != null && !PointerOverUi && game.CameraRig.TryScreenToCell(PointerPosition, out var cell)) HoverCell = cell;
            else HoverCell = null;

            if (keyboard != null)
            {
                // Hotkey N selects the Nth build card (1 Wall, 2 Tower, 3 House, 4 Farm, 5 Trap, 6 Cannon, 7 Watchtower, 8 Repair).
                for (var i = 0; i < game.PurchasableKinds.Count && i < DigitKeys.Length; i++)
                    if (keyboard[DigitKeys[i]].wasPressedThisFrame || keyboard[NumpadKeys[i]].wasPressedThisFrame) SetTool(game.PurchasableKinds[i]);
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (Tool.HasValue) Tool = null;
                    else Selected = null;
                }

                if (keyboard.spaceKey.wasPressedThisFrame && sim.State == RunState.Night) game.TogglePause();
                if (keyboard.fKey.wasPressedThisFrame && game.IsMvp && !sim.StressMode) game.CycleGameSpeed(); // 1× → 2× → 3× → 1×
                if (keyboard.rKey.wasPressedThisFrame && sim.State == RunState.Day && game.IsMvp) RotateClockwise();
            }

            // Camera first: it decides whether this frame's right release ended a pan drag or was a plain click.
            game.CameraRig.Tick(keyboard, mouse, PointerOverUi, dt);
            if (mouse != null)
            {
                if (mouse.rightButton.wasReleasedThisFrame && !game.CameraRig.RightReleaseWasDrag) Tool = null; // right click cancels; right drag pans
                if (mouse.leftButton.wasPressedThisFrame && !PointerOverUi) Click();
            }
        }

        void Click()
        {
            var sim = game.Sim;
            if (!HoverCell.HasValue)
            {
                if (!Tool.HasValue) Selected = null;
                return;
            }

            var cell = HoverCell.Value;
            if (Tool.HasValue)
            {
                // Re-validated on click inside TryPlace; an invalid preview can never become a placement.
                LastRejection = sim.TryPlace(Tool.Value, cell, GhostOrientation, out _);
                if (LastRejection != PlacementRejection.None) game.Audio?.Play(SoundId.UiReject); // success sounds from the placement event
                return;
            }

            Selected = sim.Grid.GetOccupant(cell) ?? sim.Grid.GetTrap(cell);
        }

        /// <summary>R: rotates the active Tower/House/Farm ghost, or else the selected building (Walls auto-connect; Traps are directionless).</summary>
        public void RotateClockwise()
        {
            if (game.Sim.State != RunState.Day) return;
            if (Tool.HasValue)
            {
                if (RunSimulation.IsRotatable(Tool.Value)) GhostOrientation = (GhostOrientation + 1) & 3;
                return;
            }

            if (Selected != null) game.Sim.TryRotate(Selected);
        }

        bool IsOverUi(Vector2 position)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (pointerData == null || pointerDataOwner != es)
            {
                pointerData = new PointerEventData(es);
                pointerDataOwner = es;
            }

            pointerData.position = position;
            uiHits.Clear();
            es.RaycastAll(pointerData, uiHits);
            return uiHits.Count > 0;
        }
    }
}
