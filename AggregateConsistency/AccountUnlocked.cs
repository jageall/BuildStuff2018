using System;
using System.Collections.Generic;
using System.Text;

namespace AggregateConsistency
{
    public class AccountUnlocked : UserLoginEvent
    {
        public AccountUnlocked(string userId) : base(userId)
        {
        }
    }
}
