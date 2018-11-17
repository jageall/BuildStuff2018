using System;

namespace AggregateConsistency.Infrastructure
{
	public abstract class Command : ICommand
	{
		private readonly Guid _id;
		private readonly string _targetIdentifier;

		protected Command(Guid commandId, string targetIdentifier) {
			_id = commandId;
			_targetIdentifier = targetIdentifier;
		}

		Guid ICommand.Id => _id;

		string ICommand.TargetIdentifier => _targetIdentifier;
	}
}