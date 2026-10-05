import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { ArrowRight, CheckCircle2, MapPinned, Truck } from "lucide-react";
import PageHeader from "@/components/PageHeader";
import { buttonVariants } from "@/components/ui/button";
import { useDispatchHub } from "@/hooks/useDispatchHub";
import { cn } from "@/lib/utils";
import RecommendationPanel from "./components/RecommendationPanel";

function refreshRecommendations() {
  window.dispatchEvent(new Event("nvg:recommendations-refresh"));
}

export default function TripChainingPage() {
  useDispatchHub({
    onRecommendationGenerated: refreshRecommendations,
    onTripChainingSuggestionsGenerated: refreshRecommendations
  });

  return (
    <div className="pb-10">
      <PageHeader
        title="Trip chaining"
        description="Match an available driver and truck with a nearby next movement to reduce empty travel."
        actions={(
          <Link to="/dispatch/trips" className={cn(buttonVariants({ variant: "outline" }))}>
            View trip records
            <ArrowRight className="h-4 w-4" aria-hidden="true" />
          </Link>
        )}
      />

      <section
        aria-labelledby="trip-chaining-workflow-title"
        className="mb-6 overflow-hidden rounded-[10px] border border-border bg-card"
      >
        <div className="border-b border-border px-4 py-3 sm:px-5">
          <h2 id="trip-chaining-workflow-title" className="text-sm font-semibold text-foreground">
            From completed delivery to next movement
          </h2>
          <p className="mt-1 text-xs text-muted-foreground">
            The system recommends options; a dispatcher or manager always makes the final decision.
          </p>
        </div>

        <div className="grid [&>*+*]:border-t [&>*+*]:border-border md:grid-cols-[1fr_auto_1fr_auto_1fr] md:items-stretch md:[&>*+*]:border-t-0">
          <WorkflowStep
            icon={<Truck className="h-5 w-5" aria-hidden="true" />}
            eyebrow="Available resource"
            title="Delivery completed"
            description="The driver and truck become available at the latest drop-off location."
          />
          <WorkflowArrow />
          <WorkflowStep
            icon={<MapPinned className="h-5 w-5" aria-hidden="true" />}
            eyebrow="System recommendation"
            title="Nearby work is ranked"
            description="Pending trips are compared by distance, availability, truck match, and waiting time."
          />
          <WorkflowArrow />
          <WorkflowStep
            icon={<CheckCircle2 className="h-5 w-5" aria-hidden="true" />}
            eyebrow="Dispatcher decision"
            title="Next movement is confirmed"
            description="Availability and readiness are checked again before the assignment is released."
          />
        </div>
      </section>

      <RecommendationPanel showHeading={false} />
    </div>
  );
}

type WorkflowStepProps = {
  icon: ReactNode;
  eyebrow: string;
  title: string;
  description: string;
};

function WorkflowStep({ icon, eyebrow, title, description }: WorkflowStepProps) {
  return (
    <div className="flex min-w-0 gap-3 px-4 py-4 sm:px-5">
      <div className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-muted text-primary">
        {icon}
      </div>
      <div className="min-w-0">
        <p className="text-[10px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">{eyebrow}</p>
        <h3 className="mt-1 text-sm font-semibold text-foreground">{title}</h3>
        <p className="mt-1 text-xs leading-5 text-muted-foreground">{description}</p>
      </div>
    </div>
  );
}

function WorkflowArrow() {
  return (
    <div className="hidden items-center border-x border-border px-2 text-muted-foreground md:flex" aria-hidden="true">
      <ArrowRight className="h-4 w-4" />
    </div>
  );
}
