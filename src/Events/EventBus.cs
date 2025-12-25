using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#nullable enable

namespace SpaceTradeEngine.Events
{
    /// <summary>
    /// Lightweight, high-performance event bus with typed pub/sub, priorities, filters,
    /// sync/async dispatch, and optional queued processing.
    /// </summary>
    public class EventBus
    {
        private sealed class Subscription
        {
            public int Priority { get; }
            public Delegate Handler { get; }
            public Func<object, bool>? Filter { get; }
            public bool Once { get; }
            public SubscriptionToken Token { get; }

            public Subscription(int priority, Delegate handler, Func<object, bool>? filter, bool once, SubscriptionToken token)
            {
                Priority = priority;
                Handler = handler;
                Filter = filter;
                Once = once;
                Token = token;
            }
        }

        private readonly ConcurrentDictionary<Type, List<Subscription>> _subscriptions = new();
        private readonly ConcurrentDictionary<Type, object?> _stickyLast = new();
        private readonly ConcurrentQueue<object> _queue = new();
        private readonly object _listLock = new();

        /// <summary>
        /// Subscribe to an event type T.
        /// </summary>
        /// <param name="handler">Handler delegate receiving T.</param>
        /// <param name="priority">Higher values execute first.</param>
        /// <param name="filter">Optional predicate to filter events before handling.</param>
        /// <param name="once">If true, handler is removed after first invocation.</param>
        /// <param name="deliverLastSticky">If true and a sticky event exists, immediately deliver it.</param>
        public SubscriptionToken Subscribe<T>(Action<T> handler, int priority = 0, Predicate<T>? filter = null, bool once = false, bool deliverLastSticky = false)
            where T : class
        {
            var type = typeof(T);
            var token = new SubscriptionToken(type);
            var sub = new Subscription(priority, handler, filter != null ? (o => filter((T)o)) : null, once, token);

            _subscriptions.AddOrUpdate(type,
                _ => new List<Subscription> { sub },
                (_, list) =>
                {
                    lock (_listLock)
                    {
                        list.Add(sub);
                        list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                        return list;
                    }
                });

            if (deliverLastSticky && _stickyLast.TryGetValue(type, out var last) && last is T tLast)
            {
                handler(tLast);
                if (once)
                    Unsubscribe(token);
            }

            return token;
        }

        /// <summary>
        /// Unsubscribe using token.
        /// </summary>
        public void Unsubscribe(SubscriptionToken token)
        {
            if (_subscriptions.TryGetValue(token.EventType, out var list))
            {
                lock (_listLock)
                {
                    list.RemoveAll(s => s.Token.Id == token.Id);
                }
            }
        }

        /// <summary>
        /// Publish an event synchronously to all subscribers.
        /// </summary>
        public int Publish<T>(T evt, bool sticky = false) where T : class
        {
            if (sticky)
                _stickyLast[typeof(T)] = evt;

            if (!_subscriptions.TryGetValue(typeof(T), out var list))
                return 0;

            int invoked = 0;
            List<Subscription> snapshot;
            lock (_listLock)
            {
                snapshot = list.ToList();
            }

            foreach (var sub in snapshot)
            {
                if (sub.Filter?.Invoke(evt!) == false)
                    continue;

                try
                {
                    ((Action<T>)sub.Handler)(evt);
                    invoked++;
                }
                catch (Exception)
                {
                    // Swallow to avoid cascading failures; consider logging if logging system exists
                }

                if (sub.Once)
                    Unsubscribe(sub.Token);
            }

            return invoked;
        }

        /// <summary>
        /// Publish an event asynchronously to all subscribers.
        /// </summary>
        public async Task<int> PublishAsync<T>(T evt, bool sticky = false, CancellationToken ct = default) where T : class
        {
            if (sticky)
                _stickyLast[typeof(T)] = evt;

            if (!_subscriptions.TryGetValue(typeof(T), out var list))
                return 0;

            int invoked = 0;
            List<Subscription> snapshot;
            lock (_listLock)
            {
                snapshot = list.ToList();
            }

            foreach (var sub in snapshot)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (sub.Filter?.Invoke(evt!) == false)
                    continue;

                try
                {
                    var d = (Action<T>)sub.Handler;
                    await Task.Run(() => d(evt), ct);
                    invoked++;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Swallow to avoid cascading failures; consider logging
                }

                if (sub.Once)
                    Unsubscribe(sub.Token);
            }

            return invoked;
        }

        /// <summary>
        /// Enqueue an event for later dispatch via <see cref="DispatchQueued"/>.
        /// </summary>
        public void Enqueue<T>(T evt, bool sticky = false) where T : class
        {
            if (sticky)
                _stickyLast[typeof(T)] = evt;
            _queue.Enqueue(evt!);
        }

        /// <summary>
        /// Dispatch up to <paramref name="maxEvents"/> queued events synchronously.
        /// </summary>
        public int DispatchQueued(int maxEvents = int.MaxValue)
        {
            int count = 0;
            while (count < maxEvents && _queue.TryDequeue(out var obj))
            {
                count++;
                var type = obj.GetType();
                var publishMethod = typeof(EventBus).GetMethod(nameof(Publish))!.MakeGenericMethod(type);
                publishMethod.Invoke(this, new[] { obj, false });
            }
            return count;
        }

        /// <summary>
        /// Clear all subscriptions for an event type.
        /// </summary>
        public void Clear<T>() where T : class
        {
            _subscriptions.TryRemove(typeof(T), out _);
            _stickyLast.TryRemove(typeof(T), out _);
        }

        /// <summary>
        /// Clear all subscriptions and sticky events.
        /// </summary>
        public void ClearAll()
        {
            _subscriptions.Clear();
            _stickyLast.Clear();
            while (_queue.TryDequeue(out _)) { }
        }
    }
}
