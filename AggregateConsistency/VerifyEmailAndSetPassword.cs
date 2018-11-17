using System;

namespace AggregateConsistency
{
    public class VerifyEmailAndSetPassword : UserCommand{
        public string Token { get; }
        public string Password { get; }

        public VerifyEmailAndSetPassword(Guid commandId, string userId, string token, string password) : base(commandId, userId)
        {
            Token = token;
            Password = password;
        }
    }
}