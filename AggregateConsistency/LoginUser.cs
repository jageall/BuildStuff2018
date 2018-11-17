using System;

namespace AggregateConsistency
{
    public class LoginUser : UserCommand{
        public LoginUser(Guid commandId, string userId, string password) : base(commandId, userId)
        {
            Password = password;
        }

        public string Password { get; }
    }
}