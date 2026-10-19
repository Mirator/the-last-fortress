using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastFortress
{
    /// <summary>
    /// Authored look for the board (plan 011): ground/bank/water materials, day and night post-processing profiles and the
    /// lighting values the day/night blend uses. Referenced from the content assets; when it is missing the view falls back
    /// to the generated blockout look. Owned by artists: no setup script writes into an existing asset.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Fortress/Art Direction")]
    public sealed class ArtDirection : ScriptableObject
    {
        [Header("Board materials")]
        public Material ground;
        public Material bank;
        public Material river;
        public Material ford;
        public Material beach;
        public Material cliff;
        public Material sea;
        public Material bridge;

        [Tooltip("Board ground texture repeats across the 20 x 20 board.")]
        public float groundTiling = 3f;

        [Header("Environment models (decoration only; never colliders)")]
        public GameObject pine;
        public GameObject broadleaf;
        public GameObject bush;
        public GameObject rockLarge;
        public GameObject rockSmall;
        public GameObject reeds;

        [Tooltip("Ground detail on open land; must stay under ~0.08 tall so it never reads as an obstacle.")]
        public GameObject tuft;

        public GameObject fordStone;

        [Tooltip("Enemy entry gate (its Banner part pulses for a new side); stands on the beach, never on a legal cell.")]
        public GameObject gate;

        [Tooltip("Distant shores placed in the sea beyond the frame corners.")]
        public GameObject[] farIslands;

        [Header("Post-processing (global volumes blended by the night amount)")]
        public VolumeProfile dayProfile;
        public VolumeProfile nightProfile;

        [Header("Day lighting")]
        public Color daySky = new Color(0.66f, 0.72f, 0.8f);
        public Color dayEquator = new Color(0.62f, 0.6f, 0.52f);
        public Color dayGround = new Color(0.36f, 0.34f, 0.28f);
        public Color sunColor = new Color(1f, 0.93f, 0.8f);
        public float sunIntensity = 1.5f;
        public float dayShadowStrength = 0.82f;

        [Header("Night lighting")]
        public Color nightSky = new Color(0.3f, 0.37f, 0.56f);
        public Color nightEquator = new Color(0.18f, 0.22f, 0.34f);
        public Color nightGround = new Color(0.07f, 0.08f, 0.12f);
        public Color moonColor = new Color(0.62f, 0.72f, 1f);
        public float moonIntensity = 0.85f;
        public float nightShadowStrength = 0.6f;

        [Header("Settlement light at night")]
        public Color torchColor = new Color(1f, 0.66f, 0.32f);
        public float keepLightIntensity = 3.2f;
        public float towerLightIntensity = 1.6f;
        public float towerLightRange = 2.6f;

        [Tooltip("Emission of window materials (by name ArtWindow) by day and at full night, multiplied by the torch colour.")]
        public float windowDay = 0.15f, windowNight = 2.4f;

        public Color seaColorDay = new Color(0.25f, 0.4f, 0.47f);
        public Color seaColorNight = new Color(0.06f, 0.09f, 0.16f);
    }
}
