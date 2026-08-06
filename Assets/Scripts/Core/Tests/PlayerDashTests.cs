using Game.Core.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    public class PlayerDashTests
    {
        const float Step = 1f / 60f;
        const float Tolerance = 1e-3f;

        static PlayerDash Make(out FakeDashSettings settings)
        {
            settings = new FakeDashSettings();
            return new PlayerDash(settings);
        }

        /// <summary>Runs the dash to completion, returning the summed displacement.</summary>
        static Vector3 RunToEnd(PlayerDash dash, float step = Step, int maxSteps = 1000)
        {
            Vector3 total = Vector3.zero;
            for (int i = 0; i < maxSteps && dash.IsDashing; i++)
                total += dash.Tick(step);
            return total;
        }

        [Test]
        public void StartsIdleWithFullCharges()
        {
            PlayerDash dash = Make(out FakeDashSettings settings);
            Assert.That(dash.IsDashing, Is.False);
            Assert.That(dash.Charges.Available, Is.EqualTo(settings.MaxCharges));
            Assert.That(dash.IsInvulnerable, Is.False);
        }

        [Test]
        public void TryStart_SpendsAChargeAndBeginsDashing()
        {
            PlayerDash dash = Make(out _);
            Assert.That(dash.TryStart(Vector3.right), Is.True);
            Assert.That(dash.IsDashing, Is.True);
            Assert.That(dash.Charges.Available, Is.EqualTo(1));
            Assert.That(dash.Direction, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void TryStart_FailsWithoutCharges()
        {
            PlayerDash dash = Make(out _);
            dash.TryStart(Vector3.right);
            RunToEnd(dash);
            dash.TryStart(Vector3.right);
            RunToEnd(dash);

            Assert.That(dash.Charges.Available, Is.EqualTo(0));
            Assert.That(dash.TryStart(Vector3.right), Is.False);
            Assert.That(dash.IsDashing, Is.False);
        }

        [Test]
        public void TryStart_FailsWhileAlreadyDashing_AndDoesNotSpendACharge()
        {
            PlayerDash dash = Make(out _);
            dash.TryStart(Vector3.right);
            Assert.That(dash.TryStart(Vector3.forward), Is.False);
            Assert.That(dash.Charges.Available, Is.EqualTo(1));
            Assert.That(dash.Direction, Is.EqualTo(Vector3.right), "direction must stay locked");
        }

        [Test]
        public void TryStart_RejectsDegenerateDirection_AndKeepsTheCharge()
        {
            PlayerDash dash = Make(out _);
            Assert.That(dash.TryStart(Vector3.up), Is.False);
            Assert.That(dash.Charges.Available, Is.EqualTo(2));
        }

        [Test]
        public void CoversExactlyTheConfiguredDistance()
        {
            PlayerDash dash = Make(out FakeDashSettings settings);
            dash.TryStart(Vector3.right);
            Vector3 total = RunToEnd(dash);
            Assert.That(total.magnitude, Is.EqualTo(settings.DistanceMeters).Within(Tolerance));
            Assert.That(total.x, Is.EqualTo(settings.DistanceMeters).Within(Tolerance));
        }

        [Test]
        public void DistanceIsExact_WhateverTheCurveShape()
        {
            PlayerDash dash = Make(out FakeDashSettings settings);
            settings.TravelCurve = t => t * t * t; // heavily back-loaded
            dash.TryStart(Vector3.forward);
            Vector3 total = RunToEnd(dash);
            Assert.That(total.magnitude, Is.EqualTo(settings.DistanceMeters).Within(Tolerance));
        }

        [Test]
        public void DistanceIsExact_WithAnUnevenFinalFrame()
        {
            // A frame that overshoots the end must not overshoot the distance.
            PlayerDash dash = Make(out FakeDashSettings settings);
            dash.TryStart(Vector3.right);
            Vector3 total = RunToEnd(dash, step: 0.13f);
            Assert.That(total.magnitude, Is.EqualTo(settings.DistanceMeters).Within(Tolerance));
        }

        [Test]
        public void TravelCurveShapesTheDistribution()
        {
            PlayerDash frontLoaded = Make(out FakeDashSettings frontSettings);
            frontSettings.TravelCurve = t => Mathf.Sqrt(t);
            PlayerDash backLoaded = Make(out FakeDashSettings backSettings);
            backSettings.TravelCurve = t => t * t;

            frontLoaded.TryStart(Vector3.right);
            backLoaded.TryStart(Vector3.right);
            float half = frontSettings.DurationSeconds * 0.5f;

            float frontDistance = frontLoaded.Tick(half).magnitude;
            float backDistance = backLoaded.Tick(half).magnitude;

            Assert.That(frontDistance, Is.GreaterThan(backDistance));
        }

        [Test]
        public void EndsAfterItsDuration()
        {
            PlayerDash dash = Make(out FakeDashSettings settings);
            dash.TryStart(Vector3.right);
            dash.Tick(settings.DurationSeconds - 0.01f);
            Assert.That(dash.IsDashing, Is.True);
            dash.Tick(0.02f);
            Assert.That(dash.IsDashing, Is.False);
        }

        [Test]
        public void TickWhileIdle_ProducesNoDisplacement()
        {
            PlayerDash dash = Make(out _);
            Assert.That(dash.Tick(Step), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void IFramesCoverTheLeadingFraction_ThenExpose()
        {
            PlayerDash dash = Make(out FakeDashSettings settings);
            settings.IFrameFraction = 0.85f;
            dash.TryStart(Vector3.right);

            dash.Tick(settings.DurationSeconds * 0.8f);
            Assert.That(dash.IsInvulnerable, Is.True, "should still be invulnerable at 80%");

            dash.Tick(settings.DurationSeconds * 0.1f); // now at 90%
            Assert.That(dash.IsDashing, Is.True);
            Assert.That(dash.IsInvulnerable, Is.False, "the tail must be a punish window");
        }

        [Test]
        public void NotInvulnerableAfterTheDashEnds()
        {
            PlayerDash dash = Make(out _);
            dash.TryStart(Vector3.right);
            RunToEnd(dash);
            Assert.That(dash.IsInvulnerable, Is.False);
        }

        [Test]
        public void PerfectDodge_RefundsTheChargeDuringIFrames()
        {
            PlayerDash dash = Make(out _);
            dash.TryStart(Vector3.right);
            Assert.That(dash.Charges.Available, Is.EqualTo(1));

            Assert.That(dash.TryRegisterPerfectDodge(), Is.True);
            Assert.That(dash.Charges.Available, Is.EqualTo(2));
        }

        [Test]
        public void PerfectDodge_RaisesItsEvent()
        {
            PlayerDash dash = Make(out _);
            int raised = 0;
            dash.PerfectDodged += () => raised++;

            dash.TryStart(Vector3.right);
            dash.TryRegisterPerfectDodge();

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void PerfectDodge_OnlyRefundsOncePerDash()
        {
            // A multi-hit attack must not farm charges.
            PlayerDash dash = Make(out _);
            dash.TryStart(Vector3.right);

            Assert.That(dash.TryRegisterPerfectDodge(), Is.True);
            Assert.That(dash.TryRegisterPerfectDodge(), Is.False);
            Assert.That(dash.Charges.Available, Is.EqualTo(2));
        }

        [Test]
        public void PerfectDodge_RequiresStrictOverlapWithIFrames()
        {
            PlayerDash dash = Make(out FakeDashSettings settings);
            dash.TryStart(Vector3.right);
            dash.Tick(settings.DurationSeconds * 0.95f); // past the i-frame window, still dashing

            Assert.That(dash.TryRegisterPerfectDodge(), Is.False);
            Assert.That(dash.Charges.Available, Is.EqualTo(1), "no refund outside i-frames");
        }

        [Test]
        public void PerfectDodge_DoesNothingWhenNotDashing()
        {
            PlayerDash dash = Make(out _);
            Assert.That(dash.TryRegisterPerfectDodge(), Is.False);
            Assert.That(dash.Charges.Available, Is.EqualTo(2));
        }

        [Test]
        public void PerfectDodge_IsAvailableAgainOnTheNextDash()
        {
            PlayerDash dash = Make(out _);
            dash.TryStart(Vector3.right);
            dash.TryRegisterPerfectDodge();
            RunToEnd(dash);

            dash.TryStart(Vector3.right);
            Assert.That(dash.TryRegisterPerfectDodge(), Is.True);
        }

        [Test]
        public void ChargesRechargeWhileDashing()
        {
            PlayerDash dash = Make(out FakeDashSettings settings);
            settings.DurationSeconds = 1f;
            settings.RechargeSeconds = 0.5f;

            dash.TryStart(Vector3.right);
            Assert.That(dash.Charges.Available, Is.EqualTo(1));
            dash.Tick(0.6f);
            Assert.That(dash.Charges.Available, Is.EqualTo(2), "real time passes during a dash");
        }

        [Test]
        public void CanStart_ReflectsDashStateAndCharges()
        {
            PlayerDash dash = Make(out _);
            Assert.That(dash.CanStart, Is.True);

            dash.TryStart(Vector3.right);
            Assert.That(dash.CanStart, Is.False, "cannot dash while dashing");

            RunToEnd(dash);
            Assert.That(dash.CanStart, Is.True);
        }

        [Test]
        public void Cancel_EndsTheDashWithoutRefunding()
        {
            PlayerDash dash = Make(out _);
            dash.TryStart(Vector3.right);
            dash.Cancel();

            Assert.That(dash.IsDashing, Is.False);
            Assert.That(dash.IsInvulnerable, Is.False);
            Assert.That(dash.Charges.Available, Is.EqualTo(1), "the charge stays spent");
        }

        [Test]
        public void DirectionIsNormalized()
        {
            PlayerDash dash = Make(out _);
            dash.TryStart(new Vector3(3f, 0f, 4f));
            Assert.That(dash.Direction.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void DirectionIsFlattenedToThePlane()
        {
            PlayerDash dash = Make(out _);
            dash.TryStart(new Vector3(1f, 5f, 0f));
            Assert.That(dash.Direction.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(dash.Direction.x, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void DisplacementStaysOnTheDashDirection()
        {
            PlayerDash dash = Make(out _);
            dash.TryStart(Vector3.right);
            for (int i = 0; i < 3; i++)
            {
                Vector3 step = dash.Tick(Step);
                Assert.That(step.z, Is.EqualTo(0f).Within(Tolerance));
                Assert.That(step.y, Is.EqualTo(0f).Within(Tolerance));
            }
        }
    }
}
