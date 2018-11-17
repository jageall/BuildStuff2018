using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AggregateConsistency.Infrastructure
{
	/// <summary>
	/// Helper for registering command for aggregate type.
	/// Simplifies initialization of any aggregate command handler.
	/// </summary>
	class CommandRegistry<T> : ICommandRegistry<T> where T : Aggregate
	{
		private readonly ICommandRegistry _registry;

		public CommandRegistry(ICommandRegistry registry) {
			_registry = registry;
		}

		public ICommandRegistry<T> Create<TCommand>(Func<TCommand, T> creator, Func<string, IReadOnlyList<Event>, Task<IReadOnlyList<StreamVersion>>> save)
			where TCommand : ICommand {
			_registry.Create(creator, save);
			return this;
		}

		public ICommandRegistry<T> Execute<TCommand>(
			Action<TCommand, T> handler,
			Func<string, Task<LoadResult<T>>> load,
			Func<string, IReadOnlyList<Event>, IReadOnlyList<StreamVersion>, Task<IReadOnlyList<StreamVersion>>> save) where TCommand : ICommand {
			_registry.Execute(handler, load, save);
			return this;
		}

		public ICommandRegistry<T> Execute<TCommand, TResult>(
			Func<TCommand, T, TResult> handler,
			Func<string, Task<LoadResult<T>>> load,
			Func<string, IReadOnlyList<Event>, IReadOnlyList<StreamVersion>, Task<IReadOnlyList<StreamVersion>>> save) where TCommand : ICommand {
			_registry.Execute(handler, load, save);
			return this;
		}
	}
}