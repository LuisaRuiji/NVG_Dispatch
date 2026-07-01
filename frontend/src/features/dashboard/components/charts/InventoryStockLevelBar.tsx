import { Bar, BarChart, CartesianGrid, Cell, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChartCard, chartColors } from "./chartPrimitives";
import type { InventoryStockLevelChartItem } from "./types";

type Props = {
  data: InventoryStockLevelChartItem[];
  loading: boolean;
};

export default function InventoryStockLevelBar({ data, loading }: Props) {
  return (
    <ChartCard title="Stock Levels" loading={loading} empty={data.length === 0} emptyDescription="No inventory stock levels available.">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} layout="vertical" margin={{ left: 24 }}>
          <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
          <XAxis type="number" />
          <YAxis type="category" dataKey="itemName" width={120} />
          <Tooltip />
          <Legend />
          <Bar dataKey="quantity" name="Quantity" radius={[0, 6, 6, 0]}>
            {data.map((entry) => (
              <Cell key={entry.itemName} fill={entry.isLowStock ? chartColors.danger : chartColors.primary} />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
