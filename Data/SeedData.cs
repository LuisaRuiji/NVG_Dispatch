using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;

namespace NVGInventory.Data;

internal static class SeedData
{
    private static readonly DateTime SeededAt = new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Utc);

    public static readonly Role[] Roles =
    [
        new Role { Id = 1, Name = RoleNames.InventoryOfficer },
        new Role { Id = 2, Name = RoleNames.Manager },
        new Role { Id = 3, Name = RoleNames.HeadOfFinance },
        new Role { Id = 4, Name = RoleNames.Ceo },
        new Role { Id = 5, Name = RoleNames.Driver },
        new Role { Id = 6, Name = RoleNames.Admin },
        new Role { Id = 7, Name = RoleNames.SuperAdmin },
        new Role { Id = 8, Name = RoleNames.Dispatcher },
        new Role { Id = 9, Name = RoleNames.Customer }
    ];

    public static readonly Workflow[] Workflows =
    [
        new Workflow { WorkflowKey = WorkflowKeys.MaintenanceIssueApproval, Name = "Maintenance Issue Approval", IsActive = true },
        new Workflow { WorkflowKey = WorkflowKeys.BorrowApproval, Name = "Borrow Approval", IsActive = true },
        new Workflow { WorkflowKey = WorkflowKeys.AdjustmentApproval, Name = "Adjustment Approval", IsActive = true },
        new Workflow { WorkflowKey = WorkflowKeys.PoApproval, Name = "PO Approval", IsActive = true }
    ];

    public static readonly WorkflowStep[] WorkflowSteps =
    [
        new WorkflowStep
        {
            Id = Guid.Parse("5d4e1f9f-8e90-46a6-9d91-566a1e3b1d01"),
            WorkflowKey = WorkflowKeys.MaintenanceIssueApproval,
            StepOrder = 1,
            RequiredRole = RoleNames.InventoryOfficer,
            CreatedAt = SeededAt
        },
        new WorkflowStep
        {
            Id = Guid.Parse("1cf3b1a4-f741-4e0c-9c64-57f2a9aa1c02"),
            WorkflowKey = WorkflowKeys.MaintenanceIssueApproval,
            StepOrder = 2,
            RequiredRole = RoleNames.Manager,
            CreatedAt = SeededAt
        },
        new WorkflowStep
        {
            Id = Guid.Parse("39d4f82f-4ed3-4bfa-91a9-ffcb28e1a103"),
            WorkflowKey = WorkflowKeys.BorrowApproval,
            StepOrder = 1,
            RequiredRole = RoleNames.InventoryOfficer,
            CreatedAt = SeededAt
        },
        new WorkflowStep
        {
            Id = Guid.Parse("6d49fedd-f2af-4fd0-89ae-1e1bb9c8c204"),
            WorkflowKey = WorkflowKeys.BorrowApproval,
            StepOrder = 2,
            RequiredRole = RoleNames.Manager,
            CreatedAt = SeededAt
        },
        new WorkflowStep
        {
            Id = Guid.Parse("f2eb4a0f-3195-40cd-9050-5b7b9352e705"),
            WorkflowKey = WorkflowKeys.AdjustmentApproval,
            StepOrder = 1,
            RequiredRole = RoleNames.InventoryOfficer,
            CreatedAt = SeededAt
        },
        new WorkflowStep
        {
            Id = Guid.Parse("8e6f8b8e-6a64-4f4f-8b78-5d7dd6eb8706"),
            WorkflowKey = WorkflowKeys.AdjustmentApproval,
            StepOrder = 2,
            RequiredRole = RoleNames.Manager,
            CreatedAt = SeededAt
        },
        new WorkflowStep
        {
            Id = Guid.Parse("1b6e7c02-8132-4f2f-b53b-8db168a5e407"),
            WorkflowKey = WorkflowKeys.PoApproval,
            StepOrder = 1,
            RequiredRole = RoleNames.Manager,
            CreatedAt = SeededAt
        },
        new WorkflowStep
        {
            Id = Guid.Parse("a9f0f9ce-c8c9-47aa-8a71-427f1a6d2b08"),
            WorkflowKey = WorkflowKeys.PoApproval,
            StepOrder = 2,
            RequiredRole = RoleNames.HeadOfFinance,
            CreatedAt = SeededAt
        },
        new WorkflowStep
        {
            Id = Guid.Parse("b9bb7d7d-b2f0-4bb2-91b5-50f2c8188909"),
            WorkflowKey = WorkflowKeys.PoApproval,
            StepOrder = 3,
            RequiredRole = RoleNames.Ceo,
            CreatedAt = SeededAt
        }
    ];
}
