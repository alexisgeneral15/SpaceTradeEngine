using System;

namespace SpaceTradeEngine.Events
{
    /// <summary>
    /// Opaque token returned when subscribing to an event. Use to unsubscribe.
    /// </summary>
    public sealed class SubscriptionToken
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Type EventType { get; }

        internal SubscriptionToken(Type eventType)
        {
            EventType = eventType;
        }

        public override string ToString() => $"SubscriptionToken({EventType.Name}, {Id})";
    }
}
