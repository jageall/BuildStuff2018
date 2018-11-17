using System;

namespace AggregateConsistency
{
    public class SetPassword : UserCommand{
        public string Token { get; }
        public string Password { get; }

        public SetPassword(Guid commandId, string userId, string token, string password) : base(commandId, userId)
        {
            Token = token;
            Password = password;
        }
    }
}