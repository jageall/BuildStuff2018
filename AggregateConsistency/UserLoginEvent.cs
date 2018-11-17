namespace AggregateConsistency
{
    public abstract class UserLoginEvent : UserEvent
    {
        protected UserLoginEvent(string userId) : base(userId)
        {
        }
    }
}