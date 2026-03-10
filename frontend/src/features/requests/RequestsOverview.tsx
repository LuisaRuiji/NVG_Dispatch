type RequestDto = {
  id: string;
  type: "MAINTENANCE_ISSUE" | "BORROW" | "ADJUSTMENT_DAMAGE_LOSS";
  status: "DRAFT" | "SUBMITTED" | "PENDING_IO" | "PENDING_MANAGER" | "APPROVED" | "ISSUED" | "CLOSED" | "REJECTED";
  createdAt: string;
  requestor: string;
  lines: number;
};

const sampleRequests: RequestDto[] = [
  {
    id: "req-451",
    type: "MAINTENANCE_ISSUE",
    status: "PENDING_MANAGER",
    createdAt: "2026-02-16T09:20:00Z",
    requestor: "Jordan Lee",
    lines: 4
  },
  {
    id: "req-452",
    type: "BORROW",
    status: "PENDING_IO",
    createdAt: "2026-02-17T07:10:00Z",
    requestor: "Talia Grant",
    lines: 2
  },
  {
    id: "req-453",
    type: "ADJUSTMENT_DAMAGE_LOSS",
    status: "APPROVED",
    createdAt: "2026-02-15T12:40:00Z",
    requestor: "R. Singh",
    lines: 1
  }
];

export default function RequestsOverview() {
  return (
    <section id="requests" className="panel">
      <div className="panel-header">
        <div>
          <p className="panel-eyebrow">Requests</p>
          <h2>Queue</h2>
        </div>
        <button className="button">Create Request</button>
      </div>

      <div className="list">
        {sampleRequests.map((request) => (
          <div key={request.id} className="list-row">
            <div>
              <p className="list-title">{request.id}</p>
              <p className="list-meta">
                {request.type.replace(/_/g, " ")} � {request.lines} lines � {request.requestor}
              </p>
            </div>
            <span className="pill pill-slate">{request.status.replace(/_/g, " ")}</span>
          </div>
        ))}
      </div>
    </section>
  );
}
