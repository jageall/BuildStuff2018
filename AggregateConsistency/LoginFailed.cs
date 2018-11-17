using System;

namespace AggregateConsistency
{
    public class LoginFailed : UserLoginEvent
    {
        public DateTimeOffset At { get; }

        public LoginFailed(string userId, DateTimeOffset at) : base(userId)
        {
            At = at;
        }
    }
}