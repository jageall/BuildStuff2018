namespace AggregateConsistency
{
    public class UserCreated : UserEvent
    {
        public string EmailAddress { get; }
        public string Name { get; }

        public UserCreated(string userId, string emailAddress, string name) : base(userId)
        {
            EmailAddress = emailAddress;
            Name = name;
        }
    }
}