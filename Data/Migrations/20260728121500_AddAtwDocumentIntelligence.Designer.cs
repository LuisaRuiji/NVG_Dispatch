using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NVGInventory.Data;

#nullable disable

namespace NVGInventory.Data.Migrations;

[DbContext(typeof(InventoryDbContext))]
[Migration("20260728121500_AddAtwDocumentIntelligence")]
public partial class AddAtwDocumentIntelligence
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
    }
}
