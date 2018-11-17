using System.Threading.Tasks;

namespace AggregateConsistency.Infrastructure
{
	public interface ICommandHandler
	{
		Task<Result> Execute(ICommand cmd);
	}
}