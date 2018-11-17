using System;

namespace AggregateConsistency
{
    public class ChangeEmail : UserCommand{
        public ChangeEmail(Guid commandId, string userId, string newEmailAddress, string password) : base(commandId, userId)
        {
            NewEmailAddress = newEmailAddress;
            Password = password;
        }

        public string NewEmailAddress { get; }
        public string Password { get; }
    }
}