using System;
using Game.Gameplay;
using NUnit.Framework;

namespace Game.Tests.EditMode.Gameplay
{
    public sealed class CombatCoreTests
    {
        [Test]
        public void CombatHitRetainsItsImmutableContract()
        {
            var source = new RecordingParryResponder();
            var hit = new CombatHit(12, -1f, 4.5f, true, source);

            Assert.That(hit.Damage, Is.EqualTo(12));
            Assert.That(hit.KnockbackDirectionX, Is.EqualTo(-1f));
            Assert.That(hit.KnockbackForce, Is.EqualTo(4.5f));
            Assert.That(hit.IsParryable, Is.True);
            Assert.That(hit.Source, Is.SameAs(source));
            Assert.That(typeof(CombatHit).GetProperty(nameof(CombatHit.Damage)).CanWrite, Is.False);
            Assert.That(Enum.GetNames(typeof(CombatHitResult)), Is.EquivalentTo(new[] { "Ignored", "Damaged", "Parried" }));
        }

        [Test]
        public void CombatHitAllowsAnAbsentSource()
        {
            var hit = new CombatHit(1, 1f, 0f, false);

            Assert.That(hit.Source, Is.Null);
        }

        [Test]
        public void AttackTimelineUsesExactPhaseBoundaries()
        {
            var timeline = new AttackTimeline(2f, 0.25f, 0.25f);

            Assert.That(timeline.TotalDuration, Is.EqualTo(2f));
            Assert.That(timeline.Evaluate(-0.1f), Is.EqualTo(AttackPhase.Windup));
            Assert.That(timeline.Evaluate(0f), Is.EqualTo(AttackPhase.Windup));
            Assert.That(timeline.Evaluate(0.499f), Is.EqualTo(AttackPhase.Windup));
            Assert.That(timeline.Evaluate(0.5f), Is.EqualTo(AttackPhase.Active));
            Assert.That(timeline.Evaluate(0.999f), Is.EqualTo(AttackPhase.Active));
            Assert.That(timeline.Evaluate(1f), Is.EqualTo(AttackPhase.Recovery));
            Assert.That(timeline.Evaluate(1.999f), Is.EqualTo(AttackPhase.Recovery));
            Assert.That(timeline.Evaluate(2f), Is.EqualTo(AttackPhase.Complete));
            Assert.That(timeline.Evaluate(float.PositiveInfinity), Is.EqualTo(AttackPhase.Complete));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void AttackTimelineNormalizesNonPositiveTotalDuration(float duration)
        {
            var timeline = new AttackTimeline(duration, 0.25f, 0.25f);

            Assert.That(timeline.TotalDuration, Is.Zero);
            Assert.That(timeline.Evaluate(0f), Is.EqualTo(AttackPhase.Complete));
        }

        [Test]
        public void AttackTimelineNormalizesInvalidTotalDuration()
        {
            var timeline = new AttackTimeline(float.NaN, 0.25f, 0.25f);

            Assert.That(timeline.TotalDuration, Is.Zero);
            Assert.That(timeline.Evaluate(0f), Is.EqualTo(AttackPhase.Complete));
        }

        [Test]
        public void AttackTimelineNormalizesNegativeFractionsToZero()
        {
            var timeline = new AttackTimeline(2f, -0.25f, -1f);

            Assert.That(timeline.Evaluate(0f), Is.EqualTo(AttackPhase.Recovery));
            Assert.That(timeline.Evaluate(1.999f), Is.EqualTo(AttackPhase.Recovery));
            Assert.That(timeline.Evaluate(2f), Is.EqualTo(AttackPhase.Complete));
        }

        [Test]
        public void AttackTimelineClampsFractionsToTheAvailableDuration()
        {
            var timeline = new AttackTimeline(4f, 0.75f, 0.75f);

            Assert.That(timeline.Evaluate(2.999f), Is.EqualTo(AttackPhase.Windup));
            Assert.That(timeline.Evaluate(3f), Is.EqualTo(AttackPhase.Active));
            Assert.That(timeline.Evaluate(3.999f), Is.EqualTo(AttackPhase.Active));
            Assert.That(timeline.Evaluate(4f), Is.EqualTo(AttackPhase.Complete));
        }

        [Test]
        public void AttackTimelineNormalizesNonFiniteFractionsDeterministically()
        {
            var timeline = new AttackTimeline(2f, float.NaN, float.PositiveInfinity);

            Assert.That(timeline.Evaluate(0f), Is.EqualTo(AttackPhase.Active));
            Assert.That(timeline.Evaluate(1.999f), Is.EqualTo(AttackPhase.Active));
            Assert.That(timeline.Evaluate(2f), Is.EqualTo(AttackPhase.Complete));
        }

        [Test]
        public void ActionPolicyRejectsBlockedTransitionFirst()
        {
            var decision = CombatActionPolicy.Evaluate(false, true, 0f, 10f);

            Assert.That(decision.Allowed, Is.False);
            Assert.That(decision.Failure, Is.EqualTo(CombatActionFailure.TransitionBlocked));
        }

        [Test]
        public void ActionPolicyRejectsCooldownBeforeStamina()
        {
            var decision = CombatActionPolicy.Evaluate(true, true, 0f, 10f);

            Assert.That(decision.Allowed, Is.False);
            Assert.That(decision.Failure, Is.EqualTo(CombatActionFailure.OnCooldown));
        }

        [Test]
        public void ActionPolicyRejectsInsufficientStamina()
        {
            var decision = CombatActionPolicy.Evaluate(true, false, 9.999f, 10f);

            Assert.That(decision.Allowed, Is.False);
            Assert.That(decision.Failure, Is.EqualTo(CombatActionFailure.InsufficientStamina));
        }

        [Test]
        public void ActionPolicyAllowsExactStaminaCost()
        {
            var decision = CombatActionPolicy.Evaluate(true, false, 10f, 10f);

            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.Failure, Is.EqualTo(CombatActionFailure.None));
        }

        [Test]
        public void ActionPolicyNormalizesNegativeCostToZero()
        {
            var decision = CombatActionPolicy.Evaluate(true, false, 0f, -10f);

            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.Failure, Is.EqualTo(CombatActionFailure.None));
        }

        [Test]
        public void TimeScaleRequestsWithTheSameReasonReceiveUniqueTokens()
        {
            var requests = new TimeScaleRequestSet();

            var first = requests.Add("HitStop", 0.5f);
            var second = requests.Add("HitStop", 0.25f);

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(requests.Count, Is.EqualTo(2));
            Assert.That(requests.EffectiveScale, Is.EqualTo(0.25f));
        }

        [Test]
        public void SameReasonRequestsCanBeReleasedInReverseOrder()
        {
            var requests = new TimeScaleRequestSet();
            var first = requests.Add("HitStop", 0.5f);
            var second = requests.Add("HitStop", 0.25f);

            Assert.That(requests.Release(second), Is.True);
            Assert.That(requests.EffectiveScale, Is.EqualTo(0.5f));
            Assert.That(requests.Release(first), Is.True);
            Assert.That(requests.EffectiveScale, Is.EqualTo(1f));
        }

        [Test]
        public void ReleasingOneSameReasonRequestDoesNotReleaseTheOther()
        {
            var requests = new TimeScaleRequestSet();
            var first = requests.Add("Pause", 0f);
            var second = requests.Add("Pause", 0.5f);

            Assert.That(requests.Release(first), Is.True);
            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(requests.EffectiveScale, Is.EqualTo(0.5f));
            Assert.That(requests.Release(second), Is.True);
        }

        [Test]
        public void TimeScaleRequestsClampAndUpdateScale()
        {
            var requests = new TimeScaleRequestSet();
            var low = requests.Add("Pause", -2f);
            var high = requests.Add("Presentation", 3f);

            Assert.That(requests.EffectiveScale, Is.Zero);
            Assert.That(requests.Update(low, 0.75f), Is.True);
            Assert.That(requests.EffectiveScale, Is.EqualTo(0.75f));
            Assert.That(requests.Update(high, float.NaN), Is.True);
            Assert.That(requests.EffectiveScale, Is.EqualTo(0.75f));
        }

        [Test]
        public void TimeScaleReleaseIsIdempotentAndClearResetsScale()
        {
            var requests = new TimeScaleRequestSet();
            var token = requests.Add("Pause", 0f);

            Assert.That(requests.Release(token), Is.True);
            Assert.That(requests.Release(token), Is.False);
            requests.Add("Pause", 0f);
            requests.Clear();

            Assert.That(requests.Count, Is.Zero);
            Assert.That(requests.EffectiveScale, Is.EqualTo(1f));
        }

        [Test]
        public void BattleRunAcceptsOnlyTheFirstTerminalOutcome()
        {
            var run = new BattleRunStateMachine();

            Assert.That(run.State, Is.EqualTo(BattleRunState.Running));
            Assert.That(run.TryComplete(BattleRunOutcome.Victory), Is.True);
            Assert.That(run.State, Is.EqualTo(BattleRunState.Victory));
            Assert.That(run.Outcome, Is.EqualTo(BattleRunOutcome.Victory));
            Assert.That(run.TryComplete(BattleRunOutcome.Defeat), Is.False);
            Assert.That(run.TryComplete(BattleRunOutcome.Victory), Is.False);
        }

        [Test]
        public void BattleRunRestartBeginsOnlyOnceAfterTerminalOutcome()
        {
            var run = new BattleRunStateMachine();

            Assert.That(run.BeginRestart(), Is.False);
            Assert.That(run.TryComplete(BattleRunOutcome.Defeat), Is.True);
            Assert.That(run.BeginRestart(), Is.True);
            Assert.That(run.State, Is.EqualTo(BattleRunState.Restarting));
            Assert.That(run.BeginRestart(), Is.False);
        }

        [Test]
        public void BattleRunRejectsAnUndefinedTerminalOutcome()
        {
            var run = new BattleRunStateMachine();

            Assert.That(run.TryComplete((BattleRunOutcome)999), Is.False);
            Assert.That(run.State, Is.EqualTo(BattleRunState.Running));
            Assert.That(run.Outcome, Is.EqualTo(BattleRunOutcome.None));
        }

        [Test]
        public void BattleRunDisposeIsIdempotentAndTerminal()
        {
            var run = new BattleRunStateMachine();

            run.Dispose();
            run.Dispose();

            Assert.That(run.State, Is.EqualTo(BattleRunState.Disposed));
            Assert.That(run.TryComplete(BattleRunOutcome.Victory), Is.False);
            Assert.That(run.BeginRestart(), Is.False);
        }

        private sealed class RecordingParryResponder : IParryResponder
        {
            public int CallCount { get; private set; }

            public void OnParried()
            {
                CallCount++;
            }
        }
    }
}
