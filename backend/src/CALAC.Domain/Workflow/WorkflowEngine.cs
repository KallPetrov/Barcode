namespace CALAC.Domain.Workflow;

public interface IWorkflowState<TStatus> where TStatus : Enum
{
    TStatus Status { get; }
}

public class WorkflowTransition<TStatus>(TStatus from, TStatus to, Func<bool>? validator = null)
    where TStatus : Enum
{
    public TStatus From { get; } = from;
    public TStatus To { get; } = to;
    public Func<bool>? Validator { get; } = validator;

    public bool CanTransition() => Validator?.Invoke() ?? true;
}

public abstract class WorkflowEngine<TEntity, TStatus>
    where TEntity : IWorkflowState<TStatus>
    where TStatus : Enum
{
    protected abstract IEnumerable<WorkflowTransition<TStatus>> Transitions { get; }

    public virtual bool CanTransition(TEntity entity, TStatus targetStatus)
    {
        var transition = Transitions.FirstOrDefault(t =>
            t.From.Equals(entity.Status) && t.To.Equals(targetStatus));

        return transition != null && transition.CanTransition();
    }
}
