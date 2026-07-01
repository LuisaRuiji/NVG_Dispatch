import { Area, AreaChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChartCard, chartColors } from "./chartPrimitives";
import type { FinancialRevenueChartItem } from "./types";

type Props = {
  data: FinancialRevenueChartItem[];
  loading: boolean;
};

export default function FinanceRevenueArea({ data, loading }: Props) {
  return (
    <ChartCard title="Weekly Revenue" loading={loading} empty={data.length === 0} emptyDescription="No revenue trend available.">
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={data}>
          <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
          <XAxis dataKey="period" />
          <YAxis tickFormatter={(value) => `${Number(value) / 1000}k`} />
          <Tooltip formatter={(value) => Number(value).toLocaleString()} />
          <Legend />
          <Area type="monotone" dataKey="revenue" name="Revenue" stroke={chartColors.primary} fill={chartColors.primary} fillOpacity={0.18} />
        </AreaChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
