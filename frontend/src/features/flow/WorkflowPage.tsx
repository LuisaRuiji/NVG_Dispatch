
import { useMemo, useState } from "react";
import { authApi, assetApi, inventoryApi, loanApi, requestApi } from "@/lib/api/index";
import type {
  ApprovalDecision,
  AssetType,
  CurrentUserResponse,
  ItemType,
  LoanDetailResponse,
  LoanStatus,
  RequestStatus
} from "@/lib/api/types";
import { apiBaseUrl } from "@/lib/api/client";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Textarea } from "@/components/ui/textarea";

type SessionKey = "requester" | "io" | "manager";

interface AuthSession {
  token: string;
  user: CurrentUserResponse;
}

interface LoginFormState {
  username: string;
  password: string;
  error?: string;
  loading?: boolean;
}

interface SetupState {
  assetCode: string;
  assetType: AssetType;
  plateNo: string;
  itemName: string;
  itemUnit: string;
  itemType: ItemType;
  itemQuantity: number;
  lastAssetId?: string;
  lastAssetCode?: string;
  lastConsumableId?: string;
  lastNonConsumableId?: string;
  message?: string;
  error?: string;
}

interface MaintenanceFlowState {
  purpose: string;
  assetId: string;
  inventoryId: string;
  quantity: number;
  requestId?: string;
  requestLineId?: string;
  status?: RequestStatus;
  approvalStatus?: string;
  nextApproverRole?: string | null;
  ioQtyApproved: number;
  ioRemarks: string;
  managerDecision: ApprovalDecision;
  managerRemarks: string;
  issueStatus?: RequestStatus;
  message?: string;
  error?: string;
}

interface BorrowFlowState {
  purpose: string;
  inventoryId: string;
  quantity: number;
  requestId?: string;
  requestLineId?: string;
  status?: RequestStatus;
  loanId?: string;
  loanLineId?: string;
  loanStatus?: LoanStatus;
  ioQtyApproved: number;
  ioRemarks: string;
  managerDecision: ApprovalDecision;
  managerRemarks: string;
  returnQty: number;
  returnCondition: "GOOD" | "DAMAGED" | "LOST";
  returnMissingComponents: string;
  message?: string;
  error?: string;
}

const defaultLoginState = { username: "", password: "" };

export default function WorkflowPage() {
  const [sessions, setSessions] = useState<Record<SessionKey, AuthSession | undefined>>({
    requester: undefined,
    io: undefined,
    manager: undefined
  });

  const [loginForms, setLoginForms] = useState<Record<SessionKey, LoginFormState>>({
    requester: { ...defaultLoginState },
    io: { ...defaultLoginState },
    manager: { ...defaultLoginState }
  });

  const [setup, setSetup] = useState<SetupState>({
    assetCode: "",
    assetType: "TRUCK",
    plateNo: "",
    itemName: "",
    itemUnit: "PCS",
    itemType: "CONSUMABLE",
    itemQuantity: 1
  });

  const [maintenance, setMaintenance] = useState<MaintenanceFlowState>({
    purpose: "Routine maintenance",
    assetId: "",
    inventoryId: "",
    quantity: 1,
    ioQtyApproved: 1,
    ioRemarks: "",
    managerDecision: "APPROVE",
    managerRemarks: ""
  });

  const [borrow, setBorrow] = useState<BorrowFlowState>({
    purpose: "Field use",
    inventoryId: "",
    quantity: 1,
    ioQtyApproved: 1,
    ioRemarks: "",
    managerDecision: "APPROVE",
    managerRemarks: "",
    returnQty: 1,
    returnCondition: "GOOD",
    returnMissingComponents: ""
  });

  const isReady = useMemo(() => Object.values(sessions).some(Boolean), [sessions]);

  const getToken = (key: SessionKey) => sessions[key]?.token;

  const updateLoginForm = (key: SessionKey, patch: Partial<LoginFormState>) => {
    setLoginForms((prev) => ({ ...prev, [key]: { ...prev[key], ...patch } }));
  };

  const handleLogin = async (key: SessionKey) => {
    const form = loginForms[key];
    if (!form.username || !form.password) {
      updateLoginForm(key, { error: "Username and password are required." });
      return;
    }

    updateLoginForm(key, { loading: true, error: undefined });
    try {
      const response = await authApi.login({
        username: form.username,
        password: form.password
      });
      if (!("accessToken" in response)) {
        updateLoginForm(key, {
          loading: false,
          error: "MFA is required for this account. Use the main login flow."
        });
        return;
      }

      const accessToken = response.accessToken;
      const me = await authApi.me(accessToken);
      setSessions((prev) => ({ ...prev, [key]: { token: accessToken, user: me } }));
      updateLoginForm(key, { loading: false });
    } catch (error) {
      updateLoginForm(key, {
        loading: false,
        error: error instanceof Error ? error.message : "Login failed."
      });
    }
  };

  const handleCreateAsset = async () => {
    const token = getToken("requester");
    if (!token) {
      setSetup((prev) => ({ ...prev, error: "Login as requester to create assets." }));
      return;
    }

    try {
      const asset = await assetApi.createAsset(
        {
          assetCode: setup.assetCode,
          assetType: setup.assetType,
          plateNo: setup.plateNo || undefined,
          status: "ACTIVE"
        },
        token
      );
      setSetup((prev) => ({
        ...prev,
        lastAssetId: asset.id,
        lastAssetCode: asset.assetCode,
        message: `Asset ${asset.assetCode} created.`,
        error: undefined
      }));
      setMaintenance((prev) => ({ ...prev, assetId: asset.id }));
    } catch (error) {
      setSetup((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "Failed to create asset."
      }));
    }
  };

  const handleCreateInventory = async () => {
    const token = getToken("requester");
    if (!token) {
      setSetup((prev) => ({ ...prev, error: "Login as requester to create inventory items." }));
      return;
    }

    try {
      const item = await inventoryApi.createItem(
        {
          name: setup.itemName,
          unit: setup.itemUnit,
          itemType: setup.itemType,
          quantity: setup.itemQuantity
        },
        token
      );
      setSetup((prev) => ({
        ...prev,
        message: `${item.name} created.`,
        error: undefined,
        lastConsumableId: item.itemType === "CONSUMABLE" ? item.id : prev.lastConsumableId,
        lastNonConsumableId: item.itemType === "NON_CONSUMABLE" ? item.id : prev.lastNonConsumableId
      }));
      if (item.itemType === "CONSUMABLE") {
        setMaintenance((prev) => ({ ...prev, inventoryId: item.id }));
      } else {
        setBorrow((prev) => ({ ...prev, inventoryId: item.id }));
      }
    } catch (error) {
      setSetup((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "Failed to create item."
      }));
    }
  };
  const handleMaintenanceDraft = async () => {
    const token = getToken("requester");
    if (!token) {
      setMaintenance((prev) => ({ ...prev, error: "Login as requester to create draft." }));
      return;
    }

    try {
      const created = await requestApi.createDraft(
        {
          requestType: "MAINTENANCE_ISSUE",
          assetId: maintenance.assetId,
          purpose: maintenance.purpose,
          lines: [
            {
              inventoryId: maintenance.inventoryId,
              quantity: maintenance.quantity,
              remarks: null
            }
          ]
        },
        token
      );

      const detail = await requestApi.detail(created.requestId, token);
      const lineId = detail.lines[0]?.id;

      setMaintenance((prev) => ({
        ...prev,
        requestId: created.requestId,
        requestLineId: lineId,
        status: created.status,
        message: "Draft created.",
        error: undefined
      }));
    } catch (error) {
      setMaintenance((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "Failed to create draft."
      }));
    }
  };

  const handleSubmitMaintenance = async () => {
    const token = getToken("requester");
    if (!token || !maintenance.requestId) {
      setMaintenance((prev) => ({ ...prev, error: "Missing requester login or request." }));
      return;
    }

    try {
      const result = await requestApi.submit(maintenance.requestId, token);
      const detail = await requestApi.detail(maintenance.requestId, token);
      setMaintenance((prev) => ({
        ...prev,
        status: result.status,
        approvalStatus: detail.approval?.status,
        nextApproverRole: detail.approval?.nextApproverRole,
        message: "Submitted for IO review.",
        error: undefined
      }));
    } catch (error) {
      setMaintenance((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "Submit failed."
      }));
    }
  };

  const handleIoReviewMaintenance = async () => {
    const token = getToken("io");
    if (!token || !maintenance.requestId || !maintenance.requestLineId) {
      setMaintenance((prev) => ({ ...prev, error: "Missing IO login or request line." }));
      return;
    }

    try {
      const result = await requestApi.ioReview(
        maintenance.requestId,
        {
          ioRemarks: maintenance.ioRemarks,
          lines: [
            {
              requestLineId: maintenance.requestLineId,
              qtyApproved: maintenance.ioQtyApproved,
              remarks: null
            }
          ]
        },
        token
      );
      setMaintenance((prev) => ({
        ...prev,
        status: result.status,
        message: "IO review recorded.",
        error: undefined
      }));
    } catch (error) {
      setMaintenance((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "IO review failed."
      }));
    }
  };

  const handleManagerApproveMaintenance = async () => {
    const token = getToken("manager");
    if (!token || !maintenance.requestId) {
      setMaintenance((prev) => ({ ...prev, error: "Missing manager login or request." }));
      return;
    }

    try {
      const result = await requestApi.managerDecision(
        maintenance.requestId,
        {
          decision: maintenance.managerDecision,
          remarks: maintenance.managerRemarks
        },
        token
      );
      setMaintenance((prev) => ({
        ...prev,
        status: result.status,
        message: "Manager decision saved.",
        error: undefined
      }));
    } catch (error) {
      setMaintenance((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "Manager decision failed."
      }));
    }
  };

  const handleIssueMaintenance = async () => {
    const token = getToken("io");
    if (!token || !maintenance.requestId) {
      setMaintenance((prev) => ({ ...prev, error: "Missing IO login or request." }));
      return;
    }

    try {
      const result = await requestApi.issueMaintenance(maintenance.requestId, token);
      setMaintenance((prev) => ({
        ...prev,
        issueStatus: result.status,
        message: "Issued successfully.",
        error: undefined
      }));
    } catch (error) {
      setMaintenance((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "Issue failed."
      }));
    }
  };
  const handleBorrowDraft = async () => {
    const token = getToken("requester");
    if (!token) {
      setBorrow((prev) => ({ ...prev, error: "Login as requester to create draft." }));
      return;
    }

    try {
      const created = await requestApi.createDraft(
        {
          requestType: "BORROW",
          assetId: null,
          purpose: borrow.purpose,
          lines: [
            {
              inventoryId: borrow.inventoryId,
              quantity: borrow.quantity,
              remarks: null
            }
          ]
        },
        token
      );

      const detail = await requestApi.detail(created.requestId, token);
      const lineId = detail.lines[0]?.id;

      setBorrow((prev) => ({
        ...prev,
        requestId: created.requestId,
        requestLineId: lineId,
        status: created.status,
        message: "Borrow draft created.",
        error: undefined
      }));
    } catch (error) {
      setBorrow((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "Borrow draft failed."
      }));
    }
  };

  const handleSubmitBorrow = async () => {
    const token = getToken("requester");
    if (!token || !borrow.requestId) {
      setBorrow((prev) => ({ ...prev, error: "Missing requester login or request." }));
      return;
    }

    try {
      const result = await requestApi.submit(borrow.requestId, token);
      setBorrow((prev) => ({
        ...prev,
        status: result.status,
        message: "Borrow submitted.",
        error: undefined
      }));
    } catch (error) {
      setBorrow((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "Submit failed."
      }));
    }
  };

  const handleIoReviewBorrow = async () => {
    const token = getToken("io");
    if (!token || !borrow.requestId || !borrow.requestLineId) {
      setBorrow((prev) => ({ ...prev, error: "Missing IO login or request line." }));
      return;
    }

    try {
      const result = await requestApi.ioReview(
        borrow.requestId,
        {
          ioRemarks: borrow.ioRemarks,
          lines: [
            {
              requestLineId: borrow.requestLineId,
              qtyApproved: borrow.ioQtyApproved,
              remarks: null
            }
          ]
        },
        token
      );
      setBorrow((prev) => ({
        ...prev,
        status: result.status,
        message: "IO review recorded.",
        error: undefined
      }));
    } catch (error) {
      setBorrow((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "IO review failed."
      }));
    }
  };

  const handleManagerApproveBorrow = async () => {
    const token = getToken("manager");
    if (!token || !borrow.requestId) {
      setBorrow((prev) => ({ ...prev, error: "Missing manager login or request." }));
      return;
    }

    try {
      const result = await requestApi.managerDecision(
        borrow.requestId,
        {
          decision: borrow.managerDecision,
          remarks: borrow.managerRemarks
        },
        token
      );
      setBorrow((prev) => ({
        ...prev,
        status: result.status,
        message: "Manager decision saved.",
        error: undefined
      }));
    } catch (error) {
      setBorrow((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "Manager decision failed."
      }));
    }
  };

  const handleIssueBorrow = async () => {
    const token = getToken("io");
    if (!token || !borrow.requestId) {
      setBorrow((prev) => ({ ...prev, error: "Missing IO login or request." }));
      return;
    }

    try {
      const result = await requestApi.issueStock(borrow.requestId, token);
      setBorrow((prev) => ({
        ...prev,
        status: result.status,
        message: "Borrow issued.",
        error: undefined
      }));
      await refreshLoanForBorrow(token, borrow.requestId);
    } catch (error) {
      setBorrow((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "Issue failed."
      }));
    }
  };

  const refreshLoanForBorrow = async (token: string, requestId: string) => {
    const loans = await loanApi.list("OPEN", token);
    const loan = loans.find((item) => item.requestId === requestId);
    if (!loan) {
      return;
    }
    const detail = await loanApi.detail(loan.id, token);
    const lineId = detail.lines[0]?.id;
    setBorrow((prev) => ({
      ...prev,
      loanId: loan.id,
      loanLineId: lineId,
      loanStatus: detail.status
    }));
  };

  const handleLoanReturn = async () => {
    const token = getToken("io");
    if (!token || !borrow.loanId || !borrow.loanLineId) {
      setBorrow((prev) => ({ ...prev, error: "Missing IO login or loan info." }));
      return;
    }

    try {
      const result = await loanApi.returnLoan(
        borrow.loanId,
        {
          lines: [
            {
              loanLineId: borrow.loanLineId,
              qtyReturnedIncrement: borrow.returnQty,
              condition: borrow.returnCondition,
              missingComponentsJson: borrow.returnMissingComponents || null
            }
          ]
        },
        token
      );
      const detail = await loanApi.detail(borrow.loanId, token);
      setBorrow((prev) => ({
        ...prev,
        loanStatus: result.status,
        message: `Return recorded (${result.status}).`,
        error: undefined
      }));
      hydrateBorrowFromLoan(detail);
    } catch (error) {
      setBorrow((prev) => ({
        ...prev,
        error: error instanceof Error ? error.message : "Return failed."
      }));
    }
  };

  const hydrateBorrowFromLoan = (detail: LoanDetailResponse) => {
    setBorrow((prev) => ({
      ...prev,
      loanId: detail.id,
      loanLineId: detail.lines[0]?.id,
      loanStatus: detail.status
    }));
  };

  return (
    <div className="min-h-screen">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-10 px-6 pb-24 pt-12">
        <header className="space-y-6">
          <div className="flex flex-wrap items-center justify-between gap-6">
            <div className="space-y-2">
              <p className="chip">NVG Inventory Ops UI</p>
              <h1 className="text-3xl font-semibold text-foreground md:text-4xl">
                Vertical Workflow Console
              </h1>
              <p className="max-w-2xl text-sm text-muted-foreground">
                Authenticate once, then drive maintenance requests, approvals, issuance, borrow flows, and loan returns
                from a single guided lane. API base: <span className="font-mono">{apiBaseUrl}</span>
              </p>
            </div>
            <Badge variant={isReady ? "secondary" : "outline"}>
              {isReady ? "Authenticated" : "Login Required"}
            </Badge>
          </div>
        </header>

        <section className="grid gap-6 md:grid-cols-3">
          {(["requester", "io", "manager"] as SessionKey[]).map((key) => {
            const session = sessions[key];
            const form = loginForms[key];
            return (
              <Card key={key}>
                <CardHeader>
                  <CardTitle>{key.toUpperCase()} Login</CardTitle>
                  <CardDescription>Authenticate as {key} to perform each workflow step.</CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="space-y-2">
                    <Label>Username</Label>
                    <Input
                      value={form.username}
                      onChange={(event) => updateLoginForm(key, { username: event.target.value })}
                      placeholder={key === "requester" ? "requester username" : `${key} username`}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Password</Label>
                    <Input
                      type="password"
                      value={form.password}
                      onChange={(event) => updateLoginForm(key, { password: event.target.value })}
                      placeholder="password"
                    />
                  </div>
                  {form.error ? <p className="text-sm text-destructive">{form.error}</p> : null}
                  {session ? (
                    <div className="rounded-2xl border border-border bg-muted/60 p-3 text-xs text-muted-foreground">
                      <p className="font-semibold text-foreground">{session.user.username}</p>
                      <p>UserId: {session.user.userId}</p>
                      <p>Roles: {session.user.roles.join(", ") || "None"}</p>
                    </div>
                  ) : null}
                  <Button
                    className="w-full"
                    onClick={() => handleLogin(key)}
                    disabled={form.loading}
                  >
                    {form.loading ? "Signing in..." : "Sign In"}
                  </Button>
                </CardContent>
              </Card>
            );
          })}
        </section>

        <Separator />

        <section className="grid gap-6 lg:grid-cols-[1.1fr_1fr]">
          <Card>
            <CardHeader>
              <CardTitle>Setup: Assets & Inventory</CardTitle>
              <CardDescription>Create the asset and inventory items used in the flows.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-3">
                  <Label>Asset Code</Label>
                  <Input
                    value={setup.assetCode}
                    onChange={(event) => setSetup((prev) => ({ ...prev, assetCode: event.target.value }))}
                    placeholder="TRK-500"
                  />
                </div>
                <div className="space-y-3">
                  <Label>Asset Type</Label>
                  <div className="flex gap-2">
                    {(["TRUCK", "TRAILER"] as AssetType[]).map((type) => (
                      <Button
                        key={type}
                        type="button"
                        variant={setup.assetType === type ? "default" : "outline"}
                        onClick={() => setSetup((prev) => ({ ...prev, assetType: type }))}
                      >
                        {type}
                      </Button>
                    ))}
                  </div>
                </div>
                <div className="space-y-3 md:col-span-2">
                  <Label>Plate No (optional)</Label>
                  <Input
                    value={setup.plateNo}
                    onChange={(event) => setSetup((prev) => ({ ...prev, plateNo: event.target.value }))}
                    placeholder="ABC-1234"
                  />
                </div>
              </div>
              <Button onClick={handleCreateAsset}>Create Asset</Button>

              <Separator />

              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-3">
                  <Label>Item Name</Label>
                  <Input
                    value={setup.itemName}
                    onChange={(event) => setSetup((prev) => ({ ...prev, itemName: event.target.value }))}
                    placeholder="Engine Oil"
                  />
                </div>
                <div className="space-y-3">
                  <Label>Unit</Label>
                  <Input
                    value={setup.itemUnit}
                    onChange={(event) => setSetup((prev) => ({ ...prev, itemUnit: event.target.value }))}
                    placeholder="L"
                  />
                </div>
                <div className="space-y-3">
                  <Label>Item Type</Label>
                  <div className="flex gap-2">
                    {(["CONSUMABLE", "NON_CONSUMABLE"] as ItemType[]).map((type) => (
                      <Button
                        key={type}
                        type="button"
                        variant={setup.itemType === type ? "default" : "outline"}
                        onClick={() => setSetup((prev) => ({ ...prev, itemType: type }))}
                      >
                        {type}
                      </Button>
                    ))}
                  </div>
                </div>
                <div className="space-y-3">
                  <Label>Quantity</Label>
                  <Input
                    type="number"
                    min={0}
                    step="0.01"
                    value={setup.itemQuantity}
                    onChange={(event) =>
                      setSetup((prev) => ({ ...prev, itemQuantity: Number(event.target.value) }))
                    }
                  />
                </div>
              </div>
              <Button onClick={handleCreateInventory}>Create Inventory Item</Button>
              {setup.message ? <p className="text-sm text-primary">{setup.message}</p> : null}
              {setup.error ? <p className="text-sm text-destructive">{setup.error}</p> : null}
              <div className="rounded-2xl border border-border bg-muted/60 p-4 text-xs text-muted-foreground">
                <p>Last asset: {setup.lastAssetCode || "—"}</p>
                <p>Last consumable: {setup.lastConsumableId || "—"}</p>
                <p>Last non-consumable: {setup.lastNonConsumableId || "—"}</p>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Flow Summary</CardTitle>
              <CardDescription>Track the IDs produced at each step.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4 text-sm text-muted-foreground">
              <div className="space-y-1">
                <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">Maintenance</p>
                <p>Request ID: {maintenance.requestId || "—"}</p>
                <p>Line ID: {maintenance.requestLineId || "—"}</p>
                <p>Status: {maintenance.status || "—"}</p>
              </div>
              <div className="space-y-1">
                <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">Borrow</p>
                <p>Request ID: {borrow.requestId || "—"}</p>
                <p>Line ID: {borrow.requestLineId || "—"}</p>
                <p>Loan ID: {borrow.loanId || "—"}</p>
                <p>Loan Line ID: {borrow.loanLineId || "—"}</p>
                <p>Loan Status: {borrow.loanStatus || "—"}</p>
              </div>
            </CardContent>
          </Card>
        </section>

        <Separator />
        <section className="space-y-6">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div>
              <p className="chip">Maintenance Issue Flow</p>
              <h2 className="text-2xl font-semibold">Create -&gt; Submit -&gt; IO Review -&gt; Manager Approve -&gt; IO Issue</h2>
            </div>
            <Badge variant="outline">Consumables only + asset required</Badge>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>1. Create Maintenance Issue Draft</CardTitle>
              <CardDescription>Requester creates a draft request.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Purpose</Label>
                <Textarea
                  value={maintenance.purpose}
                  onChange={(event) => setMaintenance((prev) => ({ ...prev, purpose: event.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Asset Id</Label>
                <Input
                  value={maintenance.assetId}
                  onChange={(event) => setMaintenance((prev) => ({ ...prev, assetId: event.target.value }))}
                  placeholder="asset id"
                />
                <Label>Inventory Id (Consumable)</Label>
                <Input
                  value={maintenance.inventoryId}
                  onChange={(event) => setMaintenance((prev) => ({ ...prev, inventoryId: event.target.value }))}
                  placeholder="inventory id"
                />
                <Label>Quantity</Label>
                <Input
                  type="number"
                  min={0}
                  step="0.01"
                  value={maintenance.quantity}
                  onChange={(event) =>
                    setMaintenance((prev) => ({ ...prev, quantity: Number(event.target.value) }))
                  }
                />
              </div>
              <div className="md:col-span-2 flex flex-wrap items-center gap-3">
                <Button onClick={handleMaintenanceDraft}>Create Draft</Button>
                <Badge variant="secondary">{maintenance.status || "Draft not created"}</Badge>
              </div>
              {maintenance.message ? <p className="text-sm text-primary">{maintenance.message}</p> : null}
              {maintenance.error ? <p className="text-sm text-destructive">{maintenance.error}</p> : null}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>2. Submit Request</CardTitle>
              <CardDescription>Moves the draft into IO review.</CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-4 md:flex-row md:items-center">
              <Button onClick={handleSubmitMaintenance}>Submit Draft</Button>
              <div className="text-sm text-muted-foreground">
                Status: {maintenance.status || "—"} | Next: {maintenance.nextApproverRole || "—"}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>3. IO Review</CardTitle>
              <CardDescription>Inventory Officer sets approved quantity.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Qty Approved</Label>
                <Input
                  type="number"
                  min={0}
                  step="0.01"
                  value={maintenance.ioQtyApproved}
                  onChange={(event) =>
                    setMaintenance((prev) => ({ ...prev, ioQtyApproved: Number(event.target.value) }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>IO Remarks</Label>
                <Input
                  value={maintenance.ioRemarks}
                  onChange={(event) => setMaintenance((prev) => ({ ...prev, ioRemarks: event.target.value }))}
                />
              </div>
              <div className="md:col-span-2 flex items-center gap-3">
                <Button onClick={handleIoReviewMaintenance}>Submit IO Review</Button>
                <Badge variant="outline">{maintenance.status || "—"}</Badge>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>4. Manager Decision</CardTitle>
              <CardDescription>Approve or reject the request.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Decision</Label>
                <div className="flex gap-2">
                  {(["APPROVE", "REJECT"] as ApprovalDecision[]).map((decision) => (
                    <Button
                      key={decision}
                      variant={maintenance.managerDecision === decision ? "default" : "outline"}
                      onClick={() => setMaintenance((prev) => ({ ...prev, managerDecision: decision }))}
                    >
                      {decision}
                    </Button>
                  ))}
                </div>
              </div>
              <div className="space-y-2">
                <Label>Manager Remarks</Label>
                <Input
                  value={maintenance.managerRemarks}
                  onChange={(event) => setMaintenance((prev) => ({ ...prev, managerRemarks: event.target.value }))}
                />
              </div>
              <div className="md:col-span-2 flex items-center gap-3">
                <Button onClick={handleManagerApproveMaintenance}>Save Decision</Button>
                <Badge variant="outline">{maintenance.status || "—"}</Badge>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>5. IO Issue</CardTitle>
              <CardDescription>Issue stock and close the request.</CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-4 md:flex-row md:items-center">
              <Button onClick={handleIssueMaintenance}>Issue Maintenance Items</Button>
              <Badge variant="secondary">{maintenance.issueStatus || maintenance.status || "—"}</Badge>
            </CardContent>
          </Card>
        </section>

        <Separator />
        <section className="space-y-6">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div>
              <p className="chip">Borrow + Loan Return</p>
              <h2 className="text-2xl font-semibold">Borrow -&gt; Issue -&gt; Return</h2>
            </div>
            <Badge variant="outline">Non-consumables only</Badge>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>1. Create Borrow Draft</CardTitle>
              <CardDescription>Requester creates a borrow request draft.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Purpose</Label>
                <Textarea
                  value={borrow.purpose}
                  onChange={(event) => setBorrow((prev) => ({ ...prev, purpose: event.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Inventory Id (Non-consumable)</Label>
                <Input
                  value={borrow.inventoryId}
                  onChange={(event) => setBorrow((prev) => ({ ...prev, inventoryId: event.target.value }))}
                  placeholder="inventory id"
                />
                <Label>Quantity</Label>
                <Input
                  type="number"
                  min={0}
                  step="0.01"
                  value={borrow.quantity}
                  onChange={(event) => setBorrow((prev) => ({ ...prev, quantity: Number(event.target.value) }))}
                />
              </div>
              <div className="md:col-span-2 flex items-center gap-3">
                <Button onClick={handleBorrowDraft}>Create Draft</Button>
                <Badge variant="secondary">{borrow.status || "Draft not created"}</Badge>
              </div>
              {borrow.message ? <p className="text-sm text-primary">{borrow.message}</p> : null}
              {borrow.error ? <p className="text-sm text-destructive">{borrow.error}</p> : null}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>2. Submit Borrow Request</CardTitle>
              <CardDescription>Submit to IO review.</CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-4 md:flex-row md:items-center">
              <Button onClick={handleSubmitBorrow}>Submit Borrow</Button>
              <Badge variant="outline">{borrow.status || "—"}</Badge>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>3. IO Review</CardTitle>
              <CardDescription>Approve quantity to issue.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Qty Approved</Label>
                <Input
                  type="number"
                  min={0}
                  step="0.01"
                  value={borrow.ioQtyApproved}
                  onChange={(event) =>
                    setBorrow((prev) => ({ ...prev, ioQtyApproved: Number(event.target.value) }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>IO Remarks</Label>
                <Input
                  value={borrow.ioRemarks}
                  onChange={(event) => setBorrow((prev) => ({ ...prev, ioRemarks: event.target.value }))}
                />
              </div>
              <div className="md:col-span-2 flex items-center gap-3">
                <Button onClick={handleIoReviewBorrow}>Submit IO Review</Button>
                <Badge variant="outline">{borrow.status || "—"}</Badge>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>4. Manager Decision</CardTitle>
              <CardDescription>Approve for issuance.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Decision</Label>
                <div className="flex gap-2">
                  {(["APPROVE", "REJECT"] as ApprovalDecision[]).map((decision) => (
                    <Button
                      key={decision}
                      variant={borrow.managerDecision === decision ? "default" : "outline"}
                      onClick={() => setBorrow((prev) => ({ ...prev, managerDecision: decision }))}
                    >
                      {decision}
                    </Button>
                  ))}
                </div>
              </div>
              <div className="space-y-2">
                <Label>Manager Remarks</Label>
                <Input
                  value={borrow.managerRemarks}
                  onChange={(event) => setBorrow((prev) => ({ ...prev, managerRemarks: event.target.value }))}
                />
              </div>
              <div className="md:col-span-2 flex items-center gap-3">
                <Button onClick={handleManagerApproveBorrow}>Save Decision</Button>
                <Badge variant="outline">{borrow.status || "—"}</Badge>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>5. IO Issue + Loan Creation</CardTitle>
              <CardDescription>Issue stock and open a loan.</CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-4 md:flex-row md:items-center">
              <Button onClick={handleIssueBorrow}>Issue Borrow</Button>
              <Badge variant="secondary">{borrow.status || "—"}</Badge>
              <span className="text-sm text-muted-foreground">
                Loan: {borrow.loanId || "—"}
              </span>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>6. Loan Return</CardTitle>
              <CardDescription>Record returned quantity and condition.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Return Qty</Label>
                <Input
                  type="number"
                  min={0}
                  step="0.01"
                  value={borrow.returnQty}
                  onChange={(event) => setBorrow((prev) => ({ ...prev, returnQty: Number(event.target.value) }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Condition</Label>
                <div className="flex gap-2">
                  {(["GOOD", "DAMAGED", "LOST"] as const).map((condition) => (
                    <Button
                      key={condition}
                      variant={borrow.returnCondition === condition ? "default" : "outline"}
                      onClick={() => setBorrow((prev) => ({ ...prev, returnCondition: condition }))}
                    >
                      {condition}
                    </Button>
                  ))}
                </div>
              </div>
              <div className="md:col-span-2 space-y-2">
                <Label>Missing Components (JSON, optional)</Label>
                <Textarea
                  value={borrow.returnMissingComponents}
                  onChange={(event) =>
                    setBorrow((prev) => ({ ...prev, returnMissingComponents: event.target.value }))
                  }
                />
              </div>
              <div className="md:col-span-2 flex items-center gap-3">
                <Button onClick={handleLoanReturn}>Record Return</Button>
                <Badge variant="outline">{borrow.loanStatus || "—"}</Badge>
              </div>
            </CardContent>
          </Card>
        </section>
      </div>
    </div>
  );
}
