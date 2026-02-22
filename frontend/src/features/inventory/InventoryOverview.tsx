import type { InventoryItemDto } from "../../lib/api/types";

const sampleItems: InventoryItemDto[] = [
  {
    id: "inv-001",
    sku: "C-1001",
    name: "Hydraulic Hose",
    itemType: "CONSUMABLE",
    quantity: 124,
    location: "Main Depot",
    reorderPoint: 40
  },
  {
    id: "inv-002",
    sku: "NC-402",
    name: "Tow Strap Kit",
    itemType: "NON_CONSUMABLE",
    quantity: 18,
    location: "Yard A",
    reorderPoint: 8
  },
  {
    id: "inv-003",
    sku: "C-210",
    name: "Brake Fluid 5L",
    itemType: "CONSUMABLE",
    quantity: 62,
    location: "Main Depot",
    reorderPoint: 20
  }
];

export default function InventoryOverview() {
  return (
    <section id="inventory" className="panel">
      <div className="panel-header">
        <div>
          <p className="panel-eyebrow">Inventory</p>
          <h2>Snapshot</h2>
        </div>
        <button className="button ghost">View Catalogue</button>
      </div>

      <div className="panel-grid">
        {sampleItems.map((item) => (
          <article key={item.id} className="card">
            <div className="card-top">
              <div>
                <p className="card-label">{item.sku}</p>
                <h3>{item.name}</h3>
              </div>
              <span className={`pill ${item.itemType === "CONSUMABLE" ? "pill-amber" : "pill-teal"}`}>
                {item.itemType.replace("_", " ")}
              </span>
            </div>
            <div className="card-metrics">
              <div>
                <p className="metric-label">On hand</p>
                <p className="metric-value">{item.quantity}</p>
              </div>
              <div>
                <p className="metric-label">Reorder</p>
                <p className="metric-value">{item.reorderPoint}</p>
              </div>
              <div>
                <p className="metric-label">Location</p>
                <p className="metric-value">{item.location}</p>
              </div>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
