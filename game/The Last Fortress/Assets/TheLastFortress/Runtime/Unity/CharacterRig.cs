using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Part rig of an authored enemy model (plan 011): joint transforms posed from the presentation state WorldView already
    /// computes (gait phase, strike, wind-up, hit, fall). It only rotates its own parts on the presentation clock, so it
    /// freezes while paused, follows Night speed through the simulation, and never feeds anything back to the simulation.
    /// Rest poses are captured once; every <see cref="Pose"/> call writes absolute rotations, so nothing accumulates.
    /// Axis notes (Unity): a negative X rotation swings a hanging limb forward; positive Z swings it toward +X.
    /// </summary>
    public sealed class CharacterRig : MonoBehaviour
    {
        public Transform hips, torso, head, armL, armR, weapon, legL, legR;

        /// <summary>Gait amplitudes in degrees: leg swing, free-arm swing, torso twist; and the hip bob in model units.</summary>
        public float legSwing = 32f, armSwing = 24f, twist = 7f, hipBob = 0.012f;

        Quaternion hipsRest, torsoRest, headRest, armLRest, armRRest, weaponRest, legLRest, legRRest;
        Vector3 hipsPos;
        bool captured;

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
            if (hips != null) hipsPos = hips.localPosition;
        }

        static Quaternion Rest(Transform t) => t != null ? t.localRotation : Quaternion.identity;

        static void Set(Transform t, Quaternion rest, float x, float y, float z)
        {
            if (t != null) t.localRotation = rest * Quaternion.Euler(x, y, z);
        }

        /// <param name="phase">Gait phase in radians (advances with distance walked).</param>
        /// <param name="gait">0 standing .. 1 walking.</param>
        /// <param name="strike">0..1 forward strike, peaking the moment a hit lands.</param>
        /// <param name="windUp">0..1 wind-up before a due hit (weapon raised, body leaning back).</param>
        /// <param name="hit">0..1 flinch after taking a hit (decays quickly).</param>
        /// <param name="fall">0..1 death fall (limbs go slack as the body tips).</param>
        public void Pose(float phase, float gait, float strike, float windUp, float hit, float fall)
        {
            Capture();
            var s = Mathf.Sin(phase);
            var c = Mathf.Cos(phase);
            var walk = gait * (1f - fall);
            // Legs swing opposite; the knee-less leg lifts a little on its forward half so the foot clears the ground.
            Set(legL, legLRest, -s * legSwing * walk - fall * 25f, 0f, -fall * 8f);
            Set(legR, legRRest, s * legSwing * walk - fall * 10f, 0f, fall * 8f);
            if (hips != null) hips.localPosition = hipsPos + new Vector3(0f, Mathf.Abs(c) * hipBob * walk, 0f);
            Set(hips, hipsRest, 0f, s * twist * 0.5f * walk, -s * 3f * walk);

            // Weapon arm: ready (rest), raised back on wind-up, swung down-forward on the strike; it swings a little in step.
            var weaponArm = Mathf.Lerp(-105f * windUp, -40f, strike); // raised back on the wind-up, swung down-forward on contact
            Set(armR, armRRest, weaponArm - s * armSwing * 0.35f * walk * (1f - windUp), 0f, 6f + fall * 55f);
            Set(weapon, weaponRest, strike * 25f - windUp * 20f, 0f, 0f);
            Set(armL, armLRest, s * armSwing * walk - strike * 25f + windUp * 15f, 0f, -6f - fall * 60f);

            // Torso: twist with the stride, lean into the strike, rock back on wind-up and on a hit.
            var lean = 6f * walk + strike * 16f - windUp * 12f - hit * 22f;
            Set(torso, torsoRest, lean, -s * twist * walk + strike * 14f - windUp * 10f, hit * 6f);
            Set(head, headRest, -lean * 0.4f + hit * -10f + fall * 30f, s * twist * 0.4f * walk, 0f);
        }
    }
}
