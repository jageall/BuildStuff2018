using System;

namespace AggregateConsistency
{
    public class LoginSucceeded : UserLoginEvent
    {
        public DateTimeOffset At { get; }

        public LoginSucceeded(string userId, DateTimeOffset at) : base(userId)
        {
            At = at;
        }
    }
}