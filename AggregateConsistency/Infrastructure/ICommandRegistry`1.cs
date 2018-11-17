using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AggregateConsistency.Infrastructure
{
	/// <summary>
	/// Defines the helper interface for registering command for aggregate type.
	/// Simplifies initialization of any aggregate command handler.
	/// </summary>
	public interface ICommandRegistry<TAggregate>
		where TAggregate : Aggregate
	{
		ICommandRegistry<TAggregate> Create<TCommand>(Func<TCommand, TAggregate> creator,
			Func<string, IReadOnlyList<Event>, Task<IReadOnlyList<StreamVersion>>> save)
			where TCommand : ICommand;

		ICommandRegistry<TAggregate> Execute<TCommand>(Action<TCommand, TAggregate> handler,
			Func<string, Task<LoadResult<TAggregate>>> load,
			Func<string, IReadOnlyList<Event>, IReadOnlyList<StreamVersion>, Task<IReadOnlyList<StreamVersion>>> save)
			where TCommand : ICommand;

		ICommandRegistry<TAggregate> Execute<TCommand, TResult>(Func<TCommand, TAggregate, TResult> handler,
			Func<string, Task<LoadResult<TAggregate>>> load,
			Func<string, IReadOnlyList<Event>, IReadOnlyList<StreamVersion>, Task<IReadOnlyList<StreamVersion>>> save)
			where TCommand : ICommand;
	}
}