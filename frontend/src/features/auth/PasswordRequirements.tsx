import { CheckCircle2, Circle } from "lucide-react";
import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import { getPasswordRequirements } from "./passwordPolicy";

function Requirement({ complete, children }: { complete: boolean; children: ReactNode }) {
  return (
    <li className={cn("flex items-center gap-2", complete ? "text-success" : "text-muted-foreground")}>
      {complete ? <CheckCircle2 className="h-3.5 w-3.5 shrink-0" aria-hidden="true" /> : <Circle className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />}
      <span>{children}</span>
    </li>
  );
}

export default function PasswordRequirements({ password, confirmation }: { password: string; confirmation?: string }) {
  const requirements = getPasswordRequirements(password);
  const hasConfirmation = confirmation !== undefined && confirmation.length > 0;

  return (
    <section className="rounded-lg border border-border bg-muted/50 p-3" aria-labelledby="password-requirements-title">
      <p id="password-requirements-title" className="text-xs font-semibold text-foreground">Password requirements</p>
      <ul className="mt-2 grid gap-1 text-xs" aria-live="polite">
        <Requirement complete={requirements.minimumLength}>15 or more characters</Requirement>
        <Requirement complete={requirements.uppercase}>At least one uppercase letter</Requirement>
        <Requirement complete={requirements.number}>At least one number</Requirement>
        <Requirement complete={requirements.specialCharacter}>At least one special character</Requirement>
        <Requirement complete={requirements.common}>Not a common or easily guessed password</Requirement>
        {hasConfirmation ? <Requirement complete={password === confirmation}>Passwords match</Requirement> : null}
      </ul>
    </section>
  );
}
