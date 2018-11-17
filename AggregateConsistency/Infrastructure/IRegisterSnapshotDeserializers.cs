using System;
using Newtonsoft.Json.Linq;

namespace AggregateConsistency.Infrastructure
{
	public interface IRegisterSnapshotDeserializers
	{
		void Register<T>(Func<JObject, IReadOnlyMetadata, T> serializer);
	}
}