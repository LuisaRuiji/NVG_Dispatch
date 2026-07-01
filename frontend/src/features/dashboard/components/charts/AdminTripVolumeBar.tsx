import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChartCard, chartColors } from "./chartPrimitives";
import type { TimeCountChartItem } from "./types";

type Props = {
  data: TimeCountChartItem[];
  loading: boolean;
};

export default function AdminTripVolumeBar({ data, loading }: Props) {
  return (
    <ChartCard title="System Trip Volume" loading={loading} empty={data.length === 0} emptyDescription="No system trip volume data.">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data}>
          <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
          <XAxis dataKey="period" />
          <YAxis allowDecimals={false} />
          <Tooltip />
          <Legend />
          <Bar dataKey="count" name="Trips" fill={chartColors.primary} radius={[6, 6, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
