using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NVGInventory.Data;

#nullable disable

namespace NVGInventory.Data.Migrations;

[DbContext(typeof(InventoryDbContext))]
[Migration("20260703180334_AddShipmentRequestCoordinates")]
public partial class AddShipmentRequestCoordinates
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
    }
}
