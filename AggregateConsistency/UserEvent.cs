using AggregateConsistency.Infrastructure;

namespace AggregateConsistency
{
    public abstract class UserEvent : Event
    {
        public string UserId { get; }

        protected UserEvent(string userId)
        {
            UserId = userId;
        }
    }
}