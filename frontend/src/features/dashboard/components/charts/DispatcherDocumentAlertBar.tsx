import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChartCard, chartColors } from "./chartPrimitives";
import type { DocumentAlertChartItem } from "./types";

type Props = {
  data: DocumentAlertChartItem[];
  loading: boolean;
};

export default function DispatcherDocumentAlertBar({ data, loading }: Props) {
  return (
    <ChartCard title="Document Alerts" loading={loading} empty={data.length === 0} emptyDescription="No document alerts by type.">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data}>
          <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
          <XAxis dataKey="documentType" />
          <YAxis allowDecimals={false} />
          <Tooltip />
          <Legend />
          <Bar dataKey="pendingCount" name="Pending" fill={chartColors.accent} radius={[6, 6, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
