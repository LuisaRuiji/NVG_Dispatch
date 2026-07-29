import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import { chartColors } from "@/features/dashboard/components/charts/chartPrimitives";

export type RecommendationAcceptanceChartItem = {
  weekStart: string;
  accepted: number;
  ignored: number;
};

type Props = {
  data: RecommendationAcceptanceChartItem[];
  loading: boolean;
};

export default function RecommendationAcceptanceBar({ data, loading }: Props) {
  return (
    <div className="surface-card p-5">
      <h2 className="text-lg font-semibold">Confirmed vs Dismissed by Week</h2>
      <div className="mt-4 h-72 min-h-72 w-full min-w-[1px]">
        {loading ? (
          <LoadingSkeleton rows={5} />
        ) : data.length === 0 ? (
          <EmptyState title="No Trip Chaining chart data." />
        ) : (
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={data.map((item) => ({ ...item, period: item.weekStart.slice(0, 10) }))}>
              <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
              <XAxis dataKey="period" />
              <YAxis allowDecimals={false} />
              <Tooltip />
              <Legend />
              <Bar dataKey="accepted" name="Confirmed" stackId="recommendations" fill="#16a34a" />
              <Bar dataKey="ignored" name="Dismissed" stackId="recommendations" fill="#f59e0b" radius={[6, 6, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        )}
      </div>
    </div>
  );
}
