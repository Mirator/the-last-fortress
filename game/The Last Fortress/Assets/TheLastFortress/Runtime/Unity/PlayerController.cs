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
        }

        public bool SetTool(BuildingKind kind)
        {
            if (game.Sim.State != RunState.Day || game.Sim.StressMode) return false;
            Tool = kind;
            Selected = null;
            return true;
        }

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
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) SetTool(BuildingKind.Wall);
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) SetTool(BuildingKind.Tower);
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (Tool.HasValue) Tool = null;
                    else Selected = null;
                }

                if (keyboard.spaceKey.wasPressedThisFrame && sim.State == RunState.Night) game.TogglePause();
            }

            if (mouse != null)
            {
                if (mouse.rightButton.wasPressedThisFrame) Tool = null;
                if (mouse.leftButton.wasPressedThisFrame && !PointerOverUi) Click();
            }

            game.CameraRig.Tick(keyboard, mouse, PointerOverUi, dt);
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
                LastRejection = sim.TryPlace(Tool.Value, cell, out _);
                return;
            }

            Selected = sim.Grid.GetOccupant(cell);
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
