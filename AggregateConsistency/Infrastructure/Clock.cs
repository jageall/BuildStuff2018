using System;

namespace AggregateConsistency.Infrastructure
{
	public static class Clock
	{
		public static Func<DateTimeOffset> UtcNowFunc = () => DateTimeOffset.UtcNow;
		public static DateTimeOffset Now => UtcNowFunc();
	}
}