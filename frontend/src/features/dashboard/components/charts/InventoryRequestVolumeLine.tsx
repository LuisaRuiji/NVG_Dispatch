import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChartCard, chartColors } from "./chartPrimitives";
import type { TimeCountChartItem } from "./types";

type Props = {
  data: TimeCountChartItem[];
  loading: boolean;
};

export default function InventoryRequestVolumeLine({ data, loading }: Props) {
  return (
    <ChartCard title="Inventory Requests" loading={loading} empty={data.length === 0} emptyDescription="No inventory request trend available.">
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data}>
          <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
          <XAxis dataKey="period" />
          <YAxis allowDecimals={false} />
          <Tooltip />
          <Legend />
          <Line type="monotone" dataKey="count" name="Requests" stroke={chartColors.primary} strokeWidth={2} dot={false} />
        </LineChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
