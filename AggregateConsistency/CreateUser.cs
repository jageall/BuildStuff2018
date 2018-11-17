using System;

namespace AggregateConsistency
{
    public class CreateUser : UserCommand{
        public string EmailAddress { get; }
        public string Name { get; }

        public CreateUser(Guid commandId, string userId, string emailAddress, string name) : base(commandId, userId)
        {
            EmailAddress = emailAddress;
            Name = name;
        }
    }
}