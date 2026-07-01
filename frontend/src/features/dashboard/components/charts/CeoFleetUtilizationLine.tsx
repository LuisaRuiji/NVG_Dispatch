import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChartCard, chartColors } from "./chartPrimitives";
import type { PercentByPeriodChartItem } from "./types";

type Props = {
  data: PercentByPeriodChartItem[];
  loading: boolean;
};

export default function CeoFleetUtilizationLine({ data, loading }: Props) {
  return (
    <ChartCard title="Fleet Utilization" loading={loading} empty={data.length === 0} emptyDescription="No fleet utilization trend available.">
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data}>
          <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
          <XAxis dataKey="period" />
          <YAxis unit="%" />
          <Tooltip />
          <Legend />
          <Line type="monotone" dataKey="percent" name="Utilization" stroke={chartColors.primary} strokeWidth={2} dot={false} />
        </LineChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
