using System;
using Game.Gameplay;
using Game.Network;
using Game.Protocol;
using UnityEngine;

namespace Game.Online
{
    public sealed class BattleSettlementCoordinator : IBattleSettlementGateway, IDisposable
    {
        private readonly NetworkClient _client;
        private readonly ArchiveSessionService _archiveService;
        private readonly BattleSettlementService _service;
        private readonly Action<PlayerArchive> _applyArchive;
        private readonly Func<bool> _recoverSession;
        private OnlineSessionState _sessionState;
        private int _generation;
        private int _lastSentGeneration = int.MinValue;
        private int _combatAttempt;
        private uint _combatSeq;
        private CombatResultReq _request;
        private CombatResultResp _response;
        private Action<BattleSettlementResult> _completed;
        private bool _awaitingCombat;
        private bool _awaitingSave;
        private bool _saveOnReady;
        private bool _cancellingCombat;
        private bool _disposed;

        public BattleSettlementCoordinator(
            NetworkClient client,
            ArchiveSessionService archiveService,
            Action<PlayerArchive> applyArchive = null,
            Func<bool> recoverSession = null)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            _client = client;
            _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
            _service = new BattleSettlementService(client);
            _applyArchive = applyArchive ?? (_ => { });
            _recoverSession = recoverSession;
            _archiveService.Saved += HandleArchiveSaved;
            _archiveService.Failed += HandleArchiveFailed;
        }

        public BattleSettlementState State { get; private set; }
        public string ActiveRunId => _request?.RunId;

        public void SetSessionState(OnlineSessionState state, int generation)
        {
            if (_disposed)
            {
                return;
            }

            _sessionState = state;
            _generation = generation;
            if (_awaitingSave && state == OnlineSessionState.Reconnecting)
            {
                _archiveService.CancelActiveOperation();
                _awaitingSave = false;
                Fail();
                return;
            }

            if ((_awaitingCombat || _awaitingSave || _saveOnReady) &&
                (state == OnlineSessionState.Failed || state == OnlineSessionState.Stopped))
            {
                if (_awaitingSave)
                {
                    _archiveService.CancelActiveOperation();
                }
                Fail();
                return;
            }

            if (state == OnlineSessionState.Ready && _saveOnReady && _response != null)
            {
                _saveOnReady = false;
                SaveAcceptedArchive();
                return;
            }

            if (state == OnlineSessionState.Ready && _awaitingCombat && _response == null &&
                _lastSentGeneration != _generation)
            {
                if (!SendActiveRequest())
                {
                    _awaitingCombat = false;
                    Fail();
                }
            }
        }

        public void Settle(BattleRunOutcome outcome, CombatResultData data, Action<BattleSettlementResult> completed)
        {
            if (_disposed || _request != null ||
                (outcome != BattleRunOutcome.Victory && outcome != BattleRunOutcome.Defeat))
            {
                return;
            }

            _request = _service.CreateRequest(outcome, data);
            _completed = completed;
            State = BattleSettlementState.Pending;
            _awaitingCombat = true;
            if (_sessionState == OnlineSessionState.Failed || _sessionState == OnlineSessionState.Stopped)
            {
                _awaitingCombat = false;
                Fail();
                return;
            }

            if (_sessionState == OnlineSessionState.Ready && !SendActiveRequest())
            {
                _awaitingCombat = false;
                Fail();
            }
        }

        public bool Retry()
        {
            if (_disposed || State != BattleSettlementState.Failed || _request == null || _awaitingSave)
            {
                return false;
            }

            if (_sessionState == OnlineSessionState.Stopped)
            {
                return false;
            }

            if (_sessionState == OnlineSessionState.Failed)
            {
                if (_recoverSession == null || !_recoverSession() ||
                    _sessionState == OnlineSessionState.Failed || _sessionState == OnlineSessionState.Stopped)
                {
                    return false;
                }
            }

            State = BattleSettlementState.Pending;
            if (_response != null)
            {
                if (_sessionState == OnlineSessionState.Ready)
                {
                    return SaveAcceptedArchive();
                }

                _saveOnReady = true;
                return true;
            }

            _awaitingCombat = true;
            if (_sessionState != OnlineSessionState.Ready)
            {
                return true;
            }

            if (SendActiveRequest(true))
            {
                return true;
            }

            _awaitingCombat = false;
            Fail();
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelActiveCombatRequest();
            _awaitingSave = false;
            _saveOnReady = false;
            _archiveService.Saved -= HandleArchiveSaved;
            _archiveService.Failed -= HandleArchiveFailed;
            _completed = null;
        }

        private bool SendActiveRequest(bool force = false)
        {
            if (_request == null)
            {
                return false;
            }

            if (!force && _lastSentGeneration == _generation)
            {
                return true;
            }

            if (_combatSeq != 0)
            {
                CancelActiveCombatRequest();
                _awaitingCombat = true;
            }

            var attempt = ++_combatAttempt;
            var requestReturned = false;
            CombatResultResp synchronousResponse = null;
            string synchronousFailure = null;
            var sent = _service.Send(
                _request,
                response =>
                {
                    if (!requestReturned)
                    {
                        synchronousResponse = response;
                        return;
                    }

                    HandleCombatResponse(attempt, response);
                },
                reason =>
                {
                    if (!requestReturned)
                    {
                        synchronousFailure = reason;
                        return;
                    }

                    HandleCombatFailure(attempt);
                },
                out var seq);
            requestReturned = true;
            if (!sent)
            {
                return false;
            }

            if (IsActiveCombatAttempt(attempt))
            {
                _combatSeq = seq;
            }

            _lastSentGeneration = _generation;
            if (synchronousResponse != null)
            {
                HandleCombatResponse(attempt, synchronousResponse);
            }
            else if (synchronousFailure != null)
            {
                HandleCombatFailure(attempt);
            }

            return true;
        }

        private bool IsActiveCombatAttempt(int attempt)
        {
            return !_disposed && !_cancellingCombat && _awaitingCombat && attempt == _combatAttempt;
        }

        private void CancelActiveCombatRequest()
        {
            var seq = _combatSeq;
            _awaitingCombat = false;
            _combatSeq = 0;
            _combatAttempt++;
            if (seq == 0)
            {
                return;
            }

            _cancellingCombat = true;
            _client.CancelRequest(seq);
            _cancellingCombat = false;
        }

        private void HandleCombatResponse(int attempt, CombatResultResp response)
        {
            if (!IsActiveCombatAttempt(attempt) || _request == null || response == null ||
                response.RunId != _request.RunId)
            {
                return;
            }

            _combatSeq = 0;
            if (!response.Success || response.Archive == null)
            {
                Fail();
                return;
            }

            _awaitingCombat = false;
            _response = response.Clone();
            State = BattleSettlementState.Pending;
            SaveAcceptedArchive();
        }

        private bool SaveAcceptedArchive()
        {
            if (_response?.Archive == null)
            {
                Fail();
                return false;
            }

            _awaitingSave = true;
            if (_archiveService.Save(_response.Archive))
            {
                return true;
            }

            _awaitingSave = false;
            Fail();
            return false;
        }

        private void HandleArchiveSaved()
        {
            if (_disposed || !_awaitingSave || _response == null)
            {
                return;
            }

            _awaitingSave = false;
            State = BattleSettlementState.Saved;
            var result = new BattleSettlementResult
            {
                State = BattleSettlementState.Saved,
                RewardGold = _response.RewardGold,
                RewardExp = _response.RewardExp,
                Duplicate = _response.Duplicate,
                Archive = _response.Archive.Clone()
            };
            var completed = _completed;
            ResetActiveRun();
            try
            {
                _applyArchive(result.Archive.Clone());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            completed?.Invoke(result);
        }

        private void HandleArchiveFailed(string reason)
        {
            if (!_disposed && _awaitingSave)
            {
                _awaitingSave = false;
                Fail();
            }
        }

        private void HandleCombatFailure(int attempt)
        {
            if (!IsActiveCombatAttempt(attempt))
            {
                return;
            }

            _combatSeq = 0;
            _awaitingCombat = false;
            Fail();
        }

        private void Fail()
        {
            _awaitingCombat = false;
            _awaitingSave = false;
            _saveOnReady = false;
            State = BattleSettlementState.Failed;
            Notify(new BattleSettlementResult
            {
                State = BattleSettlementState.Failed,
                RewardGold = _response?.RewardGold ?? 0,
                RewardExp = _response?.RewardExp ?? 0,
                Duplicate = _response?.Duplicate ?? false,
                Archive = _response?.Archive?.Clone()
            });
        }

        private void Notify(BattleSettlementResult result)
        {
            _completed?.Invoke(result);
        }

        private void ResetActiveRun()
        {
            _request = null;
            _response = null;
            _completed = null;
            _awaitingCombat = false;
            _awaitingSave = false;
            _saveOnReady = false;
            _combatSeq = 0;
            _lastSentGeneration = int.MinValue;
        }
    }
}
