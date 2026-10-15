using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Day-time route previews, construction ghost, range ring, selection marker and synergy links.
    /// Uses the same planner as enemies; preview routes go through the read-only ghost overlay.
    /// The first active entry gets the full treatment (faint committed line + dashed breach remainder); further
    /// entries of the upcoming night get their own route line and breach marker.
    /// </summary>
    public sealed class RouteOverlay : MonoBehaviour
    {
        const float LineY = 0.07f;
        const int MaxEntries = 4;

        PrototypeContent content;
        LineRenderer committedLine, primaryLine, rangeRing, selectionSquare;
        readonly LineRenderer[] extraLines = new LineRenderer[MaxEntries];
        readonly GameObject[] extraBreach = new GameObject[MaxEntries];
        readonly Route[] extraDrawn = new Route[MaxEntries];
        readonly Route[] extraPreview = new Route[MaxEntries];
        readonly List<LineRenderer> links = new List<LineRenderer>();
        Transform dashRoot;
        readonly List<GameObject> dashes = new List<GameObject>();
        GameObject breachMarker;
        GameObject ghostRoot;
        GameObject ghostInvalidMark;
        readonly Dictionary<BuildingKind, GameObject> ghosts = new Dictionary<BuildingKind, GameObject>();
        readonly GameObject[] wallGhosts = new GameObject[6];
        Material ghostValid, ghostInvalid, dashMat, coralMat, farmLinkMat, trapLinkMat, lineMat;

        Route drawnPrimary, drawnCommitted;
        Route cachedPreview;
        (GridPos cell, BuildingKind kind, int revision, int reset, int night)? previewKey;
        (int revision, int reset, int traps)? linksKey;

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

        /// <summary>Connected variant currently shown by the Wall ghost.</summary>
        public WallShape GhostWallShape { get; private set; }

        /// <summary>Visible synergy links (Farm-House and Wall-Trap pairs).</summary>
        public int VisibleLinkCount { get; private set; }

        /// <summary>Breach markers of additional entries (index 1..), for screen-space labels.</summary>
        public IEnumerable<Vector3> ExtraBreachPositions()
        {
            foreach (var m in extraBreach)
                if (m != null && m.activeSelf) yield return m.transform.position;
        }

        public void Initialize(PrototypeContent content, IReadOnlyList<BuildingKind> buildable)
        {
            this.content = content;
            // Route/breach/selection/link lines may use an on-top material so tall Towers never hide them (MVP).
            lineMat = content.overlayOnTop != null ? content.overlayOnTop : content.overlay;
            committedLine = Prims.Line(transform, "CommittedRoute", lineMat, Palette.RouteCyan.WithAlpha(0.35f), 0.07f);
            primaryLine = Prims.Line(transform, "RouteLine", lineMat, Palette.RouteCyan, 0.14f);
            rangeRing = Prims.Line(transform, "RangeRing", lineMat, Palette.RouteCyan.WithAlpha(0.8f), 0.05f);
            rangeRing.loop = true;
            selectionSquare = Prims.Line(transform, "Selection", lineMat, Palette.RouteCyan, 0.06f);
            selectionSquare.loop = true;
            dashRoot = new GameObject("BreachRemainderDashes").transform;
            dashRoot.SetParent(transform, false);
            dashMat = Prims.Tinted(lineMat, Palette.WarningCoral.WithAlpha(0.85f));
            coralMat = Prims.Tinted(lineMat, Palette.WarningCoral);
            ghostValid = Prims.Tinted(content.overlay, Palette.RouteCyan.WithAlpha(0.55f));
            ghostInvalid = Prims.Tinted(content.overlay, Palette.WarningCoral.WithAlpha(0.55f));
            farmLinkMat = Prims.Tinted(lineMat, Palette.Amber);
            trapLinkMat = Prims.Tinted(lineMat, Palette.HudText);
            breachMarker = BuildBreachMarker("BreachMarker");
            for (var i = 1; i < MaxEntries; i++)
            {
                extraLines[i] = Prims.Line(transform, $"RouteLine{i}", lineMat, Palette.RouteCyan.WithAlpha(0.8f), 0.1f);
                extraBreach[i] = BuildBreachMarker($"BreachMarker{i}");
            }

            BuildGhosts(buildable);
            HideAll();
        }

        GameObject BuildBreachMarker(string name)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(transform, false);
            var ring = Prims.Line(marker.transform, "Ring", coralMat, Palette.WarningCoral, 0.09f);
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
            Prims.Create(PrimitiveType.Cube, marker.transform, new Vector3(0, 0, 0), new Vector3(1.4f, 0.03f, 0.07f), coralMat, "TickX");
            Prims.Create(PrimitiveType.Cube, marker.transform, new Vector3(0, 0, 0), new Vector3(0.07f, 0.03f, 1.4f), coralMat, "TickZ");
            foreach (var r in marker.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return marker;
        }

        void BuildGhosts(IReadOnlyList<BuildingKind> buildable)
        {
            ghostRoot = new GameObject("ConstructionGhost");
            ghostRoot.transform.SetParent(transform, false);
            foreach (var kind in buildable)
            {
                var g = Instantiate(content.PrefabFor(kind), ghostRoot.transform);
                g.name = $"Ghost{kind}";
                g.transform.localPosition = FootprintOffset(kind);
                foreach (var r in g.GetComponentsInChildren<MeshRenderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.sharedMaterial = ghostValid;
                }

                if (kind == BuildingKind.Wall && content.wallShapePrefabs != null && content.wallShapePrefabs.Length == 6)
                {
                    // The Wall ghost shows its own hypothetical connected variant; committed Walls are not touched.
                    for (var s = 0; s < 6; s++)
                    {
                        var v = Instantiate(content.wallShapePrefabs[s], g.transform);
                        v.name = $"Variant{(WallShape)s}";
                        foreach (var r in v.GetComponentsInChildren<MeshRenderer>())
                        {
                            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            r.sharedMaterial = ghostValid;
                        }

                        wallGhosts[s] = v;
                    }

                    foreach (Transform child in g.transform)
                        if (System.Array.IndexOf(wallGhosts, child.gameObject) < 0) child.gameObject.SetActive(false);
                }

                // Footprint outline so multi-cell ghosts show every cell they would occupy.
                var size = BuildingDefinition.FootprintSizeOf(kind);
                if (size > 1)
                {
                    for (var dx = 0; dx < size; dx++)
                    for (var dz = 0; dz < size; dz++)
                        Prims.Create(PrimitiveType.Cube, g.transform, new Vector3(dx - (size - 1) / 2f, 0.02f, dz - (size - 1) / 2f) - Vector3.zero,
                                new Vector3(0.9f, 0.02f, 0.9f), ghostValid, "FootprintCell")
                            .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                ghosts[kind] = g;
            }

            ghostInvalidMark = new GameObject("InvalidCross");
            ghostInvalidMark.transform.SetParent(ghostRoot.transform, false);
            Prims.Create(PrimitiveType.Cube, ghostInvalidMark.transform, new Vector3(0, 0.03f, 0), new Vector3(1.0f, 0.04f, 0.1f), coralMat, "A",
                Quaternion.Euler(0, 45, 0));
            Prims.Create(PrimitiveType.Cube, ghostInvalidMark.transform, new Vector3(0, 0.03f, 0), new Vector3(1.0f, 0.04f, 0.1f), coralMat, "B",
                Quaternion.Euler(0, -45, 0));
        }

        /// <summary>Offset from the anchor cell center to the footprint center (Farm: half a tile north-east).</summary>
        public static Vector3 FootprintOffset(BuildingKind kind)
        {
            var half = (BuildingDefinition.FootprintSizeOf(kind) - 1) / 2f;
            return new Vector3(half, 0f, half);
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
            HideExtras();
            HideLinks();
            drawnPrimary = drawnCommitted = null;
            PrimaryRoute = CommittedRoute = CandidateRoute = null;
        }

        void HideExtras()
        {
            for (var i = 1; i < MaxEntries; i++)
            {
                extraLines[i].positionCount = 0;
                extraBreach[i].SetActive(false);
                extraDrawn[i] = null;
            }
        }

        void HideLinks()
        {
            foreach (var l in links) l.positionCount = 0;
            VisibleLinkCount = 0;
            linksKey = null;
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
                HideExtras();
                HideLinks();
                drawnPrimary = drawnCommitted = null;
                PrimaryRoute = CommittedRoute = CandidateRoute = null;
            }
            else
            {
                var entries = sim.Wave.EntryCells;
                CommittedRoute = sim.GetLiveRoute(entries[0]); // cached per grid revision
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

                for (var i = 1; i < MaxEntries; i++)
                {
                    var route = i < entries.Count ? (GhostValid && extraPreview[i] != null ? extraPreview[i] : sim.GetLiveRoute(entries[i])) : null;
                    if (ReferenceEquals(extraDrawn[i], route)) continue;
                    extraDrawn[i] = route;
                    SetLine(extraLines[i], route, route == null || !route.Found ? -1 : route.ReachableEndIndex);
                    var breach = route != null && route.Found && route.IsBreach;
                    extraBreach[i].SetActive(breach);
                    if (breach) extraBreach[i].transform.position = Prims.CellCenter(route.Blocker, 0.06f);
                }

                UpdateLinks(sim);
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

            ghostInvalidMark.transform.localPosition = FootprintOffset(kind);
            ghostInvalidMark.SetActive(!GhostValid);
            if (RunSimulation.IsRotatable(kind)) ghosts[kind].transform.localRotation = Quaternion.Euler(0, player.GhostOrientation * 90f, 0);
            if (kind == BuildingKind.Wall && wallGhosts[0] != null)
            {
                WallTopology.Classify(WallTopology.Mask(sim.Grid, cell), out var shape, out var turns);
                GhostWallShape = shape;
                for (var s = 0; s < 6; s++)
                    if (wallGhosts[s].activeSelf != (s == (int)shape)) wallGhosts[s].SetActive(s == (int)shape);
                wallGhosts[(int)shape].transform.localRotation = Quaternion.Euler(0, turns * 90f, 0);
            }

            if (!GhostValid) return; // invalid ghosts keep the committed route visible; no preview planning
            var key = (cell, kind, sim.Grid.Revision, sim.ResetCount, sim.NightNumber);
            if (previewKey.HasValue && previewKey.Value.Equals(key)) return;
            previewKey = key;
            var entries = sim.Wave.EntryCells;
            cachedPreview = sim.PreviewRoute(entries[0], kind, cell);
            for (var i = 1; i < MaxEntries; i++)
                extraPreview[i] = i < entries.Count ? sim.PreviewRoute(entries[i], kind, cell) : null;
            PreviewPlanCount++;
        }

        void UpdateLinks(RunSimulation sim)
        {
            if (!sim.IsCampaign)
            {
                if (VisibleLinkCount > 0) HideLinks();
                return;
            }

            var key = (sim.Grid.Revision, sim.ResetCount, sim.Grid.TrapRevision);
            if (linksKey.HasValue && linksKey.Value.Equals(key)) return;
            linksKey = key;

            var n = 0;
            var pairs = 0;
            foreach (var (farm, house) in EconomyRules.FarmHousePairs(sim.Grid))
            {
                pairs++;
                foreach (var cell in farm.Cells)
                foreach (var d in GridPos.CardinalOrder)
                    if (house.Covers(cell + d)) SetEdge(n++, cell, d, farmLinkMat, Palette.Amber);
            }

            foreach (var b in sim.Grid.AllBuildings())
            {
                if (b.Kind != BuildingKind.Wall || !b.IsAlive) continue;
                foreach (var d in GridPos.CardinalOrder)
                    if (sim.Grid.HasTrap(b.Cell + d))
                    {
                        pairs++;
                        SetEdge(n++, b.Cell, d, trapLinkMat, Palette.HudText);
                    }
            }

            for (var i = n; i < links.Count; i++) links[i].positionCount = 0;
            VisibleLinkCount = pairs;
        }

        /// <summary>Highlights the shared edge between a cell and its neighbor, raised above low buildings so it stays visible.</summary>
        void SetEdge(int index, GridPos cell, GridPos dir, Material mat, Color color)
        {
            const float y = 0.9f;
            var mid = Prims.CellCenter(cell, y) + new Vector3(dir.X * 0.5f, 0f, dir.Z * 0.5f);
            var along = new Vector3(dir.Z, 0f, dir.X) * 0.42f;
            while (links.Count <= index) links.Add(Prims.Line(transform, "SynergyLink", mat, color, 0.12f));
            var l = links[index];
            l.sharedMaterial = mat;
            l.startColor = l.endColor = color;
            l.positionCount = 2;
            l.SetPosition(0, mid - along);
            l.SetPosition(1, mid + along);
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
                var r = (float)(sim.Definitions.Tower.Range * (sim.HasPerk(PerkRules.ExpandedVision) ? PerkRules.ExpandedVisionFactor : 1.0));
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
                var size = BuildingDefinition.FootprintSizeOf(player.Selected.Kind);
                var min = new Vector3(player.Selected.Cell.X, 0.05f, player.Selected.Cell.Z);
                selectionSquare.positionCount = 4;
                selectionSquare.SetPosition(0, min);
                selectionSquare.SetPosition(1, min + new Vector3(0, 0, size));
                selectionSquare.SetPosition(2, min + new Vector3(size, 0, size));
                selectionSquare.SetPosition(3, min + new Vector3(size, 0, 0));
            }
            else selectionSquare.positionCount = 0;
        }
    }
}
