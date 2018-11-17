namespace AggregateConsistency
{
    public class EmailAddressVerificationRequested : UserEvent
    {
        public string EmailAddress { get; }
        public string Token { get; }

        public EmailAddressVerificationRequested(string userId, string emailAddress, string token):base(userId)
        {
            EmailAddress = emailAddress;
            Token = token;
        }
    }
}