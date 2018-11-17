namespace AggregateConsistency
{
    public class PasswordResetTokenCreated : UserEvent {
        public string Token { get; }

        public PasswordResetTokenCreated(string userId, string token) : base(userId)
        {
            Token = token;
        }
    }
}