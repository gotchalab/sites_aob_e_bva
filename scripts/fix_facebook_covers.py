"""
Substitui coverImagePaths de artigos que apontam para URLs voláteis do
Facebook (scontent/fbcdn) ou emojis do facebook.com.

Para URLs de fbcdn: descarrega o ficheiro para
    uploads-target/<site>/images/imported/<slug>.<ext>
e atualiza "CoverImagePath" para "/uploads/<site>/images/imported/<slug>.<ext>".

Para URLs facebook.com/images/emoji.php (não são covers reais): põe NULL.

Idempotente: se o ficheiro já existe no destino, apenas atualiza a BD;
se o cover já não é Facebook, ignora.

Uso:
    python scripts/fix_facebook_covers.py
    python scripts/fix_facebook_covers.py --dry-run
"""

import argparse
import html
import os
import sys
from pathlib import Path
from urllib.parse import urlparse
from urllib.request import Request, urlopen

import psycopg2

DB = dict(
    host="localhost", port=5433,
    dbname="aob_dev", user="aob_user",
    password="hFpiPBBrnNWIMIJXbr8BVhp4",
)
UPLOADS_ROOT = Path(r"d:/PROJETOS/aob/backup-vps-2026-07-15/uploads-target")
USER_AGENT = "Mozilla/5.0 (compatible; AOB-CoverFetcher/1.0)"
FB_HOST_MARKERS = ("fbcdn", "scontent")


def is_fb_cdn(url: str) -> bool:
    return any(m in url for m in FB_HOST_MARKERS)


def is_fb_emoji(url: str) -> bool:
    return "facebook.com/images/emoji" in url


def download(url: str, dest: Path) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    req = Request(url, headers={"User-Agent": USER_AGENT, "Accept": "image/*,*/*"})
    with urlopen(req, timeout=30) as resp:
        data = resp.read()
    dest.write_bytes(data)


def guess_ext(url: str) -> str:
    path = urlparse(url).path.lower()
    for ext in (".jpg", ".jpeg", ".png", ".webp", ".gif"):
        if path.endswith(ext):
            return ext
    return ".jpg"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    conn = psycopg2.connect(**DB)
    conn.autocommit = False
    cur = conn.cursor()

    cur.execute("""
        SELECT a."Id", s."Slug", a."Slug", a."CoverImagePath"
        FROM articles a JOIN sites s ON s."Id" = a."SiteId"
        WHERE a."CoverImagePath" ILIKE %s
           OR a."CoverImagePath" ILIKE %s
           OR a."CoverImagePath" ILIKE %s
    """, ("%fbcdn%", "%scontent%", "%facebook.com%"))
    rows = cur.fetchall()

    if not rows:
        print("Nada a fazer: nenhum artigo com cover Facebook.")
        return 0

    print(f"A processar {len(rows)} artigo(s)...")
    fixed = nulled = failed = 0

    for aid, site_slug, article_slug, cover in rows:
        cover = html.unescape(cover or "").strip()
        print(f"\n[#{aid}] {site_slug}/{article_slug}")

        if is_fb_emoji(cover):
            print(f"  -> emoji facebook (nao e cover real); a definir NULL")
            if not args.dry_run:
                cur.execute('UPDATE articles SET "CoverImagePath" = NULL WHERE "Id" = %s', (aid,))
            nulled += 1
            continue

        if not is_fb_cdn(cover):
            print(f"  -> URL nao reconhecido; skip: {cover[:80]}")
            continue

        ext = guess_ext(cover)
        rel_path = f"{site_slug}/images/imported/{article_slug}{ext}"
        abs_path = UPLOADS_ROOT / rel_path
        new_cover = f"/uploads/{rel_path}"

        if abs_path.exists() and abs_path.stat().st_size > 0:
            print(f"  -> ja descarregado: {abs_path}")
        else:
            print(f"  -> a descarregar {cover[:80]}...")
            if not args.dry_run:
                try:
                    download(cover, abs_path)
                    print(f"     OK ({abs_path.stat().st_size} bytes)")
                except Exception as e:
                    print(f"     FALHOU: {e}")
                    print(f"  -> URL expirada/inacessivel; a definir NULL")
                    cur.execute('UPDATE articles SET "CoverImagePath" = NULL WHERE "Id" = %s', (aid,))
                    nulled += 1
                    continue
            else:
                print(f"     [dry-run] iria descarregar para {abs_path}")

        print(f"  -> UPDATE CoverImagePath = {new_cover}")
        if not args.dry_run:
            cur.execute('UPDATE articles SET "CoverImagePath" = %s WHERE "Id" = %s', (new_cover, aid))
        fixed += 1

    if args.dry_run:
        print("\n[dry-run] rollback")
        conn.rollback()
    else:
        conn.commit()

    print(f"\nResumo: {fixed} descarregado(s), {nulled} definido(s) NULL, {failed} falhou(aram)")
    cur.close()
    conn.close()
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
