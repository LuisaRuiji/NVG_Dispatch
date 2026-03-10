type ApprovalDto = {
  id: string;
  workflow: string;
  step: string;
  status: "PENDING" | "APPROVED" | "REJECTED";
  requestedBy: string;
  updatedAt: string;
};

const sampleApprovals: ApprovalDto[] = [
  {
    id: "ap-112",
    workflow: "MAINTENANCE_ISSUE_APPROVAL",
    step: "Manager",
    status: "PENDING",
    requestedBy: "Jordan Lee",
    updatedAt: "2026-02-17T07:55:00Z"
  },
  {
    id: "ap-113",
    workflow: "BORROW_APPROVAL",
    step: "Inventory Officer",
    status: "PENDING",
    requestedBy: "Talia Grant",
    updatedAt: "2026-02-17T08:15:00Z"
  },
  {
    id: "ap-114",
    workflow: "ADJUSTMENT_APPROVAL",
    step: "Manager",
    status: "APPROVED",
    requestedBy: "R. Singh",
    updatedAt: "2026-02-16T18:10:00Z"
  }
];

export default function ApprovalsOverview() {
  return (
    <section id="approvals" className="panel">
      <div className="panel-header">
        <div>
          <p className="panel-eyebrow">Approvals</p>
          <h2>Pipeline</h2>
        </div>
        <button className="button ghost">Open Approvals</button>
      </div>

      <div className="panel-grid compact">
        {sampleApprovals.map((approval) => (
          <article key={approval.id} className="card compact">
            <div className="card-top">
              <div>
                <p className="card-label">{approval.workflow.replace(/_/g, " ")}</p>
                <h3>{approval.step}</h3>
              </div>
              <span className={`pill ${approval.status === "APPROVED" ? "pill-teal" : "pill-amber"}`}>
                {approval.status}
              </span>
            </div>
            <p className="card-meta">Requested by {approval.requestedBy}</p>
            <p className="card-meta">Updated {new Date(approval.updatedAt).toLocaleString()}</p>
          </article>
        ))}
      </div>
    </section>
  );
}
