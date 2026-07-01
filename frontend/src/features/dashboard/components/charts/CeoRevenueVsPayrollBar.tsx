import FinanceCostGroupedBar from "./FinanceCostGroupedBar";
import type { FinancialBreakdownChartItem } from "./types";

type Props = {
  data: FinancialBreakdownChartItem[];
  loading: boolean;
};

export default function CeoRevenueVsPayrollBar({ data, loading }: Props) {
  return <FinanceCostGroupedBar data={data} loading={loading} />;
}
