import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import { ChartCard, donutPalette } from "./chartPrimitives";
import type { StatusCountChartItem } from "./types";

type Props = {
  data: StatusCountChartItem[];
  loading: boolean;
};

export default function DispatcherTripStatusDonut({ data, loading }: Props) {
  return (
    <ChartCard title="Trip Status" loading={loading} empty={data.length === 0} emptyDescription="No active trip status data.">
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <Pie data={data} dataKey="count" nameKey="status" innerRadius={54} outerRadius={86} paddingAngle={2}>
            {data.map((entry, index) => (
              <Cell key={entry.status} fill={donutPalette[index % donutPalette.length]} />
            ))}
          </Pie>
          <Tooltip />
          <Legend />
        </PieChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
