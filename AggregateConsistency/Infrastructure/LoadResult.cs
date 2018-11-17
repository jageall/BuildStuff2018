using System.Collections.Generic;

namespace AggregateConsistency.Infrastructure
{
	public class LoadResult<T>
		where T : IEventSourcedState
	{
		public IReadOnlyList<StreamVersion> ExpectedVersions { get; }

		public LoadResult(T loaded, IReadOnlyList<StreamVersion> expectedVersions) {
			ExpectedVersions = expectedVersions;
			Loaded = loaded;
		}

		public T Loaded { get; }

		public LoadResult<TDerived> To<TDerived>() where TDerived : T {
			return new LoadResult<TDerived>((TDerived) Loaded, ExpectedVersions);
		}
	}
}