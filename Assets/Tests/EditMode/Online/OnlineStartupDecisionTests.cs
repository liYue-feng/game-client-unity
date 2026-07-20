using Game.Online;
using NUnit.Framework;

namespace Game.Tests.EditMode.Online
{
    public sealed class OnlineStartupDecisionTests
    {
        private const float TimeoutSeconds = 10f;

        [TestCase(OnlineSessionState.Idle)]
        [TestCase(OnlineSessionState.Connecting)]
        [TestCase(OnlineSessionState.Authenticating)]
        [TestCase(OnlineSessionState.LoadingArchive)]
        [TestCase(OnlineSessionState.Reconnecting)]
        public void EvaluateReturnsWaitingForNonTerminalStatesBeforeTimeout(OnlineSessionState state)
        {
            Assert.That(Evaluate(state, TimeoutSeconds - 0.1f), Is.EqualTo(OnlineStartupResult.Waiting));
        }

        [Test]
        public void EvaluateReturnsReadyForReadyState()
        {
            Assert.That(Evaluate(OnlineSessionState.Ready, 0f), Is.EqualTo(OnlineStartupResult.Ready));
        }

        [TestCase(OnlineSessionState.Failed)]
        [TestCase(OnlineSessionState.Stopped)]
        public void EvaluateReturnsFailedForFailedTerminalStates(OnlineSessionState state)
        {
            Assert.That(Evaluate(state, 0f), Is.EqualTo(OnlineStartupResult.Failed));
        }

        [TestCase(TimeoutSeconds)]
        [TestCase(TimeoutSeconds + 0.1f)]
        public void EvaluateReturnsTimedOutAtOrAfterTimeoutForNonTerminalStates(float elapsedSeconds)
        {
            Assert.That(Evaluate(OnlineSessionState.Connecting, elapsedSeconds), Is.EqualTo(OnlineStartupResult.TimedOut));
        }

        [TestCase(OnlineSessionState.Ready, OnlineStartupResult.Ready)]
        [TestCase(OnlineSessionState.Failed, OnlineStartupResult.Failed)]
        [TestCase(OnlineSessionState.Stopped, OnlineStartupResult.Failed)]
        public void EvaluateGivesTerminalStatesPrecedenceOverTimeout(
            OnlineSessionState state,
            OnlineStartupResult expected)
        {
            Assert.That(Evaluate(state, TimeoutSeconds + 0.1f), Is.EqualTo(expected));
        }

        private static OnlineStartupResult Evaluate(OnlineSessionState state, float elapsedSeconds)
        {
            return new OnlineStartupDecision().Evaluate(state, elapsedSeconds, TimeoutSeconds);
        }
    }
}
