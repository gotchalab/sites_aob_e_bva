"use client";

import Link from "next/link";
import { useState, useEffect, useRef } from "react";
import { usePathname } from "next/navigation";
import type { MenuItem } from "@/lib/api-types";

function itemHref(item: MenuItem): string {
  if (item.url && (item.url.startsWith("/") || item.url.startsWith("http"))) return item.url;
  return "#";
}

function ChevronDown({ className = "" }: { className?: string }) {
  return (
    <svg viewBox="0 0 20 20" fill="none" className={`h-3.5 w-3.5 ${className}`} aria-hidden>
      <path d="M5 8l5 5 5-5" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function HamburgerIcon({ open }: { open: boolean }) {
  return (
    <div className="relative flex h-4 w-6 flex-col justify-between">
      <span
        className={`block h-[1.5px] w-full origin-center rounded-full bg-current transition-transform duration-300 ease-out ${
          open ? "translate-y-[7px] rotate-45" : ""
        }`}
      />
      <span
        className={`block h-[1.5px] w-full rounded-full bg-current transition-opacity duration-200 ${
          open ? "opacity-0" : "opacity-100"
        }`}
      />
      <span
        className={`block h-[1.5px] w-full origin-center rounded-full bg-current transition-transform duration-300 ease-out ${
          open ? "-translate-y-[7px] -rotate-45" : ""
        }`}
      />
    </div>
  );
}

function CloseIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" className="h-5 w-5" aria-hidden>
      <path d="M6 6l12 12M18 6l-12 12" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}

export function Nav({ items }: { items: MenuItem[] }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [openDropdown, setOpenDropdown] = useState<number | null>(null);
  const [expandedMobile, setExpandedMobile] = useState<Set<number>>(new Set());
  const pathname = usePathname() ?? "";
  const navRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setMobileOpen(false);
    setOpenDropdown(null);
  }, [pathname]);

  useEffect(() => {
    if (openDropdown === null) return;
    function onClick(e: MouseEvent) {
      if (navRef.current && !navRef.current.contains(e.target as Node)) {
        setOpenDropdown(null);
      }
    }
    function onEsc(e: KeyboardEvent) {
      if (e.key === "Escape") setOpenDropdown(null);
    }
    document.addEventListener("mousedown", onClick);
    document.addEventListener("keydown", onEsc);
    return () => {
      document.removeEventListener("mousedown", onClick);
      document.removeEventListener("keydown", onEsc);
    };
  }, [openDropdown]);

  useEffect(() => {
    if (!mobileOpen) return;
    function onEsc(e: KeyboardEvent) {
      if (e.key === "Escape") setMobileOpen(false);
    }
    document.addEventListener("keydown", onEsc);
    return () => document.removeEventListener("keydown", onEsc);
  }, [mobileOpen]);

  useEffect(() => {
    document.body.style.overflow = mobileOpen ? "hidden" : "";
    return () => { document.body.style.overflow = ""; };
  }, [mobileOpen]);

  function toggleMobileExpand(id: number) {
    setExpandedMobile((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }

  return (
    <div ref={navRef} className="flex items-center">
      <nav className="hidden lg:block" aria-label="Menu principal">
        <ul className="flex items-center gap-0.5">
          {items.map((it) => {
            const href = itemHref(it);
            const isActive = pathname === href || (href !== "/" && pathname.startsWith(href));
            const hasChildren = it.children.length > 0;
            const isOpen = openDropdown === it.id;

            if (!hasChildren) {
              return (
                <li key={it.id}>
                  <Link
                    href={href}
                    target={it.openInNewTab ? "_blank" : undefined}
                    className={`inline-flex items-center whitespace-nowrap rounded-md px-2 py-2 text-sm font-medium transition xl:px-3 ${
                      isActive
                        ? "text-brand-600"
                        : "text-ink-800 hover:bg-sand-200 hover:text-brand-600"
                    }`}
                  >
                    {it.title}
                  </Link>
                </li>
              );
            }

            return (
              <li key={it.id} className="relative">
                <button
                  type="button"
                  aria-haspopup="menu"
                  aria-expanded={isOpen}
                  onClick={() => setOpenDropdown(isOpen ? null : it.id)}
                  className={`inline-flex items-center gap-1 whitespace-nowrap rounded-md px-2 py-2 text-sm font-medium transition xl:px-3 ${
                    isActive || isOpen
                      ? "bg-sand-200 text-brand-600"
                      : "text-ink-800 hover:bg-sand-200 hover:text-brand-600"
                  }`}
                >
                  {it.title}
                  <ChevronDown className={`transition ${isOpen ? "rotate-180" : ""}`} />
                </button>
                {isOpen && (
                  <div className="absolute right-0 top-full z-50 mt-1 min-w-64 rounded-lg border border-sand-300 bg-white p-1.5 shadow-xl">
                    <ul>
                      {it.children.map((c) => (
                        <li key={c.id}>
                          <Link
                            href={itemHref(c)}
                            target={c.openInNewTab ? "_blank" : undefined}
                            className="block rounded-md px-3 py-2 text-sm text-ink-800 transition hover:bg-sand-100 hover:text-brand-600"
                            onClick={() => setOpenDropdown(null)}
                          >
                            {c.title}
                          </Link>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      </nav>

      <button
        type="button"
        aria-label={mobileOpen ? "Fechar menu" : "Abrir menu"}
        aria-expanded={mobileOpen}
        onClick={() => setMobileOpen((v) => !v)}
        className="relative z-[70] flex h-10 w-10 items-center justify-center rounded-full text-ink-800 transition hover:bg-sand-200 lg:hidden"
      >
        <HamburgerIcon open={mobileOpen} />
      </button>

      <div
        onClick={() => setMobileOpen(false)}
        aria-hidden
        className={`fixed inset-0 z-[55] bg-ink-900/30 backdrop-blur-[3px] transition-opacity duration-300 lg:hidden ${
          mobileOpen ? "opacity-100" : "pointer-events-none opacity-0"
        }`}
      />

      <aside
        role="dialog"
        aria-modal="true"
        aria-label="Menu de navegação"
        aria-hidden={!mobileOpen}
        className={`fixed right-0 top-0 z-[60] flex h-[100dvh] w-[86vw] max-w-sm flex-col border-l border-sand-300/60 bg-white/85 shadow-2xl backdrop-blur-2xl transition-transform duration-500 ease-[cubic-bezier(0.32,0.72,0,1)] lg:hidden ${
          mobileOpen ? "translate-x-0" : "translate-x-full"
        }`}
      >
        <div className="flex items-center justify-between px-5 pb-3 pt-[max(1rem,env(safe-area-inset-top))]">
          <span className="text-xs font-semibold uppercase tracking-[0.14em] text-ink-500">
            Menu
          </span>
          <button
            type="button"
            aria-label="Fechar menu"
            onClick={() => setMobileOpen(false)}
            className="flex h-9 w-9 items-center justify-center rounded-full text-ink-700 transition hover:bg-sand-200 active:scale-95"
          >
            <CloseIcon />
          </button>
        </div>

        <nav className="flex-1 overflow-y-auto overscroll-contain px-5 pb-6" aria-label="Menu mobile">
          <ul className="flex flex-col divide-y divide-sand-200/70">
            {items.map((it, idx) => {
              const href = itemHref(it);
              const isActive = pathname === href || (href !== "/" && pathname.startsWith(href));
              const hasChildren = it.children.length > 0;
              const isExpanded = expandedMobile.has(it.id);
              const enterStyle = mobileOpen
                ? { opacity: 1, transform: "translateY(0)", transitionDelay: `${120 + idx * 40}ms` }
                : { opacity: 0, transform: "translateY(8px)", transitionDelay: "0ms" };

              return (
                <li
                  key={it.id}
                  className="transition-all duration-500 ease-out"
                  style={enterStyle}
                >
                  <div className="flex items-center">
                    <Link
                      href={href}
                      target={it.openInNewTab ? "_blank" : undefined}
                      className={`flex-1 py-4 text-[1.35rem] font-medium tracking-tight transition-colors ${
                        isActive ? "text-brand-600" : "text-ink-900 hover:text-brand-600"
                      }`}
                    >
                      {it.title}
                    </Link>
                    {hasChildren && (
                      <button
                        type="button"
                        aria-label={isExpanded ? "Colapsar" : "Expandir"}
                        aria-expanded={isExpanded}
                        onClick={() => toggleMobileExpand(it.id)}
                        className="ml-2 flex h-9 w-9 items-center justify-center rounded-full text-ink-500 transition hover:bg-sand-200 hover:text-ink-800 active:scale-95"
                      >
                        <ChevronDown
                          className={`transition-transform duration-300 ${isExpanded ? "rotate-180 text-brand-600" : ""}`}
                        />
                      </button>
                    )}
                  </div>
                  {hasChildren && (
                    <div
                      className={`grid transition-[grid-template-rows,opacity] duration-300 ease-out ${
                        isExpanded ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"
                      }`}
                    >
                      <div className="overflow-hidden">
                        <ul className="mb-3 ml-1 flex flex-col gap-0.5 border-l border-sand-300/70 pl-4">
                          {it.children.map((c) => {
                            const cHref = itemHref(c);
                            const cActive = pathname === cHref || (cHref !== "/" && pathname.startsWith(cHref));
                            return (
                              <li key={c.id}>
                                <Link
                                  href={cHref}
                                  target={c.openInNewTab ? "_blank" : undefined}
                                  className={`block py-2 text-[1rem] transition-colors ${
                                    cActive ? "text-brand-600" : "text-ink-600 hover:text-brand-600"
                                  }`}
                                >
                                  {c.title}
                                </Link>
                              </li>
                            );
                          })}
                        </ul>
                      </div>
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        </nav>

        <div className="border-t border-sand-200/70 bg-white/40 px-5 pb-[max(1rem,env(safe-area-inset-bottom))] pt-4 backdrop-blur-xl">
          <Link
            href="/contacto"
            className="flex w-full items-center justify-center gap-2 rounded-full bg-brand-500 px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-brand-600 active:scale-[0.98]"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" className="h-4 w-4">
              <rect x="3" y="5" width="18" height="14" rx="2" />
              <path d="m3 7 9 6 9-6" />
            </svg>
            Contactar
          </Link>
        </div>
      </aside>
    </div>
  );
}
