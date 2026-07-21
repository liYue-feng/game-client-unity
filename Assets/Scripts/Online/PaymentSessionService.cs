using System;
using Game.Network;
using Game.Protocol;
using UnityEngine;

namespace Game.Online
{
    public sealed class PaymentSessionService : IDisposable
    {
        private readonly NetworkClient _client;
        private readonly PendingRequestOwner _requests;
        private readonly IDisposable _paymentSubscription;
        private bool _disposed;

        public PaymentSessionService(NetworkClient client = null)
        {
            _client = client ?? NetworkClient.Instance;
            _requests = new PendingRequestOwner(_client);
            _paymentSubscription = _client.On<PayResultNotify>(
                MsgID.PayResultNotify,
                PublishPaymentResult);
        }

        public event Action<PayResultNotify> PaymentResult;

        public bool CreateOrder(
            int productId,
            Action<CreateOrderResp> onSuccess,
            Action<string> onFailure,
            out uint seq)
        {
            if (_disposed)
            {
                seq = 0;
                return false;
            }

            return _requests.Request<CreateOrderReq, CreateOrderResp>(
                MsgID.CreateOrderReq,
                MsgID.CreateOrderResp,
                new CreateOrderReq { ProductId = productId },
                onSuccess,
                onFailure,
                out seq);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _paymentSubscription.Dispose();
            PaymentResult = null;
            _requests.Dispose();
        }

        private void PublishPaymentResult(PayResultNotify notification)
        {
            var observers = PaymentResult;
            if (observers == null)
            {
                return;
            }

            foreach (Action<PayResultNotify> observer in observers.GetInvocationList())
            {
                try
                {
                    observer(notification);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
