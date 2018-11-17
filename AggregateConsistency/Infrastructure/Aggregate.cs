using System;
using System.Collections.Generic;

namespace AggregateConsistency.Infrastructure
{
	public abstract class Aggregate : IEventSourcedState
	{
		private readonly EventDispatcher _dispatcher;
		private readonly List<Event> _pending;
		private readonly SnapshotDispatcher _snapshotDispatcher;

		protected Aggregate() {
			_pending = new List<Event>();
			_dispatcher = EventDispatcher.For(GetType());
			_snapshotDispatcher = SnapshotDispatcher.For(GetType());
		}

		Type IEventSourcedState.SnapshotType => _snapshotDispatcher.SnapshotType;
		bool IEventSourcedState.IsSnapshottable => _snapshotDispatcher.IsSnapshottable;
		SnapshotStream IEventSourcedState.SnapshotStream(string originStream) => _snapshotDispatcher.SnapshotStream(originStream);

		void IEventSourcedState.Apply(Event @event) {
			Apply(@event);
		}

		bool IEventSourcedState.ApplySnapshot(object snapshot) {
			return _snapshotDispatcher.Apply(this, snapshot);
		}

		object IEventSourcedState.TakeSnapshot() {
			return _snapshotDispatcher.Take(this);
		}

		protected void Append(Event @event) {
			Apply(@event);
			_pending.Add(@event);
		}

		void Apply(Event @event) {
			_dispatcher.Dispatch(this, @event);
		}

		internal IReadOnlyList<Event> PendingEvents() {
			var events = _pending.ToArray();
			_pending.Clear();
			return events;
		}
	}
}