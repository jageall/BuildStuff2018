using System;

namespace AggregateConsistency
{
    public class AccountLockedOut : UserLoginEvent
    {
        public DateTimeOffset LockoutUntil { get; }

        public AccountLockedOut(string userId, DateTimeOffset lockoutUntil) : base(userId)
        {
            LockoutUntil = lockoutUntil;
        }
    }
}