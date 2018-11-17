using System;

namespace AggregateConsistency
{
    public class ManualLock : UserCommand{
        public ManualLock(Guid commandId, string userId) : base(commandId, userId)
        {
        }
    }
}