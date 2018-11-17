using System;

namespace AggregateConsistency
{
    public class RequestResetPassword : UserCommand{
        public RequestResetPassword(Guid commandId, string userId) : base(commandId, userId)
        {
        }
    }
}