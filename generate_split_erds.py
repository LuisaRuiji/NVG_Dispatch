import re

def main():
    erd_file = r"d:\Capstone Project\NVG_Dispatch\Docs\System_Architecture_ERD.md"
    
    with open(erd_file, 'r', encoding='utf-8') as f:
        content = f.read()

    # We will split Section 5 of the document into beautiful sub-sections
    # Let's replace everything from the markdown header "## 5. Complete Text-Based ERD Summary" (or where the Mermaid block begins)
    # Actually, we can just replace the entire end of the file.
    # Let's write the split ERDs in Mermaid.
    
    # 1. IAM & Security ERD
    iam_erd = """```mermaid
erDiagram
    users {
        Guid id PK
        Guid customer_id FK
        string email
        bool is_active
        bool mfa_enabled
        string username
    }
    roles {
        int id PK
        string name
    }
    user_roles {
        Guid user_id FK
        int role_id FK
    }
    mfa_challenges {
        Guid id PK
        Guid user_id FK
        string method
    }
    refresh_tokens {
        Guid id PK
        Guid user_id FK
        string token_hash
    }
    auth_events {
        Guid id PK
        Guid user_id FK
        string event_type
    }
    push_notification_tokens {
        Guid id PK
        Guid user_id FK
        string token
    }
    module_settings {
        string module_key PK
        Guid updated_by_user_id FK
    }

    roles ||--o{ user_roles : "RoleId"
    users ||--o{ user_roles : "UserId"
    users ||--o{ mfa_challenges : "UserId"
    users ||--o{ refresh_tokens : "UserId"
    users ||--o{ auth_events : "UserId"
    users ||--o{ push_notification_tokens : "UserId"
    users |o--o{ module_settings : "UpdatedByUserId"
```"""

    # 2. Inventory & Procurement ERD
    inventory_erd = """```mermaid
erDiagram
    inventory {
        Guid id PK
        string name
        decimal quantity
        decimal average_cost
    }
    kit_components {
        Guid id PK
        Guid inventory_id FK
    }
    inventory_adjustments {
        Guid id PK
        Guid created_by_user_id FK
    }
    inventory_adjustment_lines {
        Guid id PK
        Guid inventory_adjustment_id FK
        Guid inventory_id FK
    }
    suppliers {
        Guid id PK
        string name
    }
    purchase_orders {
        Guid id PK
        Guid created_by_user_id FK
        Guid supplier_id FK
    }
    purchase_order_lines {
        Guid id PK
        Guid inventory_id FK
        Guid purchase_order_id FK
    }
    purchase_order_receipts {
        Guid id PK
        Guid purchase_order_line_id FK
        Guid received_by_user_id FK
    }
    assets {
        Guid id PK
    }
    requests {
        Guid id PK
        Guid asset_id FK
        Guid requester_user_id FK
    }
    request_lines {
        Guid id PK
        Guid inventory_id FK
        Guid request_id FK
    }
    loans {
        Guid id PK
        Guid asset_id FK
        Guid borrower_user_id FK
        Guid request_id FK
    }
    loan_lines {
        Guid id PK
        Guid inventory_id FK
        Guid loan_id FK
    }
    loan_line_returns {
        Guid id PK
        Guid loan_line_id FK
        Guid received_by_user_id FK
    }

    inventory_adjustments ||--o{ inventory_adjustment_lines : "InventoryAdjustmentId"
    inventory ||--o{ inventory_adjustment_lines : "InventoryId"
    inventory ||--o{ kit_components : "InventoryItemId"
    assets |o--o{ loans : "AssetId"
    requests ||--|| loans : "RequestId"
    inventory ||--o{ loan_lines : "InventoryId"
    loans ||--o{ loan_lines : "LoanId"
    loan_lines ||--o{ loan_line_returns : "LoanLineId"
    suppliers ||--o{ purchase_orders : "SupplierId"
    inventory ||--o{ purchase_order_lines : "InventoryId"
    purchase_orders ||--o{ purchase_order_lines : "PurchaseOrderId"
    purchase_order_lines ||--o{ purchase_order_receipts : "PurchaseOrderLineId"
    assets |o--o{ requests : "AssetId"
    inventory ||--o{ request_lines : "InventoryId"
    requests ||--o{ request_lines : "RequestId"
```"""

    # 3. Fleet & Dispatch Logistics ERD
    dispatch_erd = """```mermaid
erDiagram
    dispatch_customers {
        Guid id PK
        string name
    }
    dispatch_drivers {
        Guid id PK
        Guid user_id FK
    }
    dispatch_trucks {
        Guid id PK
        Guid asset_id FK
        string plate_number
    }
    dispatch_trailers {
        Guid id PK
        Guid asset_id FK
        string trailer_code
    }
    dispatch_trips {
        Guid id PK
        Guid customer_id FK
        Guid driver_user_id FK
        Guid truck_asset_id FK
    }
    dispatch_trip_stops {
        Guid id PK
        Guid trip_id FK
    }
    dispatch_trip_documents {
        Guid id PK
        Guid trip_id FK
        Guid uploaded_by_user_id FK
    }
    dispatch_trip_status_history {
        Guid id PK
        Guid trip_id FK
    }
    shipment_requests {
        Guid id PK
        Guid customer_id FK
        Guid converted_trip_id FK
    }
    shipment_request_documents {
        Guid id PK
        Guid request_id FK
    }
    generated_waybills {
        Guid id PK
        Guid trip_id FK
    }

    assets ||--o{ dispatch_trailers : "AssetId"
    dispatch_customers ||--o{ dispatch_trips : "CustomerId"
    assets |o--o{ dispatch_trips : "TruckAssetId"
    dispatch_trips ||--o{ dispatch_trip_documents : "TripId"
    dispatch_trips ||--o{ dispatch_trip_status_history : "TripId"
    dispatch_trips ||--o{ dispatch_trip_stops : "TripId"
    assets ||--o{ dispatch_trucks : "AssetId"
    dispatch_trips |o--o{ shipment_requests : "ConvertedTripId"
    dispatch_customers ||--o{ shipment_requests : "CustomerId"
    shipment_requests ||--o{ shipment_request_documents : "RequestId"
    dispatch_trips ||--o{ generated_waybills : "TripId"
```"""

    # 4. Routing & AI Optimization ERD
    routing_erd = """```mermaid
erDiagram
    dispatch_optimization_plans {
        Guid id PK
        Guid generated_by_user_id FK
        decimal final_state_cost
    }
    dispatch_optimization_routes {
        Guid id PK
        Guid dispatch_driver_id FK
        Guid dispatch_truck_id FK
        Guid plan_id FK
    }
    dispatch_optimization_route_stops {
        Guid id PK
        Guid route_id FK
        Guid trip_id FK
    }
    dispatch_recommendations {
        Guid id PK
        Guid completed_trip_id FK
        Guid recommended_trip_id FK
    }
    driver_location_updates {
        Guid id PK
        Guid dispatch_driver_id FK
        Guid dispatch_truck_id FK
        Guid tracking_session_id FK
        Guid trip_id FK
    }
    location_tracking_sessions {
        Guid id PK
        Guid dispatch_driver_id FK
        Guid dispatch_truck_id FK
        Guid trip_id FK
    }
    optimization_weight_settings {
        Guid id PK
        Guid created_by_user_id FK
    }

    dispatch_drivers ||--o{ dispatch_optimization_routes : "DispatchDriverId"
    dispatch_trucks ||--o{ dispatch_optimization_routes : "DispatchTruckId"
    dispatch_optimization_plans ||--o{ dispatch_optimization_routes : "PlanId"
    dispatch_optimization_routes ||--o{ dispatch_optimization_route_stops : "RouteId"
    dispatch_trips ||--o{ dispatch_optimization_route_stops : "TripId"
    dispatch_trips ||--o{ dispatch_recommendations : "CompletedTripId"
    dispatch_trips ||--o{ dispatch_recommendations : "RecommendedTripId"
    dispatch_drivers ||--o{ driver_location_updates : "DispatchDriverId"
    dispatch_trucks ||--o{ driver_location_updates : "DispatchTruckId"
    location_tracking_sessions ||--o{ driver_location_updates : "TrackingSessionId"
    dispatch_trips ||--o{ driver_location_updates : "TripId"
    dispatch_drivers ||--o{ location_tracking_sessions : "DispatchDriverId"
    dispatch_trucks ||--o{ location_tracking_sessions : "DispatchTruckId"
    dispatch_trips ||--o{ location_tracking_sessions : "TripId"
```"""

    # Let's locate the Mermaid code block in System_Architecture_ERD.md and replace it
    # We find where `## 5. Database Schema ERD (Mermaid)` or similar header starts
    # If it is not there, let's create a clean section 5.
    
    # Let's split content on `## 5.` or just parse and replace the mermaid section.
    # In the view file, there was no `## 5.` header shown, but line 1255 onwards had `users ||--o{ approvals`
    # Let's look for ````mermaid` and replace it with the new split sections.
    
    pattern = r'```mermaid\s*\n\s*erDiagram\s*\n.*?\n```'
    
    replacement = f"""## 5. Domain-Specific ERDs (Optimized for A4 Printing)

To ensure high readability and enable clean exporting to A4 size pages without line overlapping or extreme stretching, the database schema is divided below into its core business domains.

### A. Identity & Access Management (IAM) Domain
This domain handles user authorization, roles, MFA, and audit histories.

{iam_erd}

### B. Inventory, Assets & Procurement Domain
This domain tracks stock catalogs, supplier procurement POs, manual adjustments, asset requesting, and borrower loans.

{inventory_erd}

### C. Fleet Operations & Dispatch Logistics Domain
This domain handles the shipment requests, truck assets, driver profiles, trailers, waybills, and trip lifecycle history.

{dispatch_erd}

### D. AI Routing & Dispatch Optimization Domain
This domain structures the algorithms, weight weights, recommendations, tracking sessions, and location matrices.

{routing_erd}
"""

    new_content, count = re.subn(pattern, replacement, content, flags=re.DOTALL)
    
    if count > 0:
        with open(erd_file, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print("Successfully split the ERD into logical domain diagrams!")
    else:
        # Fallback: append it
        with open(erd_file, 'a', encoding='utf-8') as f:
            f.write("\n\n" + replacement)
        print("Appended split ERDs to the file.")

if __name__ == '__main__':
    main()
