"use client";

import { useEffect, useState } from "react";
import { InscricaoConvoyageForm } from "./inscricao-convoyage-form";
import { RegistrationCountdown } from "./registration-countdown";
import type { ConvoyageActiveYearDto, NomenclatureVersionDto } from "@/lib/api-types";

type Props = {
  siteName: string;
  contactEmail: string | null;
  activeYear: ConvoyageActiveYearDto;
  nomenclature: NomenclatureVersionDto;
};

export function InscricaoConvoyageFormGate(props: Props) {
  const closesAtIso = props.activeYear.registrationClosesAt;
  const [closed, setClosed] = useState<boolean>(() => {
    if (!closesAtIso) return false;
    return new Date(closesAtIso).getTime() <= Date.now();
  });

  useEffect(() => {
    if (!closesAtIso || closed) return;
    const ms = new Date(closesAtIso).getTime() - Date.now();
    if (ms <= 0) {
      setClosed(true);
      return;
    }
    const id = window.setTimeout(() => setClosed(true), ms + 500);
    return () => window.clearTimeout(id);
  }, [closesAtIso, closed]);

  if (closed) return <ClosedNotice />;

  return (
    <>
      {closesAtIso && (
        <RegistrationCountdown closesAtIso={closesAtIso} onClosed={() => setClosed(true)} />
      )}
      <InscricaoConvoyageForm {...props} />
    </>
  );
}

function ClosedNotice() {
  return (
    <div className="rounded-2xl border border-sand-300 bg-white p-8 text-center shadow-sm">
      <p className="text-ink-600">
        As inscrições para a convoyage estão encerradas.
      </p>
    </div>
  );
}
