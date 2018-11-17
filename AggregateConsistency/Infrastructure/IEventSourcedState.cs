using System;

namespace AggregateConsistency.Infrastructure
{
	public interface IEventSourcedState
	{
		void Apply(Event @event);
		bool IsSnapshottable { get; }
		SnapshotStream SnapshotStream(string originStream);
		Type SnapshotType { get; }
		object TakeSnapshot();
		bool ApplySnapshot(object snapshot);
	}
}