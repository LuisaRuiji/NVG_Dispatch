type LoanDto = {
  id: string;
  status: "OPEN" | "PARTIALLY_RETURNED" | "CLOSED";
  borrower: string;
  items: number;
  dueDate: string;
};

const sampleLoans: LoanDto[] = [
  {
    id: "loan-204",
    status: "OPEN",
    borrower: "Field Ops Alpha",
    items: 3,
    dueDate: "2026-02-20"
  },
  {
    id: "loan-205",
    status: "PARTIALLY_RETURNED",
    borrower: "Workshop Team",
    items: 5,
    dueDate: "2026-02-19"
  }
];

export default function LoansOverview() {
  return (
    <section id="loans" className="panel">
      <div className="panel-header">
        <div>
          <p className="panel-eyebrow">Loans</p>
          <h2>Open Returns</h2>
        </div>
        <button className="button">Record Return</button>
      </div>

      <div className="list">
        {sampleLoans.map((loan) => (
          <div key={loan.id} className="list-row">
            <div>
              <p className="list-title">{loan.id}</p>
              <p className="list-meta">
                {loan.borrower} � {loan.items} items � Due {loan.dueDate}
              </p>
            </div>
            <span className={`pill ${loan.status === "OPEN" ? "pill-amber" : "pill-slate"}`}>
              {loan.status.replace(/_/g, " ")}
            </span>
          </div>
        ))}
      </div>
    </section>
  );
}
