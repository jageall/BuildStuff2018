namespace AggregateConsistency
{
    public class PasswordSet : UserEvent
    {
        public string Password { get; }

        public PasswordSet(string userId, string password) :base(userId)
        {
            Password = password;
        }
    }
}