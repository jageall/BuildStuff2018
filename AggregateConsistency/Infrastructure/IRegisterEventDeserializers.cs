using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace AggregateConsistency.Infrastructure
{
	public interface IRegisterEventDeserializers
	{
		void Register(Type scope, string name, int version,
			Func<JObject, IReadOnlyMetadata, IEnumerable<Event>> deserializer, CreatePreprocessor preprocessor);
	}
}