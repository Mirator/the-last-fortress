using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Part rig of an authored figure (plan 011, knees and gait in plan 013): joint transforms posed from the presentation
    /// state the views already compute (gait phase, strike, wind-up, hit, fall). It only rotates its own parts on the
    /// presentation clock, so it freezes while paused, follows Night speed through the simulation, and never feeds anything
    /// back to the simulation. Rest poses are captured once; every <see cref="Pose"/> call writes absolute rotations, so
    /// nothing accumulates. Axis notes (Unity): a negative X rotation swings a hanging limb forward; positive Z swings it
    /// toward +X; a positive X rotation of a shin folds the knee (foot goes back).
    /// </summary>
    public sealed class CharacterRig : MonoBehaviour
    {
        public Transform hips, torso, head, armL, armR, weapon, legL, legR;

        /// <summary>Knee joints (plan 013 figures); null on older models, which then swing rigid legs as before.</summary>
        public Transform shinL, shinR;

        /// <summary>Gait amplitudes in degrees: leg swing, free-arm swing, torso twist; and the hip bob in model units.</summary>
        public float legSwing = 36f, armSwing = 24f, twist = 7f, hipBob = 0.012f;

        Quaternion hipsRest, torsoRest, headRest, armLRest, armRRest, weaponRest, legLRest, legRRest, shinLRest, shinRRest;
        Vector3 hipsPos;
        bool captured;

        /// <summary>Leg swing at a full run (degrees); the walk uses <see cref="legSwing"/>.</summary>
        const float RunLegSwing = 50f;

        void Capture()
        {
            if (captured) return;
            captured = true;
            hipsRest = Rest(hips);
            torsoRest = Rest(torso);
            headRest = Rest(head);
            armLRest = Rest(armL);
            armRRest = Rest(armR);
            weaponRest = Rest(weapon);
            legLRest = Rest(legL);
            legRRest = Rest(legR);
            shinLRest = Rest(shinL);
            shinRRest = Rest(shinR);
            if (hips != null) hipsPos = hips.localPosition;
        }

        static Quaternion Rest(Transform t) => t != null ? t.localRotation : Quaternion.identity;

        static void Set(Transform t, Quaternion rest, float x, float y, float z)
        {
            if (t != null) t.localRotation = rest * Quaternion.Euler(x, y, z);
        }

        /// <summary>
        /// How fast a figure walks relative to its size, as a run amount (0 walk .. 1 run). People at true scale in the hero
        /// view cover several body heights per second at the simulation's speeds, so they run; the larger strategic figures
        /// mostly walk.
        /// </summary>
        public static float RunAmount(float speedTilesPerSecond, float bodyHeightTiles) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.4f, 3.2f, speedTilesPerSecond / Mathf.Max(0.01f, bodyHeightTiles)));

        /// <summary>
        /// Gait phase (radians) per tile walked, so the planted foot keeps pace with the ground: one step is the foot's real sweep,
        /// 2 x leg (0.33 of the height) x sin(swing): 0.39 heights walking at 36 degrees, 0.81 running at 50 degrees with flight.
        /// </summary>
        public static float PhasePerTile(float bodyHeightTiles, float run) =>
            Mathf.PI / Mathf.Max(0.02f, bodyHeightTiles * Mathf.Lerp(0.39f, 0.81f, run));

        public void Pose(float phase, float gait, float strike, float windUp, float hit, float fall) => Pose(phase, gait, strike, windUp, hit, fall, 0f, 0f);

        /// <param name="phase">Gait phase in radians (advances with distance walked).</param>
        /// <param name="gait">0 standing .. 1 moving.</param>
        /// <param name="strike">0..1 forward strike, peaking the moment a hit lands.</param>
        /// <param name="windUp">0..1 wind-up before a due hit (weapon raised, body leaning back).</param>
        /// <param name="hit">0..1 flinch after taking a hit (decays quickly).</param>
        /// <param name="fall">0..1 death fall (limbs go slack as the body tips).</param>
        /// <param name="run">0 walk .. 1 run: longer leg swing, high knee lift, pumping arms, forward lean.</param>
        /// <param name="breath">Idle clock in radians (presentation time): a slow breath and weight shift while standing.</param>
        public void Pose(float phase, float gait, float strike, float windUp, float hit, float fall, float run, float breath)
        {
            Capture();
            var s = Mathf.Sin(phase);
            var c = Mathf.Cos(phase);
            var walk = gait * (1f - fall);
            var idle = (1f - gait) * (1f - fall) * (1f - Mathf.Max(strike, windUp));
            var inhale = Mathf.Sin(breath) * idle;
            var swing = Mathf.Lerp(legSwing, RunLegSwing, run) * walk;
            // Legs swing opposite. A leg folds its knee while it swings forward (cos > 0 for the left leg), and runners
            // tuck the heel high; standing figures keep a slight soft knee so they never look locked.
            var kneeAmp = Mathf.Lerp(28f, 70f, run) * walk;
            var soft = 6f * (1f - walk) + 4f;
            var kneeL = soft + kneeAmp * Mathf.Max(0f, c) + 8f * walk * Mathf.Max(0f, -s);
            var kneeR = soft + kneeAmp * Mathf.Max(0f, -c) + 8f * walk * Mathf.Max(0f, s);
            var hasKnees = shinL != null && shinR != null;
            // With knees, the thigh leads a little further forward so the foot lands under the body, not ahead of it.
            var thighBias = hasKnees ? -soft * 0.5f : 0f;
            Set(legL, legLRest, -s * swing + thighBias - fall * 25f, 0f, -fall * 8f);
            Set(legR, legRRest, s * swing + thighBias - fall * 10f, 0f, fall * 8f);
            Set(shinL, shinLRest, kneeL + fall * 35f, 0f, 0f);
            Set(shinR, shinRRest, kneeR + fall * 20f, 0f, 0f);
            // Two bobs per stride: lowest as a foot plants, highest mid-stride; a run bobs more and adds a crouch.
            // A walk is highest at mid-stance (legs together), a run lowest there (it bounds up between footfalls).
            if (hips != null) hips.localPosition = hipsPos + new Vector3(0f, (Mathf.Lerp(Mathf.Abs(c), 1f - Mathf.Abs(c), run) * (1f + run) - run * 0.3f) * hipBob * walk, 0f);
            Set(hips, hipsRest, 0f, s * twist * 0.5f * walk, -s * 3f * walk * (1f - run * 0.5f));

            // Weapon arm: ready (rest), raised back on wind-up, swung down-forward on the strike; it pumps in step.
            var pump = Mathf.Lerp(0.35f, 0.9f, run);
            var weaponArm = Mathf.Lerp(-105f * windUp, -40f, strike);
            Set(armR, armRRest, weaponArm - s * armSwing * pump * walk * (1f - windUp), 0f, 6f + fall * 55f);
            Set(weapon, weaponRest, strike * 25f - windUp * 20f, 0f, 0f);
            var freeArm = Mathf.Lerp(1f, 1.6f, run);
            Set(armL, armLRest, s * armSwing * freeArm * walk - strike * 25f + windUp * 15f - run * 20f * walk + inhale * 2f, 0f, -6f - fall * 60f - inhale * 2f);

            // Torso: twist with the stride, lean into the run and the strike, rock back on wind-up and on a hit.
            var lean = Mathf.Lerp(6f, 16f, run) * walk + strike * 16f - windUp * 12f - hit * 22f;
            Set(torso, torsoRest, lean - inhale * 1.5f, -s * twist * walk + strike * 14f - windUp * 10f + Mathf.Sin(breath * 0.37f) * 3f * idle, hit * 6f);
            Set(head, headRest, -lean * 0.5f + hit * -10f + fall * 30f, s * twist * 0.4f * walk, 0f);
        }
    }
}
