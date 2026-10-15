using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>JSON form of a <see cref="LayoutFixture"/>, stored as TextAssets under Data/Layouts.</summary>
    public static class LayoutJson
    {
        [Serializable]
        sealed class LayoutDto
        {
            public string name;
            public string expectedOutcome;
            public PlacementDto[] placements;
        }

        [Serializable]
        sealed class PlacementDto
        {
            public string kind;
            public int x;
            public int z;
        }

        public static LayoutFixture Parse(string json, out string expectedOutcome)
        {
            var dto = JsonUtility.FromJson<LayoutDto>(json);
            if (dto == null) throw new FormatException("Layout JSON could not be parsed.");
            var list = new List<Placement>();
            foreach (var p in dto.placements ?? Array.Empty<PlacementDto>())
            {
                if (!Enum.TryParse(p.kind, out BuildingKind kind) || kind == BuildingKind.Keep)
                    throw new FormatException($"Layout '{dto.name}' has invalid building kind '{p.kind}'.");
                list.Add(new Placement(kind, new GridPos(p.x, p.z)));
            }

            expectedOutcome = dto.expectedOutcome;
            return new LayoutFixture(dto.name, list);
        }

        public static LayoutFixture Parse(TextAsset asset) => Parse(asset.text, out _);

        public static string ToJson(LayoutFixture layout, string expectedOutcome)
        {
            var dto = new LayoutDto { name = layout.Name, expectedOutcome = expectedOutcome, placements = new PlacementDto[layout.Placements.Count] };
            for (var i = 0; i < layout.Placements.Count; i++)
            {
                var p = layout.Placements[i];
                dto.placements[i] = new PlacementDto { kind = p.Kind.ToString(), x = p.Cell.X, z = p.Cell.Z };
            }

            return JsonUtility.ToJson(dto, true);
        }
    }
}
