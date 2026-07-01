import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChartCard, chartColors } from "./chartPrimitives";
import type { OnTimeDelayedChartItem } from "./types";

type Props = {
  data: OnTimeDelayedChartItem[];
  loading: boolean;
};

export default function ManagerOnTimeStackedBar({ data, loading }: Props) {
  return (
    <ChartCard title="On-Time vs Delayed" loading={loading} empty={data.length === 0} emptyDescription="No delivery timing data.">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data}>
          <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
          <XAxis dataKey="period" />
          <YAxis allowDecimals={false} />
          <Tooltip />
          <Legend />
          <Bar dataKey="onTime" name="On time" stackId="timing" fill={chartColors.primary} radius={[0, 0, 0, 0]} />
          <Bar dataKey="delayed" name="Delayed" stackId="timing" fill={chartColors.accent} radius={[6, 6, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
