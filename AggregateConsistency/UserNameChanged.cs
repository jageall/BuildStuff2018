namespace AggregateConsistency
{
    public class UserNameChanged : UserEvent
    {
        public string Name { get; }

        public UserNameChanged(string userId, string name):base(userId)
        {
            Name = name;
        }
    }
}