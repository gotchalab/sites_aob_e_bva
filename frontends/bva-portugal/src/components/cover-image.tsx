"use client";

import { useState } from "react";

/**
 * <img> nativo com fallback para o logo em marca-d'água quando o carregamento falha.
 * Usado nos cards onde não queremos passar por next/image (covers migrados do Joomla
 * podem apontar para URLs voláteis do Facebook CDN que expiram).
 */
export function CoverImage({
  src,
  alt,
  className,
  fallbackClassName = "bg-gradient-to-br from-sand-100 via-sand-200 to-brand-100",
  hideOnError = false,
}: {
  src: string;
  alt: string;
  className?: string;
  fallbackClassName?: string;
  hideOnError?: boolean;
}) {
  const [hasError, setHasError] = useState(false);

  if (hasError) {
    if (hideOnError) return null;
    return (
      <div className={`absolute inset-0 ${fallbackClassName}`}>
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img
          src="/logo.png"
          alt=""
          aria-hidden
          className="pointer-events-none absolute inset-0 m-auto h-3/4 w-auto max-w-[70%] select-none object-contain opacity-[0.18]"
        />
      </div>
    );
  }

  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={src}
      alt={alt}
      className={className}
      loading="lazy"
      onError={() => setHasError(true)}
    />
  );
}
