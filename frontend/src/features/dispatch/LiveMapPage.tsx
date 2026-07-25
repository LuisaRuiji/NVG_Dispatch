import PageHeader from "@/components/PageHeader";
import LiveOperationsMap from "./components/LiveOperationsMap";

export default function LiveMapPage() {
  return (
    <div className="container mx-auto p-4 max-w-7xl h-full flex flex-col space-y-4">
      <PageHeader
        title="Live Operations Map"
        description="Real-time tracking of all active dispatch trips and vehicles."
      />
      <div className="flex-1 min-h-[600px] w-full bg-card rounded-xl border shadow-sm p-2">
         <LiveOperationsMap readOnly={false} />
      </div>
    </div>
  );
}
