import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import { ChartCard, donutPalette } from "./chartPrimitives";
import type { RoleCountChartItem } from "./types";

type Props = {
  data: RoleCountChartItem[];
  loading: boolean;
};

export default function AdminUsersByRoleDonut({ data, loading }: Props) {
  return (
    <ChartCard title="Users By Role" loading={loading} empty={data.length === 0} emptyDescription="No active user role data.">
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <Pie data={data} dataKey="count" nameKey="role" innerRadius={54} outerRadius={86} paddingAngle={2}>
            {data.map((entry, index) => (
              <Cell key={entry.role} fill={donutPalette[index % donutPalette.length]} />
            ))}
          </Pie>
          <Tooltip />
          <Legend />
        </PieChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
