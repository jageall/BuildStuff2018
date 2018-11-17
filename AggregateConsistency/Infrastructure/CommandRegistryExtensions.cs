using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AggregateConsistency.Infrastructure
{
	public static class CommandRegistryExtensions
	{
		public static ICommandRegistry<T> For<T>(this ICommandRegistry registry)
			where T : Aggregate {
			return new CommandRegistry<T>(registry);
		}

		public static ICommandRegistryWithDefaults<T> WithDefaults<T>(
			this ICommandRegistry registry,
			Func<string, IReadOnlyList<Event>, Task<IReadOnlyList<StreamVersion>>> saveNew,
			Func<string, Task<LoadResult<T>>> loadExisting,
			Func<string, IReadOnlyList<Event>, IReadOnlyList<StreamVersion>, Task<IReadOnlyList<StreamVersion>>>
				saveExisting
		)
			where T : Aggregate {
			return WithDefaults(registry.For<T>(), saveNew, loadExisting, saveExisting);
		}

		public static ICommandRegistryWithDefaults<T> WithDefaults<T>(
			this ICommandRegistry<T> registry,
			Func<string, IReadOnlyList<Event>, Task<IReadOnlyList<StreamVersion>>> saveNew,
			Func<string, Task<LoadResult<T>>> loadExisting,
			Func<string, IReadOnlyList<Event>, IReadOnlyList<StreamVersion>, Task<IReadOnlyList<StreamVersion>>>
				saveExisting
		)
			where T : Aggregate {
			return new CommandRegistryWithDefaults<T>(registry, saveNew, loadExisting, saveExisting);
		}
	}
}