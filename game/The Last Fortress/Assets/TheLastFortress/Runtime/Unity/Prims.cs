using UnityEngine;

namespace TheLastFortress
{
    /// <summary>Collider-free primitive helpers. Picking uses grid math, never physics.</summary>
    public static class Prims
    {
        public static GameObject Create(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale,
            Material material, string name = null, Quaternion? localRotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        public static Material Tinted(Material source, Color color)
        {
            var m = new Material(source);
            SetColor(m, color);
            return m;
        }

        public static void SetColor(Material m, Color color)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }

        public static LineRenderer Line(Transform parent, string name, Material material, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = material;
            lr.useWorldSpace = true;
            lr.startColor = lr.endColor = color;
            lr.startWidth = lr.endWidth = width;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.TransformZ;
            go.transform.rotation = Quaternion.Euler(90, 0, 0); // lie flat on the board
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            return lr;
        }

        public static Vector3 CellCenter(GridPos cell, float y = 0f) => new Vector3(cell.X + 0.5f, y, cell.Z + 0.5f);
    }
}
