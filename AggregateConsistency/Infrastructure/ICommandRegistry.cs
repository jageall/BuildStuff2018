using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AggregateConsistency.Infrastructure
{
	public interface ICommandRegistry
	{
		ICommandRegistry Create<TCommand, TAggregate>(Func<TCommand, TAggregate> creator,
			Func<string, IReadOnlyList<Event>, Task<IReadOnlyList<StreamVersion>>> save)
			where TAggregate : Aggregate
			where TCommand : ICommand;

		ICommandRegistry Execute<TCommand, TAggregate>(Action<TCommand, TAggregate> action,
			Func<string, Task<LoadResult<TAggregate>>> load,
			Func<string, IReadOnlyList<Event>, IReadOnlyList<StreamVersion>, Task<IReadOnlyList<StreamVersion>>> save)
			where TAggregate : Aggregate
			where TCommand : ICommand;

		ICommandRegistry Execute<TCommand, TAggregate, TResult>(Func<TCommand, TAggregate, TResult> action,
			Func<string, Task<LoadResult<TAggregate>>> load,
			Func<string, IReadOnlyList<Event>, IReadOnlyList<StreamVersion>, Task<IReadOnlyList<StreamVersion>>> save)
			where TAggregate : Aggregate
			where TCommand : ICommand;
	}
}