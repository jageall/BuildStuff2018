using System;

namespace AggregateConsistency
{
    public class VerifyEmail : UserCommand{
        public VerifyEmail(Guid commandId, string userId, string token) : base(commandId, userId)
        {
            Token = token;
        }

        public string Token { get; }
    }
}