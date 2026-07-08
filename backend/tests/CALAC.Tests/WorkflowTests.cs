using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using CALAC.Domain.Workflow;
using Xunit;

namespace CALAC.Tests;

public class WorkflowTests
{
    [Fact]
    public void GoodsReceiptWorkflow_AllowsValidTransitions()
    {
        var workflow = new GoodsReceiptWorkflow();
        var receipt = new GoodsReceipt { Status = GoodsReceiptStatus.Draft };

        Assert.True(workflow.CanTransition(receipt, GoodsReceiptStatus.InProgress));
        Assert.True(workflow.CanTransition(receipt, GoodsReceiptStatus.Cancelled));
    }

    [Fact]
    public void GoodsReceiptWorkflow_BlocksInvalidTransitions()
    {
        var workflow = new GoodsReceiptWorkflow();
        var receipt = new GoodsReceipt { Status = GoodsReceiptStatus.Completed };

        Assert.False(workflow.CanTransition(receipt, GoodsReceiptStatus.InProgress));
        Assert.False(workflow.CanTransition(receipt, GoodsReceiptStatus.Draft));
    }
}
