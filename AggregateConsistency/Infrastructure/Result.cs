using System;
using System.Collections.Generic;
using System.Linq;

namespace AggregateConsistency.Infrastructure
{
	public class Result
	{
		public static readonly Result Nothing = new Result();

		protected Result() {
		}

		public Result With<T>(ICommand cmd, T result) {
			if(this == Nothing) {
				return new SingleResult<T>(cmd.Id, result);
			}
			return new MultipleResult(this, new SingleResult<T>(cmd.Id, result));
		}

		public T Value<T>(ICommand cmd) {
			return Value<T>(cmd.Id);
		}

		public T Value<T>(Guid commandId) {
			var single = this as SingleResult<T>;
			SingleResult<T> found = null;
			if(single != null && single.Id == commandId)
				found = single;
		    if(this is MultipleResult multiple) {
				found = multiple.Find(commandId) as SingleResult<T>;
			}
			if(found != null)
				return found.Value;
			throw new InvalidOperationException($"No value found matching the type {typeof(T).FullName} and command id {commandId}");
		}

		public interface ISingleResult
		{
			Guid Id { get; }
		}

		public class SingleResult<T> : Result, ISingleResult
		{
			public SingleResult(Guid id, T value) {
				Id = id;
				Value = value;
			}

			public Guid Id { get; }

			public T Value { get; }
		}

		class MultipleResult : Result
		{
			private readonly List<ISingleResult> _results;

			public MultipleResult(Result other, ISingleResult next) {
				var multi = other as MultipleResult;
				_results = multi != null ? multi._results : new List<ISingleResult> {(ISingleResult) other};
				if(Enumerable.Any(_results, x => x.Id == next.Id))
					throw new InvalidOperationException($"Result already added for command id {next.Id}");
				_results.Add(next);
			}

			public Result Find(Guid commandId) {
				return (Result) Enumerable.SingleOrDefault(_results, x => x.Id == commandId);
			}
		}
	}
}