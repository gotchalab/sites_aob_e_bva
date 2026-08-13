import Link from "next/link";
import type { DownloadSummary } from "@/lib/api-types";
import { formatBytes, formatDate } from "@/lib/api";

export function DownloadItem({ download }: { download: DownloadSummary }) {
  return (
    <li className="flex items-center justify-between gap-4 border-b border-sand-300 py-3 last:border-b-0">
      <div className="min-w-0">
        <Link href={`/downloads/${download.slug}`} className="font-display text-sm font-semibold text-ink-900 hover:text-brand-600">
          {download.title}
        </Link>
        <div className="mt-0.5 text-xs text-ink-500">
          {download.categoryName ?? "Sem categoria"}
          {download.publishedAt ? ` · ${formatDate(download.publishedAt)}` : ""}
          {download.fileSize ? ` · ${formatBytes(download.fileSize)}` : ""}
          {" · "}
          <span>{download.downloadCount} downloads</span>
        </div>
      </div>
      <Link
        href={`/downloads/${download.slug}`}
        className="whitespace-nowrap rounded-full bg-brand-500 px-4 py-1.5 text-xs font-medium text-white shadow-sm transition hover:bg-brand-600"
      >
        Ver
      </Link>
    </li>
  );
}
