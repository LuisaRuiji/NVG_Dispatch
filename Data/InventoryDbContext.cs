using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Data;

public sealed class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<KitComponent> KitComponents => Set<KitComponent>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Request> Requests => Set<Request>();
    public DbSet<RequestLine> RequestLines => Set<RequestLine>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();
    public DbSet<InventoryAdjustmentLine> InventoryAdjustmentLines => Set<InventoryAdjustmentLine>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PurchaseOrderReceipt> PurchaseOrderReceipts => Set<PurchaseOrderReceipt>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanLine> LoanLines => Set<LoanLine>();
    public DbSet<LoanLineReturn> LoanLineReturns => Set<LoanLineReturn>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();
    public DbSet<StockLog> StockLogs => Set<StockLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AuthEvent> AuthEvents => Set<AuthEvent>();
    public DbSet<ModuleSetting> ModuleSettings => Set<ModuleSetting>();
    public DbSet<Customer> DispatchCustomers => Set<Customer>();
    public DbSet<Trip> DispatchTrips => Set<Trip>();
    public DbSet<TripStop> DispatchTripStops => Set<TripStop>();
    public DbSet<TripStatusHistory> DispatchTripStatusHistories => Set<TripStatusHistory>();
    public DbSet<TripDocument> DispatchTripDocuments => Set<TripDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");

        ConfigureUsers(modelBuilder);
        ConfigureRoles(modelBuilder);
        ConfigureUserRoles(modelBuilder);
        ConfigureAssets(modelBuilder);
        ConfigureInventory(modelBuilder);
        ConfigureKitComponents(modelBuilder);
        ConfigureSuppliers(modelBuilder);
        ConfigureRequests(modelBuilder);
        ConfigureRequestLines(modelBuilder);
        ConfigureInventoryAdjustments(modelBuilder);
        ConfigureInventoryAdjustmentLines(modelBuilder);
        ConfigurePurchaseOrders(modelBuilder);
        ConfigurePurchaseOrderLines(modelBuilder);
        ConfigurePurchaseOrderReceipts(modelBuilder);
        ConfigureLoans(modelBuilder);
        ConfigureLoanLines(modelBuilder);
        ConfigureLoanLineReturns(modelBuilder);
        ConfigureWorkflows(modelBuilder);
        ConfigureWorkflowSteps(modelBuilder);
        ConfigureApprovals(modelBuilder);
        ConfigureApprovalActions(modelBuilder);
        ConfigureStockLogs(modelBuilder);
        ConfigureAuditLogs(modelBuilder);
        ConfigureAuthEvents(modelBuilder);
        ConfigureModuleSettings(modelBuilder);
        ConfigureDispatching(modelBuilder);

        modelBuilder.Entity<Role>().HasData(SeedData.Roles);
        modelBuilder.Entity<Workflow>().HasData(SeedData.Workflows);
        modelBuilder.Entity<WorkflowStep>().HasData(SeedData.WorkflowSteps);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasColumnName("id");
            entity.Property(user => user.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            entity.Property(user => user.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(user => user.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(user => user.Username).IsUnique();
        });
    }

    private static void ConfigureRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(role => role.Id);
            entity.Property(role => role.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(role => role.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
            entity.HasIndex(role => role.Name).IsUnique();
        });
    }

    private static void ConfigureUserRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(userRole => new { userRole.UserId, userRole.RoleId });
            entity.Property(userRole => userRole.UserId).HasColumnName("user_id");
            entity.Property(userRole => userRole.RoleId).HasColumnName("role_id");
            entity.HasOne(userRole => userRole.User)
                .WithMany(user => user.UserRoles)
                .HasForeignKey(userRole => userRole.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(userRole => userRole.Role)
                .WithMany(role => role.UserRoles)
                .HasForeignKey(userRole => userRole.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureAssets(ModelBuilder modelBuilder)
    {
        var assetTypeConverter = new ValueConverter<AssetType, string>(
            value => value == AssetType.Truck ? "TRUCK" : "TRAILER",
            value => value == "TRUCK" ? AssetType.Truck : AssetType.Trailer);

        var assetStatusConverter = new ValueConverter<AssetStatus, string>(
            value => value == AssetStatus.Active ? "ACTIVE" : "INACTIVE",
            value => value == "ACTIVE" ? AssetStatus.Active : AssetStatus.Inactive);

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("assets");
            entity.HasKey(asset => asset.Id);
            entity.Property(asset => asset.Id).HasColumnName("id");
            entity.Property(asset => asset.AssetType)
                .HasColumnName("asset_type")
                .HasConversion(assetTypeConverter)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(asset => asset.AssetCode).HasColumnName("asset_code").HasMaxLength(50).IsRequired();
            entity.Property(asset => asset.PlateNo).HasColumnName("plate_no").HasMaxLength(30);
            entity.Property(asset => asset.Status)
                .HasColumnName("status")
                .HasConversion(assetStatusConverter)
                .HasMaxLength(20)
                .HasDefaultValue(AssetStatus.Active)
                .IsRequired();
            entity.Property(asset => asset.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(asset => asset.AssetCode).IsUnique();
        });
    }

    private static void ConfigureInventory(ModelBuilder modelBuilder)
    {
        var itemTypeConverter = new ValueConverter<ItemType, string>(
            value => value == ItemType.Consumable ? "CONSUMABLE" : "NON_CONSUMABLE",
            value => value == "CONSUMABLE" ? ItemType.Consumable : ItemType.NonConsumable);

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("inventory");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(item => item.Unit).HasColumnName("unit").HasMaxLength(20).IsRequired();
            entity.Property(item => item.ItemType)
                .HasColumnName("item_type")
                .HasConversion(itemTypeConverter)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(item => item.IsKit).HasColumnName("is_kit").HasDefaultValue(false).IsRequired();
            entity.Property(item => item.Quantity)
                .HasColumnName("quantity")
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0m)
                .IsRequired();
            entity.Property(item => item.AverageCost)
                .HasColumnName("average_cost")
                .HasColumnType("decimal(18,4)")
                .HasDefaultValue(0m)
                .IsRequired();
            entity.Property(item => item.LastCost)
                .HasColumnName("last_cost")
                .HasColumnType("decimal(18,4)");
            entity.Property(item => item.ReorderLevel).HasColumnName("reorder_level").HasColumnType("decimal(18,2)");
            entity.Property(item => item.Location).HasColumnName("location").HasMaxLength(120);
            entity.Property(item => item.UnitValue).HasColumnName("unit_value").HasColumnType("decimal(18,2)");
            entity.Property(item => item.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(item => item.Name);
            entity.HasIndex(item => item.ItemType);
        });
    }

    private static void ConfigureKitComponents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KitComponent>(entity =>
        {
            entity.ToTable("kit_components");
            entity.HasKey(component => component.Id);
            entity.Property(component => component.Id).HasColumnName("id");
            entity.Property(component => component.InventoryItemId).HasColumnName("inventory_id");
            entity.Property(component => component.Name)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(component => component.RequiredQty)
                .HasColumnName("required_qty")
                .HasColumnType("decimal(18,2)")
                .IsRequired();
            entity.Property(component => component.IsRequired)
                .HasColumnName("is_required")
                .HasDefaultValue(true)
                .IsRequired();
            entity.Property(component => component.Notes)
                .HasColumnName("notes")
                .HasMaxLength(250);
            entity.Property(component => component.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(component => component.InventoryItem)
                .WithMany(item => item.KitComponents)
                .HasForeignKey(component => component.InventoryItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(component => component.InventoryItemId);
            entity.HasIndex(component => new { component.InventoryItemId, component.Name }).IsUnique();
        });
    }

    private static void ConfigureSuppliers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.HasKey(supplier => supplier.Id);
            entity.Property(supplier => supplier.Id).HasColumnName("id");
            entity.Property(supplier => supplier.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(supplier => supplier.ContactName).HasColumnName("contact_name").HasMaxLength(120);
            entity.Property(supplier => supplier.ContactPhone).HasColumnName("contact_phone").HasMaxLength(60);
            entity.Property(supplier => supplier.ContactEmail).HasColumnName("contact_email").HasMaxLength(200);
            entity.Property(supplier => supplier.Address).HasColumnName("address").HasMaxLength(300);
            entity.Property(supplier => supplier.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
            entity.Property(supplier => supplier.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(supplier => supplier.Name).IsUnique();
        });
    }

    private static void ConfigureRequests(ModelBuilder modelBuilder)
    {
        var requestTypeConverter = new ValueConverter<RequestType, string>(
            value => value == RequestType.MaintenanceIssue
                ? "MAINTENANCE_ISSUE"
                : value == RequestType.Borrow
                    ? "BORROW"
                    : "ADJUSTMENT_DAMAGE_LOSS",
            value => value == "MAINTENANCE_ISSUE"
                ? RequestType.MaintenanceIssue
                : value == "BORROW"
                    ? RequestType.Borrow
                    : RequestType.AdjustmentDamageLoss);

        var requestStatusConverter = new ValueConverter<RequestStatus, string>(
            value => value == RequestStatus.Draft
                ? "DRAFT"
                : value == RequestStatus.Submitted
                    ? "SUBMITTED"
                    : value == RequestStatus.PendingIO
                        ? "PENDING_IO"
                        : value == RequestStatus.PendingManager
                            ? "PENDING_MANAGER"
                            : value == RequestStatus.Approved
                                ? "APPROVED"
                                : value == RequestStatus.Issued
                                    ? "ISSUED"
                                    : value == RequestStatus.Closed
                                        ? "CLOSED"
                                        : "REJECTED",
            value => value == "DRAFT"
                ? RequestStatus.Draft
                : value == "SUBMITTED"
                    ? RequestStatus.Submitted
                    : value == "PENDING_IO"
                        ? RequestStatus.PendingIO
                        : value == "PENDING_MANAGER"
                            ? RequestStatus.PendingManager
                            : value == "APPROVED"
                                ? RequestStatus.Approved
                                : value == "ISSUED"
                                    ? RequestStatus.Issued
                                    : value == "CLOSED"
                                        ? RequestStatus.Closed
                                        : RequestStatus.Rejected);

        modelBuilder.Entity<Request>(entity =>
        {
            entity.ToTable("requests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.Id).HasColumnName("id");
            entity.Property(request => request.RequestType)
                .HasColumnName("request_type")
                .HasConversion(requestTypeConverter)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(request => request.RequesterUserId).HasColumnName("requester_user_id");
            entity.Property(request => request.AssetId).HasColumnName("asset_id");
            entity.Property(request => request.Purpose).HasColumnName("purpose").HasMaxLength(400);
            entity.Property(request => request.Status)
                .HasColumnName("status")
                .HasConversion(requestStatusConverter)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(request => request.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(request => request.ApprovedAt).HasColumnName("approved_at");
            entity.Property(request => request.IssuedAt).HasColumnName("issued_at");
            entity.Property(request => request.ClosedAt).HasColumnName("closed_at");
            entity.Property(request => request.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(request => request.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(request => request.Requester)
                .WithMany()
                .HasForeignKey(request => request.RequesterUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(request => request.Asset)
                .WithMany(asset => asset.Requests)
                .HasForeignKey(request => request.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(request => new { request.RequestType, request.Status });
            entity.HasIndex(request => new { request.Status, request.CreatedAt }).IsDescending(false, true);
            entity.HasIndex(request => request.AssetId);
            entity.HasIndex(request => request.RequesterUserId);
        });
    }

    private static void ConfigureRequestLines(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RequestLine>(entity =>
        {
            entity.ToTable("request_lines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Id).HasColumnName("id");
            entity.Property(line => line.RequestId).HasColumnName("request_id");
            entity.Property(line => line.InventoryId).HasColumnName("inventory_id");
            entity.Property(line => line.QtyRequested).HasColumnName("qty_requested").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(line => line.QtyApproved).HasColumnName("qty_approved").HasColumnType("decimal(18,2)");
            entity.Property(line => line.Remarks).HasColumnName("remarks").HasMaxLength(250);
            entity.Property(line => line.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(line => line.Request)
                .WithMany(request => request.Lines)
                .HasForeignKey(line => line.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(line => line.InventoryItem)
                .WithMany(item => item.RequestLines)
                .HasForeignKey(line => line.InventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(line => line.RequestId);
            entity.HasIndex(line => line.InventoryId);
        });
    }

    private static void ConfigureInventoryAdjustments(ModelBuilder modelBuilder)
    {
        var statusConverter = new ValueConverter<InventoryAdjustmentStatus, string>(
            value => value == InventoryAdjustmentStatus.Draft
                ? "DRAFT"
                : value == InventoryAdjustmentStatus.PendingManager
                    ? "PENDING_MANAGER"
                    : value == InventoryAdjustmentStatus.Approved
                        ? "APPROVED"
                        : "REJECTED",
            value => value == "DRAFT"
                ? InventoryAdjustmentStatus.Draft
                : value == "PENDING_MANAGER"
                    ? InventoryAdjustmentStatus.PendingManager
                    : value == "APPROVED"
                        ? InventoryAdjustmentStatus.Approved
                        : InventoryAdjustmentStatus.Rejected);

        modelBuilder.Entity<InventoryAdjustment>(entity =>
        {
            entity.ToTable("inventory_adjustments");
            entity.HasKey(adjustment => adjustment.Id);
            entity.Property(adjustment => adjustment.Id).HasColumnName("id");
            entity.Property(adjustment => adjustment.Status)
                .HasColumnName("status")
                .HasConversion(statusConverter)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(adjustment => adjustment.Reason)
                .HasColumnName("reason")
                .HasMaxLength(400)
                .IsRequired();
            entity.Property(adjustment => adjustment.CreatedByUserId)
                .HasColumnName("created_by_user_id");
            entity.Property(adjustment => adjustment.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(adjustment => adjustment.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(adjustment => adjustment.ApprovedAt).HasColumnName("approved_at");
            entity.Property(adjustment => adjustment.RejectedAt).HasColumnName("rejected_at");
            entity.Property(adjustment => adjustment.RejectionReason)
                .HasColumnName("rejection_reason")
                .HasMaxLength(300);
            entity.Property(adjustment => adjustment.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(adjustment => adjustment.CreatedBy)
                .WithMany()
                .HasForeignKey(adjustment => adjustment.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(adjustment => adjustment.Status);
            entity.HasIndex(adjustment => new { adjustment.Status, adjustment.CreatedAt })
                .IsDescending(false, true);
        });
    }

    private static void ConfigureInventoryAdjustmentLines(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryAdjustmentLine>(entity =>
        {
            entity.ToTable("inventory_adjustment_lines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Id).HasColumnName("id");
            entity.Property(line => line.InventoryAdjustmentId).HasColumnName("inventory_adjustment_id");
            entity.Property(line => line.InventoryId).HasColumnName("inventory_id");
            entity.Property(line => line.QtyDelta)
                .HasColumnName("qty_delta")
                .HasColumnType("decimal(18,2)")
                .IsRequired();
            entity.Property(line => line.Remarks).HasColumnName("remarks").HasMaxLength(250);
            entity.Property(line => line.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(line => line.InventoryAdjustment)
                .WithMany(adjustment => adjustment.Lines)
                .HasForeignKey(line => line.InventoryAdjustmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(line => line.InventoryItem)
                .WithMany()
                .HasForeignKey(line => line.InventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(line => line.InventoryAdjustmentId);
            entity.HasIndex(line => line.InventoryId);
            entity.HasIndex(line => new { line.InventoryAdjustmentId, line.InventoryId }).IsUnique();
        });
    }

    private static void ConfigurePurchaseOrders(ModelBuilder modelBuilder)
    {
        var statusConverter = new ValueConverter<PurchaseOrderStatus, string>(
            value => value == PurchaseOrderStatus.Draft
                ? "DRAFT"
                : value == PurchaseOrderStatus.PendingManager
                    ? "PENDING_MANAGER"
                    : value == PurchaseOrderStatus.PendingFinance
                        ? "PENDING_FINANCE"
                        : value == PurchaseOrderStatus.PendingCeo
                            ? "PENDING_CEO"
                            : value == PurchaseOrderStatus.Approved
                                ? "APPROVED"
                                : value == PurchaseOrderStatus.Rejected
                                    ? "REJECTED"
                                    : value == PurchaseOrderStatus.PartiallyReceived
                                        ? "PARTIALLY_RECEIVED"
                                        : "CLOSED",
            value => value == "DRAFT"
                ? PurchaseOrderStatus.Draft
                : value == "PENDING_MANAGER"
                    ? PurchaseOrderStatus.PendingManager
                    : value == "PENDING_FINANCE"
                        ? PurchaseOrderStatus.PendingFinance
                        : value == "PENDING_CEO"
                            ? PurchaseOrderStatus.PendingCeo
                            : value == "APPROVED"
                                ? PurchaseOrderStatus.Approved
                                : value == "REJECTED"
                                    ? PurchaseOrderStatus.Rejected
                                    : value == "PARTIALLY_RECEIVED"
                                        ? PurchaseOrderStatus.PartiallyReceived
                                        : PurchaseOrderStatus.Closed);

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("purchase_orders");
            entity.HasKey(order => order.Id);
            entity.Property(order => order.Id).HasColumnName("id");
            entity.Property(order => order.SupplierId).HasColumnName("supplier_id").IsRequired();
            entity.Property(order => order.Notes).HasColumnName("notes").HasMaxLength(500);
            entity.Property(order => order.Status)
                .HasColumnName("status")
                .HasConversion(statusConverter)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(order => order.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(order => order.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(order => order.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(order => order.ApprovedAt).HasColumnName("approved_at");
            entity.Property(order => order.RejectedAt).HasColumnName("rejected_at");
            entity.Property(order => order.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(300);
            entity.Property(order => order.ReceivedAt).HasColumnName("received_at");
            entity.Property(order => order.ClosedAt).HasColumnName("closed_at");
            entity.Property(order => order.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(order => order.CreatedBy)
                .WithMany()
                .HasForeignKey(order => order.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(order => order.Supplier)
                .WithMany(supplier => supplier.PurchaseOrders)
                .HasForeignKey(order => order.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(order => order.Status);
            entity.HasIndex(order => new { order.Status, order.CreatedAt }).IsDescending(false, true);
            entity.HasIndex(order => order.CreatedByUserId);
            entity.HasIndex(order => order.SupplierId);
        });
    }

    private static void ConfigurePurchaseOrderLines(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.ToTable("purchase_order_lines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Id).HasColumnName("id");
            entity.Property(line => line.PurchaseOrderId).HasColumnName("purchase_order_id");
            entity.Property(line => line.InventoryId).HasColumnName("inventory_id");
            entity.Property(line => line.QtyOrdered)
                .HasColumnName("qty_ordered")
                .HasColumnType("decimal(18,2)")
                .IsRequired();
            entity.Property(line => line.QtyReceived)
                .HasColumnName("qty_received")
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0m);
            entity.Property(line => line.UnitPrice)
                .HasColumnName("unit_price")
                .HasColumnType("decimal(18,2)");
            entity.Property(line => line.Remarks).HasColumnName("remarks").HasMaxLength(250);

            entity.HasOne(line => line.PurchaseOrder)
                .WithMany(order => order.Lines)
                .HasForeignKey(line => line.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(line => line.InventoryItem)
                .WithMany()
                .HasForeignKey(line => line.InventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(line => line.PurchaseOrderId);
            entity.HasIndex(line => line.InventoryId);
            entity.HasIndex(line => new { line.PurchaseOrderId, line.InventoryId }).IsUnique();
        });
    }

    private static void ConfigurePurchaseOrderReceipts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseOrderReceipt>(entity =>
        {
            entity.ToTable("purchase_order_receipts");
            entity.HasKey(receipt => receipt.Id);
            entity.Property(receipt => receipt.Id).HasColumnName("id");
            entity.Property(receipt => receipt.PurchaseOrderLineId).HasColumnName("purchase_order_line_id");
            entity.Property(receipt => receipt.QtyReceivedIncrement)
                .HasColumnName("qty_received_increment")
                .HasColumnType("decimal(18,2)")
                .IsRequired();
            entity.Property(receipt => receipt.ReceivedByUserId).HasColumnName("received_by_user_id");
            entity.Property(receipt => receipt.ReceivedAt)
                .HasColumnName("received_at")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(receipt => receipt.ReceivedBy)
                .WithMany()
                .HasForeignKey(receipt => receipt.ReceivedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(receipt => receipt.PurchaseOrderLine)
                .WithMany(line => line.Receipts)
                .HasForeignKey(receipt => receipt.PurchaseOrderLineId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(receipt => receipt.PurchaseOrderLineId);
            entity.HasIndex(receipt => receipt.ReceivedByUserId);
        });
    }

    private static void ConfigureLoans(ModelBuilder modelBuilder)
    {
        var loanStatusConverter = new ValueConverter<LoanStatus, string>(
            value => value == LoanStatus.Open
                ? "OPEN"
                : value == LoanStatus.PartiallyReturned
                    ? "PARTIALLY_RETURNED"
                    : "CLOSED",
            value => value == "OPEN"
                ? LoanStatus.Open
                : value == "PARTIALLY_RETURNED"
                    ? LoanStatus.PartiallyReturned
                    : LoanStatus.Closed);

        modelBuilder.Entity<Loan>(entity =>
        {
            entity.ToTable("loans");
            entity.HasKey(loan => loan.Id);
            entity.Property(loan => loan.Id).HasColumnName("id");
            entity.Property(loan => loan.RequestId).HasColumnName("request_id");
            entity.Property(loan => loan.BorrowerUserId).HasColumnName("borrower_user_id");
            entity.Property(loan => loan.AssetId).HasColumnName("asset_id");
            entity.Property(loan => loan.Status)
                .HasColumnName("status")
                .HasConversion(loanStatusConverter)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(loan => loan.IssuedAt).HasColumnName("issued_at");
            entity.Property(loan => loan.DueAt).HasColumnName("due_at");
            entity.Property(loan => loan.ClosedAt).HasColumnName("closed_at");

            entity.HasOne(loan => loan.Request)
                .WithOne(request => request.Loan)
                .HasForeignKey<Loan>(loan => loan.RequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(loan => loan.Borrower)
                .WithMany()
                .HasForeignKey(loan => loan.BorrowerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(loan => loan.Asset)
                .WithMany(asset => asset.Loans)
                .HasForeignKey(loan => loan.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(loan => loan.Status);
            entity.HasIndex(loan => new { loan.Status, loan.IssuedAt }).IsDescending(false, true);
            entity.HasIndex(loan => loan.BorrowerUserId);
            entity.HasIndex(loan => loan.RequestId).IsUnique();
        });
    }

    private static void ConfigureLoanLines(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LoanLine>(entity =>
        {
            entity.ToTable("loan_lines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Id).HasColumnName("id");
            entity.Property(line => line.LoanId).HasColumnName("loan_id");
            entity.Property(line => line.InventoryId).HasColumnName("inventory_id");
            entity.Property(line => line.QtyIssued)
                .HasColumnName("qty_issued")
                .HasColumnType("decimal(18,2)")
                .IsRequired();
            entity.Property(line => line.QtyReturned)
                .HasColumnName("qty_returned")
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0m);

            entity.HasOne(line => line.Loan)
                .WithMany(loan => loan.Lines)
                .HasForeignKey(line => line.LoanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(line => line.InventoryItem)
                .WithMany()
                .HasForeignKey(line => line.InventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(line => line.LoanId);
            entity.HasIndex(line => line.InventoryId);
        });
    }

    private static void ConfigureLoanLineReturns(ModelBuilder modelBuilder)
    {
        var returnConditionConverter = new ValueConverter<ReturnCondition, string>(
            value => value == ReturnCondition.Good
                ? "GOOD"
                : value == ReturnCondition.Damaged
                    ? "DAMAGED"
                    : "LOST",
            value => value == "GOOD"
                ? ReturnCondition.Good
                : value == "DAMAGED"
                    ? ReturnCondition.Damaged
                    : ReturnCondition.Lost);

        modelBuilder.Entity<LoanLineReturn>(entity =>
        {
            entity.ToTable("loan_line_returns");
            entity.HasKey(ret => ret.Id);
            entity.Property(ret => ret.Id).HasColumnName("id");
            entity.Property(ret => ret.LoanLineId).HasColumnName("loan_line_id");
            entity.Property(ret => ret.QtyReturned)
                .HasColumnName("qty_returned")
                .HasColumnType("decimal(18,2)")
                .IsRequired();
            entity.Property(ret => ret.Condition)
                .HasColumnName("return_condition")
                .HasConversion(returnConditionConverter)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(ret => ret.MissingComponentsJson).HasColumnName("missing_components_json");
            entity.Property(ret => ret.ReceivedByUserId).HasColumnName("received_by_user_id");
            entity.Property(ret => ret.ReturnedAt)
                .HasColumnName("returned_at")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(ret => ret.LoanLine)
                .WithMany(line => line.Returns)
                .HasForeignKey(ret => ret.LoanLineId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ret => ret.ReceivedBy)
                .WithMany()
                .HasForeignKey(ret => ret.ReceivedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(ret => ret.LoanLineId);
        });
    }

    private static void ConfigureWorkflows(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workflow>(entity =>
        {
            entity.ToTable("workflows");
            entity.HasKey(workflow => workflow.WorkflowKey);
            entity.Property(workflow => workflow.WorkflowKey).HasColumnName("workflow_key").HasMaxLength(50).IsRequired();
            entity.Property(workflow => workflow.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            entity.Property(workflow => workflow.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        });
    }

    private static void ConfigureWorkflowSteps(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowStep>(entity =>
        {
            entity.ToTable("workflow_steps");
            entity.HasKey(step => step.Id);
            entity.Property(step => step.Id).HasColumnName("id");
            entity.Property(step => step.WorkflowKey).HasColumnName("workflow_key").HasMaxLength(50).IsRequired();
            entity.Property(step => step.StepOrder).HasColumnName("step_order");
            entity.Property(step => step.RequiredRole).HasColumnName("required_role").HasMaxLength(80).IsRequired();
            entity.Property(step => step.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(step => step.Workflow)
                .WithMany(workflow => workflow.Steps)
                .HasForeignKey(step => step.WorkflowKey)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(step => new { step.WorkflowKey, step.StepOrder }).IsUnique();
        });
    }

    private static void ConfigureApprovals(ModelBuilder modelBuilder)
    {
        var approvalStatusConverter = new ValueConverter<ApprovalStatus, string>(
            value => value == ApprovalStatus.Pending
                ? "PENDING"
                : value == ApprovalStatus.Approved
                    ? "APPROVED"
                    : "REJECTED",
            value => value == "PENDING"
                ? ApprovalStatus.Pending
                : value == "APPROVED"
                    ? ApprovalStatus.Approved
                    : ApprovalStatus.Rejected);

        modelBuilder.Entity<Approval>(entity =>
        {
            entity.ToTable("approvals");
            entity.HasKey(approval => approval.Id);
            entity.Property(approval => approval.Id).HasColumnName("id");
            entity.Property(approval => approval.WorkflowKey).HasColumnName("workflow_key").HasMaxLength(50).IsRequired();
            entity.Property(approval => approval.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();
            entity.Property(approval => approval.EntityId).HasColumnName("entity_id");
            entity.Property(approval => approval.Status)
                .HasColumnName("status")
                .HasConversion(approvalStatusConverter)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(approval => approval.CurrentStep).HasColumnName("current_step").HasDefaultValue(1).IsRequired();
            entity.Property(approval => approval.CreatedBy).HasColumnName("created_by");
            entity.Property(approval => approval.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(approval => approval.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(approval => approval.Workflow)
                .WithMany()
                .HasForeignKey(approval => approval.WorkflowKey)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(approval => approval.Creator)
                .WithMany()
                .HasForeignKey(approval => approval.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(approval => new { approval.EntityType, approval.EntityId });
            entity.HasIndex(approval => approval.Status);
        });
    }

    private static void ConfigureApprovalActions(ModelBuilder modelBuilder)
    {
        var decisionConverter = new ValueConverter<ApprovalDecision, string>(
            value => value == ApprovalDecision.Approve ? "APPROVE" : "REJECT",
            value => value == "APPROVE" ? ApprovalDecision.Approve : ApprovalDecision.Reject);

        modelBuilder.Entity<ApprovalAction>(entity =>
        {
            entity.ToTable("approval_actions");
            entity.HasKey(action => action.Id);
            entity.Property(action => action.Id).HasColumnName("id");
            entity.Property(action => action.ApprovalId).HasColumnName("approval_id");
            entity.Property(action => action.StepOrder).HasColumnName("step_order");
            entity.Property(action => action.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(action => action.Decision)
                .HasColumnName("decision")
                .HasConversion(decisionConverter)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(action => action.Remarks).HasColumnName("remarks").HasMaxLength(250);
            entity.Property(action => action.ActedAt).HasColumnName("acted_at").HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(action => action.Approval)
                .WithMany(approval => approval.Actions)
                .HasForeignKey(action => action.ApprovalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(action => action.Actor)
                .WithMany()
                .HasForeignKey(action => action.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(action => action.ApprovalId);
        });
    }

    private static void ConfigureStockLogs(ModelBuilder modelBuilder)
    {
        var movementConverter = new ValueConverter<StockMovementType, string>(
            value => value == StockMovementType.In
                ? "IN"
                : value == StockMovementType.Out
                    ? "OUT"
                    : value == StockMovementType.Borrow
                        ? "BORROW"
                        : value == StockMovementType.Return
                            ? "RETURN"
                            : value == StockMovementType.WriteOff
                                ? "WRITE_OFF"
                                : "ADJUSTMENT",
            value => value == "IN"
                ? StockMovementType.In
                : value == "OUT"
                    ? StockMovementType.Out
                    : value == "BORROW"
                        ? StockMovementType.Borrow
                        : value == "RETURN"
                            ? StockMovementType.Return
                            : value == "WRITE_OFF"
                                ? StockMovementType.WriteOff
                                : StockMovementType.Adjustment);

        modelBuilder.Entity<StockLog>(entity =>
        {
            entity.ToTable("stock_logs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Id).HasColumnName("id");
            entity.Property(log => log.MovementType)
                .HasColumnName("movement_type")
                .HasConversion(movementConverter)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(log => log.InventoryId).HasColumnName("inventory_id");
            entity.Property(log => log.QtyDelta).HasColumnName("qty_delta").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(log => log.UnitCostSnapshot)
                .HasColumnName("unit_cost_snapshot")
                .HasColumnType("decimal(18,4)");
            entity.Property(log => log.TotalCostSnapshot)
                .HasColumnName("total_cost_snapshot")
                .HasColumnType("decimal(18,4)");
            entity.Property(log => log.RefType).HasColumnName("ref_type").HasMaxLength(50).IsRequired();
            entity.Property(log => log.RefId).HasColumnName("ref_id");
            entity.Property(log => log.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(log => log.MetaJson).HasColumnName("meta_json");
            entity.Property(log => log.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(log => log.InventoryItem)
                .WithMany(item => item.StockLogs)
                .HasForeignKey(log => log.InventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(log => log.Actor)
                .WithMany()
                .HasForeignKey(log => log.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(log => new { log.InventoryId, log.CreatedAt }).IsDescending(false, true);
            entity.HasIndex(log => new { log.RefType, log.RefId });
            entity.HasIndex(log => new { log.RefType, log.RefId, log.InventoryId, log.MovementType })
                .IsUnique()
                .HasFilter("[ref_type] = 'request' AND [movement_type] IN ('OUT', 'BORROW')");
        });
    }

    private static void ConfigureAuditLogs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Id).HasColumnName("id");
            entity.Property(log => log.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(log => log.Action).HasColumnName("action").HasMaxLength(120).IsRequired();
            entity.Property(log => log.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();
            entity.Property(log => log.EntityId).HasColumnName("entity_id");
            entity.Property(log => log.BeforeJson).HasColumnName("before_json");
            entity.Property(log => log.AfterJson).HasColumnName("after_json");
            entity.Property(log => log.TraceId).HasColumnName("trace_id").HasMaxLength(64);
            entity.Property(log => log.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(log => log.Actor)
                .WithMany()
                .HasForeignKey(log => log.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(log => log.ActorUserId);
            entity.HasIndex(log => new { log.EntityType, log.EntityId, log.CreatedAt })
                .IsDescending(false, false, true);
        });
    }

    private static void ConfigureAuthEvents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthEvent>(entity =>
        {
            entity.ToTable("auth_events");
            entity.HasKey(authEvent => authEvent.Id);
            entity.Property(authEvent => authEvent.Id).HasColumnName("id");
            entity.Property(authEvent => authEvent.EventType).HasColumnName("event_type").HasMaxLength(40).IsRequired();
            entity.Property(authEvent => authEvent.Outcome).HasColumnName("outcome").HasMaxLength(20).IsRequired();
            entity.Property(authEvent => authEvent.ReasonCode).HasColumnName("reason_code").HasMaxLength(60);
            entity.Property(authEvent => authEvent.Username).HasColumnName("username").HasMaxLength(100);
            entity.Property(authEvent => authEvent.UserId).HasColumnName("user_id");
            entity.Property(authEvent => authEvent.RolesSnapshotJson).HasColumnName("roles_snapshot_json");
            entity.Property(authEvent => authEvent.AuthMethod).HasColumnName("auth_method").HasMaxLength(40).IsRequired();
            entity.Property(authEvent => authEvent.MfaPerformed).HasColumnName("mfa_performed").HasDefaultValue(false).IsRequired();
            entity.Property(authEvent => authEvent.MfaMethod).HasColumnName("mfa_method").HasMaxLength(40);
            entity.Property(authEvent => authEvent.SessionId).HasColumnName("session_id").HasMaxLength(120);
            entity.Property(authEvent => authEvent.TokenJti).HasColumnName("token_jti").HasMaxLength(120);
            entity.Property(authEvent => authEvent.CorrelationId).HasColumnName("correlation_id").HasMaxLength(120);
            entity.Property(authEvent => authEvent.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
            entity.Property(authEvent => authEvent.UserAgent).HasColumnName("user_agent").HasMaxLength(400);
            entity.Property(authEvent => authEvent.ClientApp).HasColumnName("client_app").HasMaxLength(120);
            entity.Property(authEvent => authEvent.Environment).HasColumnName("environment").HasMaxLength(40);
            entity.Property(authEvent => authEvent.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(authEvent => authEvent.User)
                .WithMany()
                .HasForeignKey(authEvent => authEvent.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(authEvent => authEvent.CreatedAt);
            entity.HasIndex(authEvent => authEvent.Username);
            entity.HasIndex(authEvent => authEvent.UserId);
            entity.HasIndex(authEvent => authEvent.Outcome);
            entity.HasIndex(authEvent => authEvent.EventType);
        });
    }

    private static void ConfigureModuleSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ModuleSetting>(entity =>
        {
            entity.ToTable("module_settings");
            entity.HasKey(setting => setting.ModuleKey);
            entity.Property(setting => setting.ModuleKey)
                .HasColumnName("module_key")
                .HasMaxLength(80)
                .IsRequired();
            entity.Property(setting => setting.IsEnabled)
                .HasColumnName("is_enabled")
                .HasDefaultValue(true)
                .IsRequired();
            entity.Property(setting => setting.UpdatedByUserId).HasColumnName("updated_by_user_id");
            entity.Property(setting => setting.Notes).HasColumnName("notes").HasMaxLength(400);
            entity.Property(setting => setting.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(setting => setting.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(setting => setting.UpdatedBy)
                .WithMany()
                .HasForeignKey(setting => setting.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDispatching(ModelBuilder modelBuilder)
    {
        var statusConverter = new ValueConverter<TripStatus, string>(
            value =>
                value == TripStatus.Draft ? "DRAFT" :
                value == TripStatus.Dispatched ? "DISPATCHED" :
                value == TripStatus.EnroutePickup ? "ENROUTE_PICKUP" :
                value == TripStatus.AtPickup ? "AT_PICKUP" :
                value == TripStatus.Loaded ? "LOADED" :
                value == TripStatus.EnrouteDropoff ? "ENROUTE_DROPOFF" :
                value == TripStatus.AtDropoff ? "AT_DROPOFF" :
                value == TripStatus.Delivered ? "DELIVERED" :
                value == TripStatus.Closed ? "CLOSED" :
                value == TripStatus.Cancelled ? "CANCELLED" :
                value == TripStatus.OnHold ? "ON_HOLD" :
                value == TripStatus.FailedAttempt ? "FAILED_ATTEMPT" :
                "DRAFT",
            value =>
                value == "DRAFT" ? TripStatus.Draft :
                value == "DISPATCHED" ? TripStatus.Dispatched :
                value == "ENROUTE_PICKUP" ? TripStatus.EnroutePickup :
                value == "AT_PICKUP" ? TripStatus.AtPickup :
                value == "LOADED" ? TripStatus.Loaded :
                value == "ENROUTE_DROPOFF" ? TripStatus.EnrouteDropoff :
                value == "AT_DROPOFF" ? TripStatus.AtDropoff :
                value == "DELIVERED" ? TripStatus.Delivered :
                value == "CLOSED" ? TripStatus.Closed :
                value == "CANCELLED" ? TripStatus.Cancelled :
                value == "ON_HOLD" ? TripStatus.OnHold :
                value == "FAILED_ATTEMPT" ? TripStatus.FailedAttempt :
                TripStatus.Draft);

        var historyEventConverter = new ValueConverter<TripHistoryEventType, string>(
            value => value == TripHistoryEventType.ScheduleUpdated ? "SCHEDULE_UPDATED" : "STATUS_CHANGE",
            value => value == "SCHEDULE_UPDATED"
                ? TripHistoryEventType.ScheduleUpdated
                : TripHistoryEventType.StatusChange);

        var stopTypeConverter = new ValueConverter<TripStopType, string>(
            value => value == TripStopType.Pickup ? "PICKUP" : "DROPOFF",
            value => value == "PICKUP" ? TripStopType.Pickup : TripStopType.Dropoff);

        var docTypeConverter = new ValueConverter<TripDocumentType, string>(
            value =>
                value == TripDocumentType.Waybill ? "WAYBILL" :
                value == TripDocumentType.Pod ? "POD" :
                value == TripDocumentType.Atw ? "ATW" :
                "WAYBILL",
            value =>
                value == "WAYBILL" ? TripDocumentType.Waybill :
                value == "POD" ? TripDocumentType.Pod :
                value == "ATW" ? TripDocumentType.Atw :
                TripDocumentType.Waybill);

        var docStateConverter = new ValueConverter<TripDocumentState, string>(
            value =>
                value == TripDocumentState.Missing ? "MISSING" :
                value == TripDocumentState.Uploaded ? "UPLOADED" :
                value == TripDocumentState.Verified ? "VERIFIED" :
                value == TripDocumentState.Rejected ? "REJECTED" :
                "MISSING",
            value =>
                value == "MISSING" ? TripDocumentState.Missing :
                value == "UPLOADED" ? TripDocumentState.Uploaded :
                value == "VERIFIED" ? TripDocumentState.Verified :
                value == "REJECTED" ? TripDocumentState.Rejected :
                TripDocumentState.Missing);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("dispatch_customers");
            entity.HasKey(customer => customer.Id);
            entity.Property(customer => customer.Id).HasColumnName("id");
            entity.Property(customer => customer.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(customer => customer.Address).HasColumnName("address").HasMaxLength(300);
            entity.Property(customer => customer.Contact).HasColumnName("contact").HasMaxLength(200);
            entity.Property(customer => customer.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(customer => customer.Name);
        });

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.ToTable("dispatch_trips");
            entity.HasKey(trip => trip.Id);
            entity.Property(trip => trip.Id).HasColumnName("id");
            entity.Property(trip => trip.CustomerId).HasColumnName("customer_id");
            entity.Property(trip => trip.DriverUserId).HasColumnName("driver_user_id");
            entity.Property(trip => trip.TruckAssetId).HasColumnName("truck_asset_id");
            entity.Property(trip => trip.Status)
                .HasColumnName("status")
                .HasConversion(statusConverter)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(trip => trip.PodPending).HasColumnName("pod_pending").HasDefaultValue(false).IsRequired();
            entity.Property(trip => trip.HoldPreviousStatus)
                .HasColumnName("hold_previous_status")
                .HasConversion(statusConverter)
                .HasMaxLength(30);
            entity.Property(trip => trip.Notes).HasColumnName("notes").HasMaxLength(400);
            entity.Property(trip => trip.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(trip => trip.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(trip => trip.Customer)
                .WithMany(customer => customer.Trips)
                .HasForeignKey(trip => trip.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(trip => trip.Driver)
                .WithMany()
                .HasForeignKey(trip => trip.DriverUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(trip => trip.TruckAsset)
                .WithMany()
                .HasForeignKey(trip => trip.TruckAssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(trip => trip.Status);
            entity.HasIndex(trip => trip.DriverUserId);
            entity.HasIndex(trip => trip.CustomerId);
        });

        modelBuilder.Entity<TripStop>(entity =>
        {
            entity.ToTable("dispatch_trip_stops");
            entity.HasKey(stop => stop.Id);
            entity.Property(stop => stop.Id).HasColumnName("id");
            entity.Property(stop => stop.TripId).HasColumnName("trip_id");
            entity.Property(stop => stop.StopType)
                .HasColumnName("stop_type")
                .HasConversion(stopTypeConverter)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(stop => stop.LocationText).HasColumnName("location_text").HasMaxLength(300).IsRequired();
            entity.Property(stop => stop.ScheduledAt).HasColumnName("scheduled_at");
            entity.Property(stop => stop.ActualAt).HasColumnName("actual_at");
            entity.Property(stop => stop.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(stop => stop.Trip)
                .WithMany(trip => trip.Stops)
                .HasForeignKey(stop => stop.TripId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(stop => stop.TripId);
        });

        modelBuilder.Entity<TripStatusHistory>(entity =>
        {
            entity.ToTable("dispatch_trip_status_history");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Id).HasColumnName("id");
            entity.Property(history => history.TripId).HasColumnName("trip_id");
            entity.Property(history => history.EventType)
                .HasColumnName("event_type")
                .HasConversion(historyEventConverter)
                .HasMaxLength(30)
                .HasDefaultValue(TripHistoryEventType.StatusChange)
                .IsRequired();
            entity.Property(history => history.FromStatus)
                .HasColumnName("from_status")
                .HasConversion(statusConverter)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(history => history.ToStatus)
                .HasColumnName("to_status")
                .HasConversion(statusConverter)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(history => history.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(history => history.Remarks).HasColumnName("remarks").HasMaxLength(400);
            entity.Property(history => history.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(history => history.Trip)
                .WithMany(trip => trip.StatusHistory)
                .HasForeignKey(history => history.TripId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(history => history.Actor)
                .WithMany()
                .HasForeignKey(history => history.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(history => history.TripId);
        });

        modelBuilder.Entity<TripDocument>(entity =>
        {
            entity.ToTable("dispatch_trip_documents");
            entity.HasKey(doc => doc.Id);
            entity.Property(doc => doc.Id).HasColumnName("id");
            entity.Property(doc => doc.TripId).HasColumnName("trip_id");
            entity.Property(doc => doc.Type)
                .HasColumnName("doc_type")
                .HasConversion(docTypeConverter)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(doc => doc.State)
                .HasColumnName("state")
                .HasConversion(docStateConverter)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(doc => doc.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();
            entity.Property(doc => doc.UploadedByUserId).HasColumnName("uploaded_by_user_id");
            entity.Property(doc => doc.VerifiedByUserId).HasColumnName("verified_by_user_id");
            entity.Property(doc => doc.RejectedByUserId).HasColumnName("rejected_by_user_id");
            entity.Property(doc => doc.Remarks).HasColumnName("remarks").HasMaxLength(500);
            entity.Property(doc => doc.UploadedAt).HasColumnName("uploaded_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(doc => doc.VerifiedAt).HasColumnName("verified_at");
            entity.Property(doc => doc.RejectedAt).HasColumnName("rejected_at");

            entity.HasOne(doc => doc.Trip)
                .WithMany(trip => trip.Documents)
                .HasForeignKey(doc => doc.TripId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(doc => doc.UploadedBy)
                .WithMany()
                .HasForeignKey(doc => doc.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(doc => doc.VerifiedBy)
                .WithMany()
                .HasForeignKey(doc => doc.VerifiedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(doc => doc.RejectedBy)
                .WithMany()
                .HasForeignKey(doc => doc.RejectedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(doc => doc.TripId);
            entity.HasIndex(doc => new { doc.TripId, doc.Type }).IsUnique();
        });
    }
}
