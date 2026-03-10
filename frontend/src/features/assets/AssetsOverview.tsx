type AssetDto = {
  id: string;
  assetTag: string;
  type: "TRUCK" | "TRAILER";
  status: "ACTIVE" | "INACTIVE";
  location: string;
};

const sampleAssets: AssetDto[] = [
  {
    id: "asset-11",
    assetTag: "TRK-044",
    type: "TRUCK",
    status: "ACTIVE",
    location: "Depot North"
  },
  {
    id: "asset-12",
    assetTag: "TRL-019",
    type: "TRAILER",
    status: "INACTIVE",
    location: "Workshop"
  }
];

export default function AssetsOverview() {
  return (
    <section id="assets" className="panel">
      <div className="panel-header">
        <div>
          <p className="panel-eyebrow">Assets</p>
          <h2>Fleet Status</h2>
        </div>
        <button className="button ghost">Open Asset Register</button>
      </div>

      <div className="panel-grid compact">
        {sampleAssets.map((asset) => (
          <article key={asset.id} className="card compact">
            <div className="card-top">
              <div>
                <p className="card-label">{asset.assetTag}</p>
                <h3>{asset.type.toUpperCase()}</h3>
              </div>
              <span className={`pill ${asset.status === "ACTIVE" ? "pill-teal" : "pill-amber"}`}>
                {asset.status}
              </span>
            </div>
            <p className="card-meta">{asset.location}</p>
          </article>
        ))}
      </div>
    </section>
  );
}
