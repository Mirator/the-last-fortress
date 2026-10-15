using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Day-time route previews, construction ghost, range ring and selection marker.
    /// Uses the same planner as enemies; preview routes go through the read-only ghost overlay.
    /// </summary>
    public sealed class RouteOverlay : MonoBehaviour
    {
        const float LineY = 0.07f;

        PrototypeContent content;
        LineRenderer committedLine, primaryLine, rangeRing, selectionSquare;
        Transform dashRoot;
        readonly List<GameObject> dashes = new List<GameObject>();
        GameObject breachMarker;
        GameObject ghostRoot;
        GameObject ghostInvalidMark;
        readonly Dictionary<BuildingKind, GameObject> ghosts = new Dictionary<BuildingKind, GameObject>();
        Material ghostValid, ghostInvalid, dashMat;

        Route drawnPrimary, drawnCommitted;
        Route cachedPreview;
        (GridPos cell, BuildingKind kind, int revision, int reset)? previewKey;

        /// <summary>Route currently drawn as the main line (candidate if a valid ghost exists, else committed).</summary>
        public Route PrimaryRoute { get; private set; }
        public Route CommittedRoute { get; private set; }
        public Route CandidateRoute { get; private set; }
        public bool BreachMarkerVisible => breachMarker.activeSelf;
        public Vector3 BreachMarkerPosition => breachMarker.transform.position;
        public bool GhostVisible => ghostRoot.activeSelf;
        public bool GhostValid { get; private set; }
        public PlacementRejection GhostRejection { get; private set; }
        public int PreviewPlanCount { get; private set; }
        public int PrimaryLinePointCount => primaryLine.positionCount;
        public Vector3 PrimaryLinePoint(int i) => primaryLine.GetPosition(i);

        public void Initialize(PrototypeContent content)
        {
            this.content = content;
            committedLine = Prims.Line(transform, "CommittedRoute", content.overlay, Palette.RouteCyan.WithAlpha(0.35f), 0.07f);
            primaryLine = Prims.Line(transform, "RouteLine", content.overlay, Palette.RouteCyan, 0.14f);
            rangeRing = Prims.Line(transform, "RangeRing", content.overlay, Palette.RouteCyan.WithAlpha(0.8f), 0.05f);
            rangeRing.loop = true;
            selectionSquare = Prims.Line(transform, "Selection", content.overlay, Palette.RouteCyan, 0.06f);
            selectionSquare.loop = true;
            dashRoot = new GameObject("BreachRemainderDashes").transform;
            dashRoot.SetParent(transform, false);
            dashMat = Prims.Tinted(content.overlay, Palette.WarningCoral.WithAlpha(0.85f));
            ghostValid = Prims.Tinted(content.overlay, Palette.RouteCyan.WithAlpha(0.55f));
            ghostInvalid = Prims.Tinted(content.overlay, Palette.WarningCoral.WithAlpha(0.55f));
            BuildBreachMarker();
            BuildGhosts();
            HideAll();
        }

        void BuildBreachMarker()
        {
            breachMarker = new GameObject("BreachMarker");
            breachMarker.transform.SetParent(transform, false);
            var coral = Prims.Tinted(content.overlay, Palette.WarningCoral);
            var ring = Prims.Line(breachMarker.transform, "Ring", coral, Palette.WarningCoral, 0.07f);
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            const int n = 24;
            ring.positionCount = n;
            for (var i = 0; i < n; i++)
            {
                var a = i * Mathf.PI * 2f / n;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.55f, Mathf.Sin(a) * 0.55f, 0f));
            }

            // crosshair ticks
            Prims.Create(PrimitiveType.Cube, breachMarker.transform, new Vector3(0, 0, 0), new Vector3(1.4f, 0.03f, 0.07f), coral, "TickX");
            Prims.Create(PrimitiveType.Cube, breachMarker.transform, new Vector3(0, 0, 0), new Vector3(0.07f, 0.03f, 1.4f), coral, "TickZ");
            foreach (var r in breachMarker.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildGhosts()
        {
            ghostRoot = new GameObject("ConstructionGhost");
            ghostRoot.transform.SetParent(transform, false);
            foreach (var kind in new[] { BuildingKind.Wall, BuildingKind.Tower })
            {
                var g = Instantiate(content.PrefabFor(kind), ghostRoot.transform);
                g.name = $"Ghost{kind}";
                foreach (var r in g.GetComponentsInChildren<MeshRenderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.sharedMaterial = ghostValid;
                }

                ghosts[kind] = g;
            }

            ghostInvalidMark = new GameObject("InvalidCross");
            ghostInvalidMark.transform.SetParent(ghostRoot.transform, false);
            var coral = Prims.Tinted(content.overlay, Palette.WarningCoral);
            Prims.Create(PrimitiveType.Cube, ghostInvalidMark.transform, new Vector3(0, 0.03f, 0), new Vector3(1.0f, 0.04f, 0.1f), coral, "A",
                Quaternion.Euler(0, 45, 0));
            Prims.Create(PrimitiveType.Cube, ghostInvalidMark.transform, new Vector3(0, 0.03f, 0), new Vector3(1.0f, 0.04f, 0.1f), coral, "B",
                Quaternion.Euler(0, -45, 0));
        }

        public void HideAll()
        {
            committedLine.positionCount = 0;
            primaryLine.positionCount = 0;
            rangeRing.positionCount = 0;
            selectionSquare.positionCount = 0;
            foreach (var d in dashes) d.SetActive(false);
            breachMarker.SetActive(false);
            ghostRoot.SetActive(false);
            drawnPrimary = drawnCommitted = null;
            PrimaryRoute = CommittedRoute = CandidateRoute = null;
        }

        public void ResetCaches()
        {
            cachedPreview = null;
            previewKey = null;
            HideAll();
        }

        public void Refresh(RunSimulation sim, PlayerController player)
        {
            var day = sim.State == RunState.Day && !sim.StressMode;
            UpdateGhost(sim, player, day);

            if (!day)
            {
                committedLine.positionCount = 0;
                primaryLine.positionCount = 0;
                foreach (var d in dashes) d.SetActive(false);
                breachMarker.SetActive(false);
                drawnPrimary = drawnCommitted = null;
                PrimaryRoute = CommittedRoute = CandidateRoute = null;
            }
            else
            {
                CommittedRoute = sim.GetLiveRoute(WavePlan.MainEntry); // cached per grid revision
                CandidateRoute = GhostValid ? cachedPreview : null;
                PrimaryRoute = CandidateRoute ?? CommittedRoute;
                if (!ReferenceEquals(drawnPrimary, PrimaryRoute))
                {
                    DrawPrimary(PrimaryRoute);
                    drawnPrimary = PrimaryRoute;
                }

                var faint = CandidateRoute != null ? CommittedRoute : null;
                if (!ReferenceEquals(drawnCommitted, faint))
                {
                    SetLine(committedLine, faint, faint == null ? 0 : faint.ReachableEndIndex);
                    drawnCommitted = faint;
                }
            }

            UpdateRangeAndSelection(sim, player);
        }

        void UpdateGhost(RunSimulation sim, PlayerController player, bool day)
        {
            GhostValid = false;
            GhostRejection = PlacementRejection.None;
            if (!day || !player.Tool.HasValue || !player.HoverCell.HasValue)
            {
                if (ghostRoot.activeSelf) ghostRoot.SetActive(false);
                return;
            }

            var kind = player.Tool.Value;
            var cell = player.HoverCell.Value;
            GhostRejection = sim.CheckPlacement(kind, cell);
            GhostValid = GhostRejection == PlacementRejection.None;
            if (!ghostRoot.activeSelf) ghostRoot.SetActive(true);
            ghostRoot.transform.position = Prims.CellCenter(cell, 0.01f);
            foreach (var pair in ghosts)
            {
                var active = pair.Key == kind;
                if (pair.Value.activeSelf != active) pair.Value.SetActive(active);
                if (!active) continue;
                var mat = GhostValid ? ghostValid : ghostInvalid;
                foreach (var r in pair.Value.GetComponentsInChildren<MeshRenderer>())
                    if (r.sharedMaterial != mat) r.sharedMaterial = mat;
            }

            ghostInvalidMark.SetActive(!GhostValid);

            if (!GhostValid) return; // invalid ghosts keep the committed route visible; no preview planning
            var key = (cell, kind, sim.Grid.Revision, sim.ResetCount);
            if (previewKey.HasValue && previewKey.Value.Equals(key)) return;
            previewKey = key;
            cachedPreview = sim.PreviewRoute(WavePlan.MainEntry, cell);
            PreviewPlanCount++;
        }

        void DrawPrimary(Route route)
        {
            foreach (var d in dashes) d.SetActive(false);
            if (route == null || !route.Found)
            {
                primaryLine.positionCount = 0;
                breachMarker.SetActive(false);
                return;
            }

            SetLine(primaryLine, route, route.ReachableEndIndex);
            if (!route.IsBreach)
            {
                breachMarker.SetActive(false);
                return;
            }

            breachMarker.SetActive(true);
            breachMarker.transform.position = Prims.CellCenter(route.Blocker, 0.06f);
            // dashed hypothetical remainder: from the attack cell, through the blocker, to the Keep neighbor
            var n = 0;
            for (var i = route.ReachableEndIndex; i < route.Cells.Count - 1; i++)
            {
                var a = Prims.CellCenter(route.Cells[i], LineY);
                var b = Prims.CellCenter(route.Cells[i + 1], LineY);
                for (var k = 0; k < 3; k++)
                {
                    var t0 = (k * 2 + 0.5f) / 6f;
                    var t1 = (k * 2 + 1.5f) / 6f;
                    var dash = GetDash(n++);
                    var p0 = Vector3.Lerp(a, b, t0);
                    var p1 = Vector3.Lerp(a, b, t1);
                    dash.transform.position = (p0 + p1) * 0.5f;
                    dash.transform.rotation = Quaternion.LookRotation(p1 - p0);
                    dash.transform.localScale = new Vector3(0.09f, 0.02f, (p1 - p0).magnitude);
                }
            }
        }

        GameObject GetDash(int index)
        {
            while (dashes.Count <= index)
            {
                var d = Prims.Create(PrimitiveType.Cube, dashRoot, Vector3.zero, Vector3.one, dashMat, "Dash");
                d.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dashes.Add(d);
            }

            dashes[index].SetActive(true);
            return dashes[index];
        }

        static void SetLine(LineRenderer line, Route route, int lastIndex)
        {
            if (route == null || !route.Found || lastIndex < 0)
            {
                line.positionCount = 0;
                return;
            }

            line.positionCount = lastIndex + 1;
            for (var i = 0; i <= lastIndex; i++) line.SetPosition(i, Prims.CellCenter(route.Cells[i], LineY));
        }

        void UpdateRangeAndSelection(RunSimulation sim, PlayerController player)
        {
            GridPos? rangeCenter = null;
            if (ghostRoot.activeSelf && player.Tool == BuildingKind.Tower && player.HoverCell.HasValue) rangeCenter = player.HoverCell;
            else if (player.Selected != null && player.Selected.Kind == BuildingKind.Tower) rangeCenter = player.Selected.Cell;

            if (rangeCenter.HasValue)
            {
                const int n = 64;
                var r = (float)sim.Definitions.Tower.Range;
                rangeRing.positionCount = n;
                var c = Prims.CellCenter(rangeCenter.Value, 0.05f);
                for (var i = 0; i < n; i++)
                {
                    var a = i * Mathf.PI * 2f / n;
                    rangeRing.SetPosition(i, c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                }
            }
            else rangeRing.positionCount = 0;

            if (player.Selected != null)
            {
                var c = Prims.CellCenter(player.Selected.Cell, 0.05f);
                selectionSquare.positionCount = 4;
                selectionSquare.SetPosition(0, c + new Vector3(-0.5f, 0, -0.5f));
                selectionSquare.SetPosition(1, c + new Vector3(-0.5f, 0, 0.5f));
                selectionSquare.SetPosition(2, c + new Vector3(0.5f, 0, 0.5f));
                selectionSquare.SetPosition(3, c + new Vector3(0.5f, 0, -0.5f));
            }
            else selectionSquare.positionCount = 0;
        }
    }
}
