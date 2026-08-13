"use client";

import Image, { type ImageProps } from "next/image";
import { useState } from "react";

/**
 * Wrapper para next/image que:
 *  - Passa `unoptimized` para URLs externas não whitelisted (ex.: Facebook CDN),
 *    que expiram/quebram por não estarem em `remotePatterns`.
 *  - Em caso de falha (URL expirada, 403, etc.) mostra o logo em marca-d'água
 *    em vez da imagem partida.
 */
export function SafeImage(props: ImageProps) {
  const [hasError, setHasError] = useState(false);
  const src = typeof props.src === "string" ? props.src : "";
  const isExternal = /^https?:\/\//i.test(src);
  const isKnownHost = /^https?:\/\/(localhost|aobarcelos\.pt|.*\.aobarcelos\.pt)/i.test(src);
  const shouldOptimize = !isExternal || isKnownHost;

  if (hasError) {
    return (
      <div className="absolute inset-0 bg-gradient-to-br from-sand-100 via-sand-200 to-brand-100">
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
    <Image
      {...props}
      unoptimized={!shouldOptimize}
      onError={() => setHasError(true)}
    />
  );
}
