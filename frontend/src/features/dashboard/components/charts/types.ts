export type StatusCountChartItem = {
  status: string;
  count: number;
};

export type TimeCountChartItem = {
  period: string;
  count: number;
};

export type DocumentAlertChartItem = {
  documentType: string;
  pendingCount: number;
};

export type PercentByPeriodChartItem = {
  period: string;
  percent: number;
};

export type DriverUtilizationChartItem = {
  driverName: string;
  trips: number;
};

export type OnTimeDelayedChartItem = {
  period: string;
  onTime: number;
  delayed: number;
};

export type FinancialRevenueChartItem = {
  period: string;
  revenue: number;
};

export type FinancialBreakdownChartItem = {
  period: string;
  revenue: number;
  payroll: number;
  fuel: number;
};

export type RoleCountChartItem = {
  role: string;
  count: number;
};

export type InventoryStockLevelChartItem = {
  itemName: string;
  quantity: number;
  reorderLevel?: number | null;
  isLowStock: boolean;
};
