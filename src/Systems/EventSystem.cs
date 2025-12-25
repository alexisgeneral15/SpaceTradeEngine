using System;
using SpaceTradeEngine.Events;
#nullable enable

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// ECS-facing wrapper around EventBus. Provides per-frame queued dispatch
    /// and a single place to expose engine-wide events.
    /// </summary>
    public class EventSystem
    {
        public EventBus Bus { get; } = new EventBus();

        /// <summary>
        /// Dispatches a limited number of queued events each frame.
        /// </summary>
        /// <param name="deltaTime">Frame delta time (seconds).</param>
        /// <param name="maxEventsPerFrame">Cap to avoid hitches.</param>
        /// <returns>Number of events dispatched.</returns>
        public int Update(float deltaTime, int maxEventsPerFrame = 256)
        {
            return Bus.DispatchQueued(maxEventsPerFrame);
        }

        // Convenience publishing methods
        public void Publish<T>(T evt, bool sticky = false) where T : class => Bus.Publish(evt, sticky);
        public void Enqueue<T>(T evt, bool sticky = false) where T : class => Bus.Enqueue(evt, sticky);

        // Convenience subscribing methods
        public SubscriptionToken Subscribe<T>(Action<T> handler, int priority = 0, Predicate<T>? filter = null, bool once = false, bool deliverLastSticky = false)
            where T : class => Bus.Subscribe(handler, priority, filter, once, deliverLastSticky);

        public void Unsubscribe(SubscriptionToken token) => Bus.Unsubscribe(token);

        public void ClearAll() => Bus.ClearAll();
    }
}
