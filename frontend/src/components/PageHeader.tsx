import type { ReactNode } from "react";

type Props = {
  title: string;
  description?: string;
  actions?: ReactNode;
  breadcrumbs?: ReactNode;
};

export default function PageHeader({ title, description, actions, breadcrumbs }: Props) {
  return (
    <div className="mb-5 flex flex-wrap items-start justify-between gap-4">
      <div>
        {breadcrumbs ? (
          <div className="mb-2 text-xs text-muted-foreground">{breadcrumbs}</div>
        ) : null}
        <h1 className="text-[26px] font-bold leading-tight text-foreground">{title}</h1>
        {description ? <p className="mt-1 text-sm text-muted-foreground">{description}</p> : null}
      </div>
      {actions ? <div className="flex items-center gap-2">{actions}</div> : null}
    </div>
  );
}
