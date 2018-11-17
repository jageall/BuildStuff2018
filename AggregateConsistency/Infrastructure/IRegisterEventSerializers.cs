using System;
using Newtonsoft.Json.Linq;

namespace AggregateConsistency.Infrastructure
{
	public interface IRegisterEventSerializers
	{
		void Register<T>(string typeName, int version, Func<T, IMetadata, JObject> serializer, CreatePostprocessor postProcessor)
			where T : Event;
	}
}