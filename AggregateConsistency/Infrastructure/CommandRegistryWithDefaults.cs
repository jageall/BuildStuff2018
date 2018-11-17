using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AggregateConsistency.Infrastructure
{
	internal class CommandRegistryWithDefaults<T> : ICommandRegistryWithDefaults<T> where T : Aggregate
	{
		private readonly ICommandRegistry<T> _registry;
		private readonly Func<string, IReadOnlyList<Event>, Task<IReadOnlyList<StreamVersion>>> _saveNew;
		private readonly Func<string, IReadOnlyList<Event>, IReadOnlyList<StreamVersion>, Task<IReadOnlyList<StreamVersion>>> _saveExisting;
		private readonly Func<string, Task<LoadResult<T>>> _loadExisting;

		public CommandRegistryWithDefaults(
			ICommandRegistry<T> registry,
			Func<string, IReadOnlyList<Event>, Task<IReadOnlyList<StreamVersion>>> saveNew,
			Func<string, Task<LoadResult<T>>> loadExisting,
			Func<string, IReadOnlyList<Event>, IReadOnlyList<StreamVersion>, Task<IReadOnlyList<StreamVersion>>> saveExisting) {
			_registry = registry;
			_saveNew = saveNew;
			_loadExisting = loadExisting;
			_saveExisting = saveExisting;
		}

		public ICommandRegistryWithDefaults<T> Create<TCommand>(Func<TCommand, T> creator) where TCommand : ICommand {
			_registry.Create(creator, _saveNew);
			return this;
		}

		public ICommandRegistryWithDefaults<T> Execute<TCommand>(Action<TCommand, T> handler) where TCommand : ICommand {
			_registry.Execute(handler, _loadExisting, _saveExisting);
			return this;
		}

		public ICommandRegistryWithDefaults<T> Execute<TCommand, TResult>(Func<TCommand, T, TResult> handler)
			where TCommand : ICommand {
			_registry.Execute(handler, _loadExisting, _saveExisting);
			return this;
		}
	}
}