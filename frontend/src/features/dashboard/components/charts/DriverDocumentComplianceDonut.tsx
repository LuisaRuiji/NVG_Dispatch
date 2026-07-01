import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import { ChartCard, chartColors } from "./chartPrimitives";

type Props = {
  percent?: number | null;
  loading: boolean;
};

export default function DriverDocumentComplianceDonut({ percent, loading }: Props) {
  const value = percent ?? 0;
  const data = [
    { label: "Submitted", value },
    { label: "Missing", value: Math.max(100 - value, 0) }
  ];

  return (
    <ChartCard title="Document Compliance" loading={loading} empty={percent == null} emptyDescription="No document compliance data.">
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <Pie data={data} dataKey="value" nameKey="label" innerRadius={54} outerRadius={86}>
            <Cell fill={chartColors.primary} />
            <Cell fill={chartColors.accent} />
          </Pie>
          <Tooltip formatter={(v) => `${Number(v).toFixed(1)}%`} />
          <Legend />
        </PieChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
