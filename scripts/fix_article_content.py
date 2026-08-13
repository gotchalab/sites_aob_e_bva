"""
Corrige conteúdo de artigos migrados do Joomla:

  1. Substitui placeholders `{phocadownload view=file|id=N}` por um link real
     para a página do download (`/downloads/<slug>`). O `LegacyId` do download
     corresponde ao `id=N` do plugin Joomla.

  2. Remove tags `<img src="...fbcdn/scontent...">` inline no conteúdo — são
     URLs voláteis do Facebook CDN que expiram (403) e ficam quebradas.

Idempotente. Suporta --dry-run e --site (aob|bva|all).

Uso:
    python scripts/fix_article_content.py --dry-run
    python scripts/fix_article_content.py
    python scripts/fix_article_content.py --site bva
"""

import argparse
import re
import sys

import psycopg2

DB = dict(
    host="localhost", port=5433,
    dbname="aob_dev", user="aob_user",
    password="hFpiPBBrnNWIMIJXbr8BVhp4",
    client_encoding="UTF8",
)

SITES = {"aob": 1, "bva": 2}

PHOCA_RE = re.compile(r"\{phocadownload\s+view=file\|id=\s*(\d+)\s*\}", re.IGNORECASE)
FB_IMG_RE = re.compile(
    r"<img\b[^>]*\bsrc\s*=\s*['\"][^'\"]*(?:fbcdn|scontent)[^'\"]*['\"][^>]*/?\s*>",
    re.IGNORECASE,
)
# Links legados para o painel admin do Joomla:
#   <a href="administrator/index.php?option=com_phocadownload&amp;task=...&amp;id=N">
JOOMLA_ADMIN_HREF_RE = re.compile(
    r'''href\s*=\s*(['"])[^'"]*com_phocadownload[^'"]*?(?:&amp;|&)id=\s*(\d+)[^'"]*\1''',
    re.IGNORECASE,
)


def build_download_map(cur, site_id: int) -> dict[int, tuple[str, str]]:
    cur.execute(
        'SELECT "LegacyId", "Slug", "Title" FROM downloads '
        'WHERE "SiteId"=%s AND "LegacyId" IS NOT NULL',
        (site_id,),
    )
    return {legacy: (slug, title) for legacy, slug, title in cur.fetchall()}


def replace_phoca(content: str, dmap: dict[int, tuple[str, str]]) -> tuple[str, int, list[int]]:
    missing: list[int] = []
    replaced = 0

    def sub(m: re.Match) -> str:
        nonlocal replaced
        lid = int(m.group(1))
        target = dmap.get(lid)
        if not target:
            missing.append(lid)
            return m.group(0)  # deixa como estava
        slug, title = target
        replaced += 1
        safe_title = (title or slug).replace('"', "&quot;")
        return (
            f'<a href="/downloads/{slug}" class="download-link" '
            f'title="{safe_title}">📄 {safe_title}</a>'
        )

    return PHOCA_RE.sub(sub, content), replaced, missing


def remove_fb_images(content: str) -> tuple[str, int]:
    new, n = FB_IMG_RE.subn("", content)
    return new, n


def replace_joomla_admin_links(
    content: str, dmap: dict[int, tuple[str, str]]
) -> tuple[str, int, list[int]]:
    missing: list[int] = []
    replaced = 0

    def sub(m: re.Match) -> str:
        nonlocal replaced
        lid = int(m.group(2))
        target = dmap.get(lid)
        if not target:
            missing.append(lid)
            return m.group(0)
        slug, _ = target
        replaced += 1
        return f'href="/downloads/{slug}"'

    return JOOMLA_ADMIN_HREF_RE.sub(sub, content), replaced, missing


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--site", choices=("aob", "bva", "all"), default="all")
    args = ap.parse_args()

    sys.stdout.reconfigure(encoding="utf-8")

    conn = psycopg2.connect(**DB)
    conn.autocommit = False
    cur = conn.cursor()

    targets = list(SITES.items()) if args.site == "all" else [(args.site, SITES[args.site])]
    grand_updated = grand_phoca = grand_img = 0
    all_missing: dict[int, set[int]] = {}

    for site_slug, site_id in targets:
        print(f"\n=== Site: {site_slug} (id={site_id}) ===")
        dmap = build_download_map(cur, site_id)
        print(f"  {len(dmap)} downloads com LegacyId disponivel")

        cur.execute(
            'SELECT "Id", "Slug", "Content" FROM articles '
            'WHERE "SiteId"=%s AND "IsPublished"=true AND "Content" IS NOT NULL',
            (site_id,),
        )
        rows = cur.fetchall()
        touched = 0
        total_phoca = total_img = total_admin = 0
        missing_ids: set[int] = set()

        for aid, slug, content in rows:
            new_content, n_phoca, missing = replace_phoca(content, dmap)
            new_content, n_img = remove_fb_images(new_content)
            new_content, n_admin, missing_admin = replace_joomla_admin_links(new_content, dmap)
            missing_ids.update(missing)
            missing_ids.update(missing_admin)

            if new_content == content:
                continue

            touched += 1
            total_phoca += n_phoca
            total_img += n_img
            total_admin += n_admin

            action = "[dry-run]" if args.dry_run else "UPDATE"
            print(f"  {action} #{aid} {slug}: {n_phoca} phoca, {n_admin} admin-href, {n_img} <img> fb")

            if not args.dry_run:
                cur.execute('UPDATE articles SET "Content"=%s WHERE "Id"=%s', (new_content, aid))

        print(f"  Total: {touched} artigos alterados, "
              f"{total_phoca} placeholders phoca, {total_admin} hrefs admin-Joomla, "
              f"{total_img} imagens fb removidas")
        if missing_ids:
            print(f"  AVISO: {len(missing_ids)} LegacyIds referenciados sem download correspondente: "
                  f"{sorted(missing_ids)[:20]}")
            all_missing[site_id] = missing_ids

        grand_updated += touched
        grand_phoca += total_phoca
        grand_img += total_img

    if args.dry_run:
        print("\n[dry-run] rollback")
        conn.rollback()
    else:
        conn.commit()
        print("\nCommit OK")

    print(f"\nResumo global: {grand_updated} artigos alterados, "
          f"{grand_phoca} phoca convertidos, {grand_img} <img> fb removidas")

    cur.close()
    conn.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
