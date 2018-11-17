using System;

namespace AggregateConsistency
{
    public class ChangeName : UserCommand
    {
        public ChangeName(Guid commandId, string userId, string name) : base(commandId, userId)
        {
            Name = name;
        }

        public string Name { get; }
    }
}