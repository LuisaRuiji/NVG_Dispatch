import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChartCard, chartColors } from "./chartPrimitives";
import type { DriverUtilizationChartItem } from "./types";

type Props = {
  data: DriverUtilizationChartItem[];
  loading: boolean;
};

export default function ManagerDriverUtilizationBar({ data, loading }: Props) {
  return (
    <ChartCard title="Driver Utilization" loading={loading} empty={data.length === 0} emptyDescription="No driver utilization data this month.">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} layout="vertical" margin={{ left: 24 }}>
          <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
          <XAxis type="number" allowDecimals={false} />
          <YAxis type="category" dataKey="driverName" width={110} />
          <Tooltip />
          <Legend />
          <Bar dataKey="trips" name="Trips" fill={chartColors.primary} radius={[0, 6, 6, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
