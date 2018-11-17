namespace AggregateConsistency
{
    public class EmailAddressVerified : UserEvent
    {
        public string EmailAddress { get; }

        public EmailAddressVerified(string userId, string emailAddress) : base(userId)
        {
            EmailAddress = emailAddress;
        }
    }
}