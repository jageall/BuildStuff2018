using System;

namespace AggregateConsistency
{
    public class ManualUnlock : UserCommand{
        public ManualUnlock(Guid commandId, string userId) : base(commandId, userId)
        {
        }
    }
}