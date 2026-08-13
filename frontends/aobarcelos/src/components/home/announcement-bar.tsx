import Link from "next/link";
import { Info, AlertTriangle, CalendarClock } from "lucide-react";
import type { Announcement } from "@/lib/api-types";

export function AnnouncementBar({ announcement }: { announcement: Announcement | null }) {
  if (!announcement) return null;

  const styles: Record<string, string> = {
    info: "bg-brand-500/95 text-white",
    warning: "bg-amber-500/95 text-earth-900",
    event: "bg-earth-900 text-gold-400",
  };
  const icons: Record<string, React.ReactNode> = {
    info: <Info className="h-4 w-4" strokeWidth={2} aria-hidden />,
    warning: <AlertTriangle className="h-4 w-4" strokeWidth={2} aria-hidden />,
    event: <CalendarClock className="h-4 w-4" strokeWidth={2} aria-hidden />,
  };
  const tone = announcement.tone ?? "info";
  const cls = styles[tone] ?? styles.info;

  const inner = (
    <div className="mx-auto flex max-w-6xl items-center justify-center gap-2 px-4 py-2 text-sm">
      {icons[tone] ?? icons.info}
      <span className="text-center">{announcement.message}</span>
      {announcement.href && (
        <span className="hidden text-[13px] font-semibold underline underline-offset-4 sm:inline">
          Saber mais →
        </span>
      )}
    </div>
  );

  if (announcement.href) {
    return (
      <Link href={announcement.href} className={`block ${cls} transition hover:brightness-105`}>
        {inner}
      </Link>
    );
  }
  return <div className={cls}>{inner}</div>;
}
