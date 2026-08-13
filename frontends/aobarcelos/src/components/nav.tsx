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
    <div className="relative flex h-5 w-6 flex-col justify-center">
      <span className={`absolute h-0.5 w-full bg-current transition ${open ? "top-1/2 -translate-y-1/2 rotate-45" : "top-0"}`} />
      <span className={`absolute top-1/2 h-0.5 w-full -translate-y-1/2 bg-current transition ${open ? "opacity-0" : ""}`} />
      <span className={`absolute h-0.5 w-full bg-current transition ${open ? "top-1/2 -translate-y-1/2 -rotate-45" : "bottom-0"}`} />
    </div>
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
                        ? "text-brand-700"
                        : "text-earth-800 hover:bg-cream-300/50 hover:text-brand-700"
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
                      ? "bg-cream-300/60 text-brand-700"
                      : "text-earth-800 hover:bg-cream-300/50 hover:text-brand-700"
                  }`}
                >
                  {it.title}
                  <ChevronDown className={`transition ${isOpen ? "rotate-180" : ""}`} />
                </button>
                {isOpen && (
                  <div className="absolute right-0 top-full z-50 mt-1 min-w-64 rounded-lg border border-gold-500/40 bg-white p-1.5 shadow-xl">
                    <ul>
                      {it.children.map((c) => (
                        <li key={c.id}>
                          <Link
                            href={itemHref(c)}
                            target={c.openInNewTab ? "_blank" : undefined}
                            className="block rounded-md px-3 py-2 text-sm text-earth-800 transition hover:bg-cream-100 hover:text-brand-700"
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
        aria-label="Abrir menu"
        aria-expanded={mobileOpen}
        onClick={() => setMobileOpen((v) => !v)}
        className="flex h-10 w-10 items-center justify-center rounded-md text-earth-800 hover:bg-cream-300/60 lg:hidden"
      >
        <HamburgerIcon open={mobileOpen} />
      </button>

      {mobileOpen && (
        <div
          className="fixed inset-0 top-16 z-40 bg-earth-900/40 backdrop-blur-sm lg:hidden"
          onClick={() => setMobileOpen(false)}
          aria-hidden
        />
      )}

      <div
        className={`fixed inset-x-0 top-16 z-50 max-h-[calc(100vh-4rem)] overflow-y-auto border-t border-gold-500/40 bg-cream-100 shadow-2xl transition-transform lg:hidden ${
          mobileOpen ? "translate-y-0" : "-translate-y-[110%]"
        }`}
      >
        <nav className="mx-auto max-w-7xl px-4 py-4" aria-label="Menu mobile">
          <ul className="flex flex-col gap-1">
            {items.map((it) => {
              const href = itemHref(it);
              const isActive = pathname === href || (href !== "/" && pathname.startsWith(href));
              const hasChildren = it.children.length > 0;
              const isExpanded = expandedMobile.has(it.id);

              return (
                <li key={it.id}>
                  <div className="flex items-stretch">
                    <Link
                      href={href}
                      target={it.openInNewTab ? "_blank" : undefined}
                      className={`flex-1 rounded-l-md px-4 py-3 text-base font-medium transition ${
                        isActive
                          ? "bg-brand-500 text-white"
                          : "bg-white text-earth-800 hover:bg-cream-200 hover:text-brand-700"
                      } ${hasChildren ? "" : "rounded-r-md"}`}
                    >
                      {it.title}
                    </Link>
                    {hasChildren && (
                      <button
                        type="button"
                        aria-label={isExpanded ? "Colapsar" : "Expandir"}
                        aria-expanded={isExpanded}
                        onClick={() => toggleMobileExpand(it.id)}
                        className={`flex w-12 items-center justify-center rounded-r-md border-l border-cream-200 transition ${
                          isActive ? "bg-brand-500 text-white" : "bg-white text-earth-700 hover:bg-cream-200"
                        }`}
                      >
                        <ChevronDown className={`transition ${isExpanded ? "rotate-180" : ""}`} />
                      </button>
                    )}
                  </div>
                  {hasChildren && isExpanded && (
                    <ul className="ml-3 mt-1 flex flex-col gap-0.5 border-l-2 border-gold-500/40 pl-3">
                      {it.children.map((c) => (
                        <li key={c.id}>
                          <Link
                            href={itemHref(c)}
                            target={c.openInNewTab ? "_blank" : undefined}
                            className="block rounded-md px-3 py-2 text-sm text-earth-800 transition hover:bg-cream-200 hover:text-brand-700"
                          >
                            {c.title}
                          </Link>
                        </li>
                      ))}
                    </ul>
                  )}
                </li>
              );
            })}
            <li className="mt-3 border-t border-gold-500/40 pt-3">
              <Link
                href="/socio"
                className="block rounded-md bg-brand-500 px-4 py-3 text-center text-base font-medium text-white transition hover:bg-brand-600"
              >
                Área de sócio
              </Link>
            </li>
          </ul>
        </nav>
      </div>
    </div>
  );
}
