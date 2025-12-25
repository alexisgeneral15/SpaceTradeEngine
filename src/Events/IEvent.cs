using System;

namespace SpaceTradeEngine.Events
{
    /// <summary>
    /// Base marker interface for all engine events.
    /// </summary>
    public interface IEvent
    {
        DateTime Timestamp { get; }
    }
}
