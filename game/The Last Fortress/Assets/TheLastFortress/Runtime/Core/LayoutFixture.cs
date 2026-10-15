using System.Collections.Generic;

namespace TheLastFortress
{
    public readonly struct Placement
    {
        public readonly BuildingKind Kind;
        public readonly GridPos Cell;

        public Placement(BuildingKind kind, GridPos cell)
        {
            Kind = kind;
            Cell = cell;
        }

        public override string ToString() => $"{Kind}{Cell}";
    }

    /// <summary>A reproducible list of placements applied to a fresh Day.</summary>
    public sealed class LayoutFixture
    {
        public readonly string Name;
        public readonly IReadOnlyList<Placement> Placements;

        public LayoutFixture(string name, IReadOnlyList<Placement> placements)
        {
            Name = name;
            Placements = placements;
        }

        /// <summary>
        /// Labeled debug additions for the MVP stress fixture so House/Farm/Trap views are exercised: a House and a Farm away
        /// from the four entry routes and a Spike Trap on the west approach.
        /// </summary>
        public static LayoutFixture CreateMvpStressExtras() => new LayoutFixture("MVP stress extras", new[]
        {
            new Placement(BuildingKind.House, new GridPos(15, 15)),
            new Placement(BuildingKind.Farm, new GridPos(15, 3)),
            new Placement(BuildingKind.SpikeTrap, new GridPos(4, 10)),
        });

        /// <summary>Exact stress layout from specification 002 (30 Walls, 10 Towers).</summary>
        public static LayoutFixture CreateStressLayout()
        {
            var list = new List<Placement>();
            for (var x = 8; x <= 12; x++)
            for (var z = 8; z <= 12; z++)
                if (x == 8 || x == 12 || z == 8 || z == 12)
                    list.Add(new Placement(BuildingKind.Wall, new GridPos(x, z)));
            for (var z = 3; z <= 16; z++) list.Add(new Placement(BuildingKind.Wall, new GridPos(6, z)));
            int[,] towers = { { 7, 8 }, { 7, 10 }, { 7, 12 }, { 13, 8 }, { 13, 10 }, { 13, 12 }, { 8, 7 }, { 10, 7 }, { 12, 7 }, { 10, 13 } };
            for (var i = 0; i < towers.GetLength(0); i++)
                list.Add(new Placement(BuildingKind.Tower, new GridPos(towers[i, 0], towers[i, 1])));
            return new LayoutFixture("Stress layout", list);
        }
    }
}
