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
        LineRenderer committedLine, primaryLine, rangeRing, selectionSquare, reachSquare;
        readonly List<LineRenderer> buffMarks = new List<LineRenderer>();
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

        /// <summary>Radius of the drawn range ring (0 when hidden): the effective range of the hovered/selected attacker.</summary>
        public float RangeRingRadius { get; private set; }

        /// <summary>Towers outlined because the Watchtower ghost or selection boosts them.</summary>
        public int BuffMarkCount { get; private set; }

        /// <summary>Whether the Repair Station reach square is drawn.</summary>
        public bool ReachVisible => reachSquare.positionCount > 0;

        /// <summary>Breach markers of additional entries (index 1..), for screen-space labels.</summary>
        public IEnumerable<Vector3> ExtraBreachPositions()
        {
            foreach (var m in extraBreach)
                if (m != null && m.activeSelf) yield return m.transform.position;
        }

        // ---------- entry markers and route direction (plan 010 stage 3; no gate structures since plan 016) ----------

        readonly GameObject[] entryMarkers = new GameObject[MaxEntries];
        readonly Transform[] entryPads = new Transform[MaxEntries];
        readonly Vector3[] entryPadRest = new Vector3[MaxEntries];
        readonly Light[] roadTorches = new Light[MaxEntries];

        /// <summary>0 by day, 1 at night: the road torches light up so the four ways out of the mist stay readable (plan 016).</summary>
        public float NightGlow;
        Transform chevronRoot;
        readonly List<Transform> chevrons = new List<Transform>();
        Material chevronMat;

        const float ChevronSpacing = 1.4f;
        const float ChevronSpeed = 0.9f; // tiles per second toward the Keep

        /// <summary>Entry markers shown at tonight's entries (MVP/release), for tests and captures.</summary>
        public int VisibleEntryCount { get; private set; }

        public Vector3 EntryMarkerPosition(int i) => entryMarkers[i].transform.position;

        /// <summary>Chevrons currently drawn along the route lines, pointing from the entries toward the Keep.</summary>
        public int VisibleChevronCount { get; private set; }

        public Pose Chevron(int i) => new Pose(chevrons[i].position, chevrons[i].rotation);

        /// <summary>Unit step from a boundary-ring entry cell out of the board.</summary>
        public static Vector3 Outward(GridPos entry)
        {
            var max = GridCoordinates.Size - 1;
            if (entry.X == 0) return Vector3.left;
            if (entry.X == max) return Vector3.right;
            return entry.Z == 0 ? Vector3.back : Vector3.forward;
        }

        const float RoadMouthOut = 0.85f; // from the entry cell centre: past the board edge (0.5), on its road

        /// <summary>Where an entry's road meets the board, where its screen label is set beside it.</summary>
        public static Vector3 RoadMouth(GridPos entry) => Prims.CellCenter(entry, 0.6f) + Outward(entry) * RoadMouthOut;

        /// <summary>
        /// The marker of an entry (plan 016: no gate structure, enemies simply come down the road out of the mist): a coral
        /// pad on the entry cell, where they step onto the board, and a torch light on the road mouth that glows at Night.
        /// </summary>
        GameObject BuildEntryMarker(int i)
        {
            var marker = new GameObject($"EntryMarker{i}");
            marker.transform.SetParent(transform, false);
            var torch = new GameObject("RoadTorch");
            torch.transform.SetParent(marker.transform, false);
            torch.transform.localPosition = new Vector3(0f, 0.55f, -0.15f); // just outside, lighting the road out of the mist
            roadTorches[i] = torch.AddComponent<Light>();
            roadTorches[i].type = LightType.Point;
            roadTorches[i].color = content.art != null ? content.art.torchColor : Palette.Amber;
            roadTorches[i].range = 1.8f;
            roadTorches[i].intensity = 0f;
            roadTorches[i].shadows = LightShadows.None;
            var coral = Prims.Tinted(content.timberShadow, Palette.WarningCoral);
            var pad = Prims.Create(PrimitiveType.Cube, marker.transform, new Vector3(0, 0.012f, RoadMouthOut), new Vector3(0.9f, 0.02f, 0.9f), coral, "EntryPad");
            pad.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            entryPads[i] = pad.transform; // a new side's pad pulses
            entryPadRest[i] = pad.transform.localScale;
            marker.SetActive(false);
            return marker;
        }

        GameObject BuildChevron()
        {
            var c = new GameObject("Chevron");
            c.transform.SetParent(chevronRoot, false);
            foreach (var side in new[] { -1f, 1f })
                Prims.Create(PrimitiveType.Cube, c.transform, new Vector3(side * 0.13f, 0, -0.13f), new Vector3(0.08f, 0.02f, 0.44f), chevronMat, "Arm",
                    Quaternion.Euler(0, -side * 40f, 0)).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return c;
        }

        /// <summary>Markers at tonight's entries by Day and Night; a new side's pad pulses during the Day.</summary>
        void UpdateEntryMarkers(RunSimulation sim)
        {
            VisibleEntryCount = 0;
            if (entryMarkers[0] == null) return;
            var show = sim.IsCampaign && !sim.StressMode && (sim.State == RunState.Day || sim.State == RunState.Night);
            var entries = sim.Wave.EntryCells;
            for (var i = 0; i < MaxEntries; i++)
            {
                var active = show && i < entries.Count;
                if (entryMarkers[i].activeSelf != active) entryMarkers[i].SetActive(active);
                if (!active) continue;
                VisibleEntryCount++;
                var cell = entries[i];
                var outward = Outward(cell);
                // 0.5 to the board edge, then onto its road: the torch never stands on a legal cell (the pad is on the entry).
                entryMarkers[i].transform.position = Prims.CellCenter(cell) + outward * RoadMouthOut;
                entryMarkers[i].transform.rotation = Quaternion.LookRotation(-outward);
                if (roadTorches[i] != null) roadTorches[i].intensity = 1.6f * NightGlow;
                var pulse = sim.State == RunState.Day && HudView.IsNewEntry(sim, cell) ? 1f + 0.18f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;
                if (entryPads[i] != null) entryPads[i].localScale = Vector3.Scale(entryPadRest[i], new Vector3(pulse, 1f, pulse));
            }
        }

        /// <summary>Chevrons drift along each drawn route line from its entry toward the Keep (Day only, like the lines).</summary>
        void UpdateChevrons(bool day)
        {
            var used = 0;
            if (day && chevronRoot != null)
            {
                var phase = Time.unscaledTime * ChevronSpeed % ChevronSpacing;
                used = PlaceChevrons(primaryLine, phase, used);
                for (var i = 1; i < MaxEntries; i++) used = PlaceChevrons(extraLines[i], phase, used);
            }

            for (var i = used; i < chevrons.Count; i++)
                if (chevrons[i].gameObject.activeSelf) chevrons[i].gameObject.SetActive(false);
            VisibleChevronCount = used;
        }

        int PlaceChevrons(LineRenderer line, float phase, int used)
        {
            var travelled = 0f;
            var next = phase;
            for (var k = 0; k + 1 < line.positionCount; k++)
            {
                var a = line.GetPosition(k);
                var b = line.GetPosition(k + 1);
                var length = Vector3.Distance(a, b);
                if (length < 1e-4f) continue;
                var dir = (b - a) / length;
                while (next <= travelled + length)
                {
                    if (used == chevrons.Count) chevrons.Add(BuildChevron().transform);
                    var c = chevrons[used++];
                    if (!c.gameObject.activeSelf) c.gameObject.SetActive(true);
                    c.position = a + dir * (next - travelled) + new Vector3(0, 0.012f, 0);
                    c.rotation = Quaternion.LookRotation(dir);
                    next += ChevronSpacing;
                }

                travelled += length;
            }

            return used;
        }

        public void Initialize(PrototypeContent content, IReadOnlyList<BuildingKind> buildable)
        {
            this.content = content;
            // Route/breach/selection/link lines may use an on-top material so tall Towers never hide them (MVP).
            lineMat = content.overlayOnTop != null ? content.overlayOnTop : content.overlay;
            // A dark under-stroke drawn just before each route line, so the cyan reads over pale water and sand too.
            underMat = new Material(lineMat) { name = lineMat.name + " (under-stroke)", renderQueue = lineMat.renderQueue - 1 };
            committedLine = Prims.Line(transform, "CommittedRoute", lineMat, Palette.RouteCyan.WithAlpha(0.35f), 0.07f);
            primaryLine = Prims.Line(transform, "RouteLine", lineMat, Palette.RouteCyan, 0.14f);
            UnderStroke(primaryLine);
            rangeRing = Prims.Line(transform, "RangeRing", lineMat, Palette.RouteCyan.WithAlpha(0.8f), 0.05f);
            rangeRing.loop = true;
            selectionSquare = Prims.Line(transform, "Selection", lineMat, Palette.RouteCyan, 0.06f);
            selectionSquare.loop = true;
            reachSquare = Prims.Line(transform, "RepairReach", lineMat, Palette.RouteCyan.WithAlpha(0.6f), 0.05f);
            reachSquare.loop = true;
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
                UnderStroke(extraLines[i]);
                extraBreach[i] = BuildBreachMarker($"BreachMarker{i}");
            }

            if (content.IsMvp)
            {
                for (var i = 0; i < MaxEntries; i++) entryMarkers[i] = BuildEntryMarker(i);
                chevronRoot = new GameObject("RouteChevrons").transform;
                chevronRoot.SetParent(transform, false);
                chevronMat = Prims.Tinted(lineMat, Palette.HudText);
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
                if (kind == BuildingKind.Cannon && content.compactCannonPrefab != null)
                {
                    // The release-rules-4 field gun, shown instead of the 2 x 2 parts when the run's Cannon is 1 x 1.
                    var compact = Instantiate(content.compactCannonPrefab, g.transform);
                    compact.name = "Compact";
                    compact.SetActive(false);
                }

                foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true))
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
        public static Vector3 FootprintOffset(BuildingKind kind) => FootprintOffset(BuildingDefinition.FootprintSizeOf(kind));

        public static Vector3 FootprintOffset(int size)
        {
            var half = (size - 1) / 2f;
            return new Vector3(half, 0f, half);
        }

        /// <summary>
        /// Drawn size of a building's model against its art: a release-rules-4 Cannon stands on one tile, so without its own
        /// field-gun model (<see cref="PrototypeContent.compactCannonPrefab"/>) the 2 x 2 model is shown at half size. 1 for
        /// everything else.
        /// </summary>
        public static float ModelScale(BuildingDefinition def) =>
            def != null && def.Kind == BuildingKind.Cannon && def.FootprintSize < BuildingDefinition.FootprintSizeOf(BuildingKind.Cannon) ? 0.5f : 1f;

        public void HideAll()
        {
            committedLine.positionCount = 0;
            primaryLine.positionCount = 0;
            rangeRing.positionCount = 0;
            selectionSquare.positionCount = 0;
            reachSquare.positionCount = 0;
            foreach (var m in buffMarks) m.positionCount = 0;
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

            SyncUnderStrokes();
            UpdateEntryMarkers(sim);
            UpdateChevrons(day);
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

            var ghostDef = sim.Definitions.For(kind);
            var ghostSize = ghostDef?.FootprintSize ?? BuildingDefinition.FootprintSizeOf(kind);
            ghostInvalidMark.transform.localPosition = FootprintOffset(ghostSize);
            // The ghost follows the run's own footprint (a release-rules-4 Cannon is 1 x 1: its field gun, or the 2 x 2
            // model at half size).
            var ghost = ghosts[kind].transform;
            var compactGhost = content.CompactCannon(ghostDef);
            ghost.localPosition = FootprintOffset(ghostSize);
            ghost.localScale = Vector3.one * (compactGhost ? 1f : ModelScale(ghostDef));
            foreach (Transform child in ghost)
            {
                var show = child.name == "FootprintCell" ? ghostSize > 1
                    : kind != BuildingKind.Cannon ? child.gameObject.activeSelf // other kinds manage their own parts (Wall variants)
                    : child.name == "Compact" ? compactGhost : !compactGhost;
                if (child.gameObject.activeSelf != show) child.gameObject.SetActive(show);
            }
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

        /// <summary>Outline of a size x size cell square from its lowest corner cell, inset by <paramref name="inset"/>.</summary>
        static void Square(LineRenderer line, GridPos min, int size, float inset)
        {
            var a = new Vector3(min.X + inset, 0.06f, min.Z + inset);
            var s = size - inset * 2f;
            line.positionCount = 4;
            line.SetPosition(0, a);
            line.SetPosition(1, a + new Vector3(0, 0, s));
            line.SetPosition(2, a + new Vector3(s, 0, s));
            line.SetPosition(3, a + new Vector3(s, 0, 0));
        }

        Material underMat;
        readonly List<(LineRenderer line, LineRenderer under)> underStrokes = new List<(LineRenderer, LineRenderer)>();
        Vector3[] strokeBuffer = new Vector3[64];

        /// <summary>Route line contrast: a darker, wider copy of the line drawn under it.</summary>
        void UnderStroke(LineRenderer line)
        {
            var under = Prims.Line(transform, line.name + "Under", underMat, new Color(0.06f, 0.11f, 0.13f, 0.6f), line.startWidth + 0.06f);
            underStrokes.Add((line, under));
        }

        /// <summary>Copies each route line's points to its under-stroke (after every route update).</summary>
        void SyncUnderStrokes()
        {
            foreach (var (line, under) in underStrokes)
            {
                var n = line.positionCount;
                if (strokeBuffer.Length < n) strokeBuffer = new Vector3[n * 2];
                line.GetPositions(strokeBuffer);
                under.positionCount = n;
                if (n > 0) under.SetPositions(strokeBuffer);
                under.enabled = line.enabled && line.gameObject.activeInHierarchy;
            }
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

        static bool IsAttacker(BuildingKind kind) => kind == BuildingKind.Tower || kind == BuildingKind.Cannon;

        void UpdateRangeAndSelection(RunSimulation sim, PlayerController player)
        {
            // Range ring from Core values: a ghost shows its base range (plus a Watchtower it would touch is not guessed),
            // a placed Archer Tower/Cannon its effective range including perks and any Watchtower bonus.
            Vector3? rangeCenter = null;
            var radius = 0f;
            var ghostKind = ghostRoot.activeSelf && player.Tool.HasValue && player.HoverCell.HasValue ? player.Tool : null;
            if (ghostKind.HasValue && IsAttacker(ghostKind.Value))
            {
                rangeCenter = Prims.CellCenter(player.HoverCell.Value, 0.05f) + FootprintOffset(sim.Definitions.For(ghostKind.Value)?.FootprintSize ?? BuildingDefinition.FootprintSizeOf(ghostKind.Value));
                radius = (float)sim.TowerRange(sim.Definitions.For(ghostKind.Value));
            }
            else if (player.Selected != null && IsAttacker(player.Selected.Kind))
            {
                rangeCenter = new Vector3((float)player.Selected.CenterX, 0.05f, (float)player.Selected.CenterZ);
                radius = (float)sim.TowerRange(player.Selected);
            }

            RangeRingRadius = rangeCenter.HasValue ? radius : 0f;
            if (rangeCenter.HasValue)
            {
                const int n = 64;
                rangeRing.positionCount = n;
                for (var i = 0; i < n; i++)
                {
                    var a = i * Mathf.PI * 2f / n;
                    rangeRing.SetPosition(i, rangeCenter.Value + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
                }
            }
            else rangeRing.positionCount = 0;

            // Watchtower: outline every tower the ghost (or the selected Watchtower) would boost.
            GridPos? watch = null;
            if (ghostKind == BuildingKind.Watchtower && GhostValid) watch = player.HoverCell;
            else if (player.Selected != null && player.Selected.Kind == BuildingKind.Watchtower) watch = player.Selected.Cell;
            var marks = 0;
            if (watch.HasValue)
                foreach (var t in sim.TowersBuffedBy(watch.Value))
                {
                    while (buffMarks.Count <= marks)
                    {
                        var line = Prims.Line(transform, "WatchtowerBoost", lineMat, Palette.Amber, 0.07f);
                        line.loop = true;
                        buffMarks.Add(line);
                    }

                    Square(buffMarks[marks++], t.Cell, t.Definition.FootprintSize, 0.1f);
                }

            for (var i = marks; i < buffMarks.Count; i++) buffMarks[i].positionCount = 0;
            BuffMarkCount = marks;

            // Repair Station: the square it repairs at dawn (Chebyshev reach around its footprint).
            GridPos? station = null;
            if (ghostKind == BuildingKind.RepairStation) station = player.HoverCell;
            else if (player.Selected != null && player.Selected.Kind == BuildingKind.RepairStation) station = player.Selected.Cell;
            if (station.HasValue && sim.Definitions.RepairStation != null)
            {
                var reach = sim.Definitions.RepairStation.RepairReach;
                Square(reachSquare, new GridPos(station.Value.X - reach, station.Value.Z - reach), 2 + reach * 2, 0f);
            }
            else reachSquare.positionCount = 0;

            if (player.Selected != null)
            {
                var size = player.Selected.Definition.FootprintSize;
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
