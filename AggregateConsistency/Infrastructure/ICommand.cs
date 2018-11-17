using System;

namespace AggregateConsistency.Infrastructure
{
	public interface ICommand
	{
		Guid Id { get; }
		string TargetIdentifier { get; }
	}
}