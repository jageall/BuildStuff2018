using System;
using AggregateConsistency.Infrastructure;

namespace AggregateConsistency
{
    public abstract class UserCommand : Command
    {
        public string UserId { get; }

        protected UserCommand(Guid commandId, string userId) : base(commandId, userId) {
            UserId = userId;
        }
    }
}