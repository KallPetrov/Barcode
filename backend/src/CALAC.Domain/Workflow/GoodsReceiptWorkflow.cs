using CALAC.Domain.Entities;
using CALAC.Domain.Enums;

namespace CALAC.Domain.Workflow;

public class GoodsReceiptWorkflow : WorkflowEngine<GoodsReceipt, GoodsReceiptStatus>
{
    protected override IEnumerable<WorkflowTransition<GoodsReceiptStatus>> Transitions => new List<WorkflowTransition<GoodsReceiptStatus>>
    {
        new(GoodsReceiptStatus.Draft, GoodsReceiptStatus.InProgress),
        new(GoodsReceiptStatus.Draft, GoodsReceiptStatus.Cancelled),
        new(GoodsReceiptStatus.InProgress, GoodsReceiptStatus.Completed),
        new(GoodsReceiptStatus.InProgress, GoodsReceiptStatus.Cancelled)
    };
}
