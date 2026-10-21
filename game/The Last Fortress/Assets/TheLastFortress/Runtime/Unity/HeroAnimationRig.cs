using UnityEngine;

namespace TheLastFortress
{
    /// <summary>What the hero presenter knows this frame, in the figure's own frame (specs/003 section 7).</summary>
    public struct HeroPoseInput
    {
        /// <summary>Shown travel this frame in tiles per second, along the root's forward (+) and right (+) axes.</summary>
        public float Forward, Right;

        /// <summary>Change of the root's yaw this frame, degrees per second (turning on the spot moves the feet).</summary>
        public float TurnRate;

        /// <summary>Where the upper body should aim, relative to the root's yaw (degrees; clamped by the rig).</summary>
        public float AimOffset;

        /// <summary>release-rules-4 attack phase from the simulation's ticks: 0..1 wind-up, 1..2 recovery, below 0 none.</summary>
        public float AttackPhase;

        /// <summary>0..1 flinch, 0..1 fall (down), 0..1 crouch (rising after a respawn).</summary>
        public float Hit, Fall, Crouch;

        public bool Airborne;

        /// <summary>Presentation seconds (0 while frozen) and the figure's height in tiles (gait stride).</summary>
        public float Dt, BodyHeight;
    }

    /// <summary>
    /// The release-rules-4 hero's animation adapter (specs/003 section 7): an articulated part rig (elbows, wrists, ankles)
    /// posed procedurally from what the presenter passes. The lower body follows actual travel (forward, backward, sideways
    /// and diagonal cycles; it settles when the hero stops or is blocked), the hips turn toward the travel direction while
    /// the upper body keeps aiming within a limited twist, feet stay flat while planted, and the slash's wind-up, contact
    /// and recovery come from the simulation's attack phase. Footfalls are counted where a foot plants. It rotates only its
    /// own joints on the presentation clock and never feeds anything back. The legacy hero and every enemy keep
    /// <see cref="CharacterRig"/>.
    /// </summary>
    public sealed class HeroAnimationRig : MonoBehaviour
    {
        public Transform hips, torso, head, armL, armR, forearmL, forearmR, handL, handR, weapon, offHand, legL, legR, shinL, shinR, footL, footR;

        /// <summary>Most the chest turns from the hips toward the aim (degrees); beyond it the hips and feet turn.</summary>
        public float twistLimit = 55f;

        /// <summary>Most the hips turn toward the travel direction away from the root (degrees).</summary>
        public float hipLimit = 50f;

        Quaternion hipsRest, torsoRest, headRest, armLRest, armRRest, foreLRest, foreRRest, handLRest, handRRest, weaponRest;
        Quaternion legLRest, legRRest, shinLRest, shinRRest, footLRest, footRRest;
        Vector3 hipsPos;
        bool captured;
        float phase, gait, run, hipYaw, lean, idleClock, lastSpeed, aimTwist;

        /// <summary>Feet that planted so far, and where the last one did (world), for the footfall sounds.</summary>
        public int FootfallCount { get; private set; }

        public Vector3 LastFootfall { get; private set; }

        /// <summary>Gait 0 standing .. 1 moving, as shown (tests and the capture log).</summary>
        public float Gait => gait;

        public float LastAttackPhase { get; private set; } = -1f;

        /// <summary>Whether every joint the adapter needs is present (content validation).</summary>
        public bool Complete =>
            hips && torso && head && armL && armR && forearmL && forearmR && handL && handR && weapon && legL && legR && shinL && shinR && footL && footR;

        void Capture()
        {
            if (captured) return;
            captured = true;
            hipsRest = Rest(hips);
            torsoRest = Rest(torso);
            headRest = Rest(head);
            armLRest = Rest(armL);
            armRRest = Rest(armR);
            foreLRest = Rest(forearmL);
            foreRRest = Rest(forearmR);
            handLRest = Rest(handL);
            handRRest = Rest(handR);
            weaponRest = Rest(weapon);
            legLRest = Rest(legL);
            legRRest = Rest(legR);
            shinLRest = Rest(shinL);
            shinRRest = Rest(shinR);
            footLRest = Rest(footL);
            footRRest = Rest(footR);
            if (hips != null) hipsPos = hips.localPosition;
        }

        static Quaternion Rest(Transform t) => t != null ? t.localRotation : Quaternion.identity;

        static void Set(Transform t, Quaternion rest, float x, float y, float z)
        {
            if (t != null) t.localRotation = rest * Quaternion.Euler(x, y, z);
        }

        /// <summary>Poses the figure for this frame (absolute rotations from the rest pose: nothing accumulates).</summary>
        public void Pose(in HeroPoseInput p)
        {
            Capture();
            var dt = p.Dt;
            var speed = Mathf.Sqrt(p.Forward * p.Forward + p.Right * p.Right);
            var height = Mathf.Max(0.02f, p.BodyHeight);
            var alive = 1f - p.Fall;

            if (dt > 0f)
            {
                // Gait follows actual shown travel: it settles when the hero stops or presses into a wall. Turning on the
                // spot shuffles the feet a little.
                // Only a real turn on the spot (not ordinary looking around, and never while running) shuffles the feet.
                var turnOnSpot = speed < 0.05f && Mathf.Abs(p.TurnRate) > 150f ? Mathf.Abs(p.TurnRate) : 0f;
                var turning = Mathf.Clamp01(turnOnSpot / 360f) * 0.5f;
                var moving = Mathf.Clamp01(speed / (0.35f * height * 10f) + turning);
                gait = Mathf.MoveTowards(gait, p.Airborne ? 0.3f : moving, dt * 7f);
                run = Mathf.MoveTowards(run, CharacterRig.RunAmount(speed, height), dt * 4f);
                var before = phase;
                phase += (speed + turnOnSpot * Mathf.Deg2Rad * 0.06f) * dt * CharacterRig.PhasePerTile(height, run);
                // A foot plants each half cycle: footfalls follow the feet, not a timer.
                if (gait > 0.3f && speed > 0.04f && Mathf.Floor(before / Mathf.PI) != Mathf.Floor(phase / Mathf.PI))
                {
                    FootfallCount++;
                    var left = Mathf.FloorToInt(phase / Mathf.PI) % 2 == 0;
                    LastFootfall = (left ? footL : footR) != null ? (left ? footL : footR).position : transform.position;
                }

                // Starts lean in, stops rock back: from the change of shown speed along the root's forward axis.
                var accel = (p.Forward - lastSpeed) / dt;
                lastSpeed = p.Forward;
                lean = Mathf.MoveTowards(lean, Mathf.Clamp(accel * 6f, -10f, 12f), dt * 120f);
                idleClock += dt * 2.1f;
            }

            // Travel direction relative to the root; past 100 degrees from the aim the figure backpedals instead of turning
            // its hips around. The hips take up to hipLimit of it, the legs step along the rest.
            var travel = speed > 1e-3f ? Mathf.Atan2(p.Right, p.Forward) * Mathf.Rad2Deg : 0f;
            var back = Mathf.Abs(travel) > 100f;
            var legDir = back ? Mathf.DeltaAngle(180f, travel) : travel;
            var wantHip = speed > 1e-3f ? Mathf.Clamp(legDir, -hipLimit, hipLimit) * Mathf.Clamp01(gait * 1.5f) : 0f;
            hipYaw = dt > 0f ? Mathf.MoveTowards(hipYaw, wantHip, dt * 400f) : wantHip;
            var stepDir = (legDir - hipYaw) * Mathf.Deg2Rad; // what the legs still have to cover, in the hips' frame
            var dirForward = Mathf.Cos(stepDir) * (back ? -1f : 1f);
            var dirSide = Mathf.Sin(stepDir);

            var walk = gait * alive;
            var s = Mathf.Sin(phase);
            var c = Mathf.Cos(phase);
            var swing = Mathf.Lerp(32f, 46f, run) * walk;
            var kneeAmp = Mathf.Lerp(26f, 62f, run) * walk;
            var soft = 6f * (1f - walk) + 4f + p.Crouch * 70f;
            var kneeL = soft + kneeAmp * Mathf.Max(0f, c * (back ? -1f : 1f)) + 8f * walk * Mathf.Max(0f, -s);
            var kneeR = soft + kneeAmp * Mathf.Max(0f, -c * (back ? -1f : 1f)) + 8f * walk * Mathf.Max(0f, s);
            var thighL = -s * swing * dirForward - soft * 0.5f - p.Crouch * 40f - p.Fall * 25f;
            var thighR = s * swing * dirForward - soft * 0.5f - p.Crouch * 40f - p.Fall * 10f;
            var sideL = -s * swing * dirSide * 0.55f;
            var sideR = s * swing * dirSide * 0.55f;
            Set(legL, legLRest, thighL, 0f, sideL - p.Fall * 8f);
            Set(legR, legRRest, thighR, 0f, sideR + p.Fall * 8f);
            Set(shinL, shinLRest, kneeL + p.Fall * 35f, 0f, 0f);
            Set(shinR, shinRRest, kneeR + p.Fall * 20f, 0f, 0f);
            // Feet: flat while planted (cancel thigh and knee), toes lifting through the swing.
            var stanceL = Mathf.Clamp01(0.5f - 0.5f * s * dirForward * Mathf.Sign(swing + 1e-4f));
            var stanceR = Mathf.Clamp01(0.5f + 0.5f * s * dirForward * Mathf.Sign(swing + 1e-4f));
            Set(footL, footLRest, -(thighL + kneeL) * Mathf.Lerp(0.6f, 1f, stanceL) * alive - 12f * walk * (1f - stanceL), 0f, -sideL * 0.8f);
            Set(footR, footRRest, -(thighR + kneeR) * Mathf.Lerp(0.6f, 1f, stanceR) * alive - 12f * walk * (1f - stanceR), 0f, -sideR * 0.8f);

            // Hips: bob twice a stride, turn toward travel, sway with the step.
            if (hips != null)
                hips.localPosition = hipsPos + new Vector3(0f, (Mathf.Lerp(Mathf.Abs(c), 1f - Mathf.Abs(c), run) * (1f + run) - run * 0.3f) * 0.012f * walk - p.Crouch * 0.05f, 0f);
            Set(hips, hipsRest, 0f, hipYaw + s * 4f * walk, -s * 3f * walk * (1f - run * 0.5f));

            // The attack: wind up (coiled right, blade back and high), strike through at phase 1, recover to guard.
            var a = p.AttackPhase;
            var wind = a >= 0f && a < 1f ? Mathf.SmoothStep(0f, 1f, a) : 0f;
            var r = a >= 1f ? a - 1f : -1f;
            var strike = r >= 0f ? 1f - Mathf.SmoothStep(0f, 1f, r / 0.55f) : 0f;
            var follow = r >= 0f ? Mathf.Clamp01(r / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, (r - 0.12f) / 0.6f)) : 0f;
            LastAttackPhase = a;
            var combat = Mathf.Max(wind, strike);

            // Upper body: counter the hips so the chest aims; the rest of the aim within the twist limit; the slash coils
            // and uncoils it.
            var wantTwist = Mathf.Clamp(p.AimOffset, -twistLimit, twistLimit);
            aimTwist = dt > 0f ? Mathf.MoveTowards(aimTwist, wantTwist, dt * 500f) : wantTwist;
            var idle = (1f - gait) * alive * (1f - combat);
            var breathe = Mathf.Sin(idleClock) * idle;
            var chestYaw = -hipYaw + aimTwist + wind * 38f - follow * 42f - s * 5f * walk;
            var chestLean = Mathf.Lerp(5f, 14f, run) * walk + lean + strike * 12f - wind * 6f - p.Hit * 20f + p.Crouch * 25f;
            Set(torso, torsoRest, chestLean - breathe * 1.5f, chestYaw, p.Hit * 6f);
            Set(head, headRest, -chestLean * 0.5f - p.Hit * 10f + p.Fall * 30f, -chestYaw * 0.35f, 0f);

            // Sword arm: guard (forearm up, blade forward) -> wind-up (arm out and back, elbow folded, wrist cocked) -> contact
            // (arm across the front, nearly straight) -> back to guard.
            // Blade angles (from straight down, forward positive) add up joint by joint: the generated grip holds the blade
            // 135 degrees from the forearm, so the sword is re-gripped 60 degrees back (a natural 75-degree grip) and the
            // wrist snaps the rest: guard about 75 (blade up and forward), wind-up 60 (back over the shoulder), contact
            // 0 (in line with the extended arm).
            var pump = Mathf.Lerp(0.3f, 0.8f, run) * walk * (1f - combat);
            var guardArm = new Vector3(-20f - s * 18f * pump, 0f, 10f);
            var windArm = new Vector3(-95f, -10f, 62f);
            var hitArm = new Vector3(-70f, 25f, -18f);
            var arm = Vector3.Lerp(guardArm, windArm, wind);
            arm = Vector3.Lerp(arm, hitArm, Mathf.Max(strike * 0.9f, follow));
            Set(armR, armRRest, arm.x * alive, arm.y * alive, arm.z * alive + p.Fall * 55f);
            var elbowR = Mathf.Lerp(Mathf.Lerp(-55f, -60f, wind), -10f, Mathf.Max(strike, follow));
            Set(forearmR, foreRRest, elbowR * alive, 0f, 0f);
            var wristR = Mathf.Lerp(Mathf.Lerp(0f, 15f, wind), 75f, Mathf.Max(strike, follow));
            Set(handR, handRRest, wristR * alive, 0f, Mathf.Lerp(0f, -25f, follow) * alive);
            Set(weapon, weaponRest, 60f, 0f, 0f);

            // Shield arm: held before the chest, swinging a little with the stride; tucked in as the sword comes through.
            var shieldSwing = s * 12f * walk * (1f - combat);
            Set(armL, armLRest, (-32f + shieldSwing + combat * 12f) * alive, 0f, (-12f - combat * 6f) * alive - p.Fall * 60f);
            Set(forearmL, foreLRest, (-72f + combat * 10f) * alive - p.Fall * 20f, 0f, 0f);
            Set(handL, handLRest, 0f, 0f, 0f);
        }
    }
}
