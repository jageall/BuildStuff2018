using System;

namespace AggregateConsistency.Infrastructure
{
	public class SnapshotStream
	{
		public SnapshotStream(string originStream, Type type) {
			OriginStream = originStream;
			Type = type;
		}

		public string OriginStream { get; }

		public Type Type { get; }

		public override string ToString() {
			return $"snapshot-{OriginStream}_{Type.Name}";
		}
	}
}