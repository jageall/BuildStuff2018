namespace AggregateConsistency.Infrastructure
{
	public interface ISnapshot<T>
		where T : class
	{
		void ApplySnapshot(T snapshot);
		T TakeSnapshot();
	}
}