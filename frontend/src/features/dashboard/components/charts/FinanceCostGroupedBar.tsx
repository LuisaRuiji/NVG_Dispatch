import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChartCard, chartColors } from "./chartPrimitives";
import type { FinancialBreakdownChartItem } from "./types";

type Props = {
  data: FinancialBreakdownChartItem[];
  loading: boolean;
};

export default function FinanceCostGroupedBar({ data, loading }: Props) {
  return (
    <ChartCard title="Revenue vs Cost" loading={loading} empty={data.length === 0} emptyDescription="No financial breakdown available.">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data}>
          <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
          <XAxis dataKey="period" />
          <YAxis tickFormatter={(value) => `${Number(value) / 1000}k`} />
          <Tooltip formatter={(value) => Number(value).toLocaleString()} />
          <Legend />
          <Bar dataKey="revenue" name="Revenue" fill={chartColors.primary} radius={[6, 6, 0, 0]} />
          <Bar dataKey="payroll" name="Payroll" fill={chartColors.accent} radius={[6, 6, 0, 0]} />
          <Bar dataKey="fuel" name="Fuel" fill={chartColors.muted} radius={[6, 6, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
