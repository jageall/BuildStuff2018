using System;

namespace AggregateConsistency.Infrastructure
{
	public struct SerializedEvent
	{
		public SerializedEvent(Guid id, string type, byte[] data, byte[] metadata, string stream, long streamRevision) {
			Id = id;
			Type = type;
			Data = data;
			Metadata = metadata;
			Stream = stream;
			StreamRevision = streamRevision;
			IsValid = true;
		}

		public Guid Id { get; }
		public string Type { get; }

		public byte[] Data { get; }

		public byte[] Metadata { get; }
		public string Stream { get; }
		public long StreamRevision { get; }
		public bool IsValid { get; }

		public static readonly SerializedEvent None = new SerializedEvent();
	}
}