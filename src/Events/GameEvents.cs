using System;
using Microsoft.Xna.Framework;

namespace SpaceTradeEngine.Events
{
    // Common engine/game events. Extend as needed.

    public abstract record BaseEvent(DateTime Timestamp) : IEvent;

    public record EntityDamagedEvent(int EntityId, float Damage, float NewHealth, int? AttackerId, DateTime Timestamp) : BaseEvent(Timestamp);

    public record EntityDestroyedEvent(int EntityId, int? KillerId, DateTime Timestamp) : BaseEvent(Timestamp);

    public record CollisionEvent(int EntityAId, int EntityBId, Vector2 Point, Vector2 Normal, DateTime Timestamp) : BaseEvent(Timestamp);

    public record TargetAcquiredEvent(int EntityId, int TargetId, DateTime Timestamp) : BaseEvent(Timestamp);

    public record TradeCompletedEvent(int TraderId, int FromStationId, int ToStationId, string WareId, int Quantity, float Profit, DateTime Timestamp) : BaseEvent(Timestamp);

    public record AIStateChangedEvent(int EntityId, string FromState, string ToState, DateTime Timestamp) : BaseEvent(Timestamp);

    public record SelectionChangedEvent(int? PreviousEntityId, int? CurrentEntityId, DateTime Timestamp) : BaseEvent(Timestamp);

    public record TagAddedEvent(int EntityId, string Tag, DateTime Timestamp) : BaseEvent(Timestamp);

    public record TagRemovedEvent(int EntityId, string Tag, DateTime Timestamp) : BaseEvent(Timestamp);

    public static class EventFactory
    {
        public static DateTime Now() => DateTime.UtcNow;
    }
}
