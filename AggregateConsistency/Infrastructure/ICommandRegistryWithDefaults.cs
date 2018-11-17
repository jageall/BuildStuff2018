using System;

namespace AggregateConsistency.Infrastructure
{
	public interface ICommandRegistryWithDefaults<T>
		where T : Aggregate
	{
		ICommandRegistryWithDefaults<T> Create<TCommand>(Func<TCommand, T> creator) where TCommand : ICommand;
		ICommandRegistryWithDefaults<T> Execute<TCommand>(Action<TCommand, T> handler) where TCommand : ICommand;
		ICommandRegistryWithDefaults<T> Execute<TCommand, TResult>(Func<TCommand, T, TResult> handler) where TCommand : ICommand;
	}
}