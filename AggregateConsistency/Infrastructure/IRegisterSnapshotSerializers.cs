using System;
using Newtonsoft.Json.Linq;

namespace AggregateConsistency.Infrastructure
{
	public interface IRegisterSnapshotSerializers
	{
		void Register<T>(Func<T, IMetadata, JObject> serializer);
	}
}