namespace AggregateConsistency
{
    public class PasswordResetRequested : UserEvent
    {
        public string EmailAddress { get; }
        public string Token { get; }

        public PasswordResetRequested(string userId, string emailAddress, string token) : base(userId)
        {
            EmailAddress = emailAddress;
            Token = token;
        }
    }
}