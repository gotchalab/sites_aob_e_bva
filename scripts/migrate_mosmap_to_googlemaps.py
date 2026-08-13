"""
Converte {mosmap width='...'|height='...'|lat='...'|lon='...'|text='...'} legacy do Joomla
em {googleMaps lat=... long=... label='...'} do novo expander.

Faz strip do HTML embutido nos valores dos atributos (Joomla antigo permitia <span>...</span>
dentro dos campos), e guarda backup de cada linha alterada em ficheiro JSON.

Uso:
    python migrate_mosmap_to_googlemaps.py --dry-run
    python migrate_mosmap_to_googlemaps.py --commit
"""

import argparse
import html
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

import psycopg2

DB = dict(
    host="localhost", port=5433, dbname="aob_dev",
    user="aob_user", password="hFpiPBBrnNWIMIJXbr8BVhp4",
)

MOSMAP_RX = re.compile(r"\{mosmap\s+(?P<attrs>[^}]+)\}", re.IGNORECASE)
ATTR_RX = re.compile(r"(?P<k>\w+)\s*=\s*(?:'(?P<v1>[^']*)'|\"(?P<v2>[^\"]*)\"|(?P<v3>[^|}\s]+))")
STRIP_TAGS_RX = re.compile(r"<[^>]+>")


def clean(value: str) -> str:
    if value is None:
        return ""
    stripped = STRIP_TAGS_RX.sub("", value)
    return html.unescape(stripped).strip()


def convert(match: re.Match) -> str:
    attrs = {}
    for m in ATTR_RX.finditer(match.group("attrs")):
        v = m.group("v1") or m.group("v2") or m.group("v3") or ""
        attrs[m.group("k").lower()] = clean(v)

    lat = attrs.get("lat")
    lon = attrs.get("lon")
    if not lat or not lon:
        return match.group(0)  # deixa como está — expander mostrará placeholder

    label = attrs.get("text", "").replace("'", "’")
    label_part = f" label='{label}'" if label else ""
    return f"{{googleMaps lat={lat} long={lon}{label_part}}}"


def process(content: str) -> tuple[str, int]:
    count = 0

    def repl(m):
        nonlocal count
        new = convert(m)
        if new != m.group(0):
            count += 1
        return new

    new_content = MOSMAP_RX.sub(repl, content)
    return new_content, count


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--commit", action="store_true", help="Aplicar UPDATE (sem isto é dry-run)")
    args = parser.parse_args()

    conn = psycopg2.connect(**DB)
    conn.autocommit = False
    try:
        with conn.cursor() as cur:
            cur.execute("""
                SELECT "Id", "Title", "Content"
                FROM articles
                WHERE "Content" ILIKE '%{mosmap%'
                ORDER BY "Id"
            """)
            rows = cur.fetchall()

        print(f"Encontrados {len(rows)} artigos com {{mosmap...}}\n")

        backup = []
        updates = []
        for aid, title, content in rows:
            new_content, n = process(content)
            print(f"-- Artigo {aid}: {title}")
            print(f"   substituicoes: {n}")
            if n == 0:
                print("   (nenhuma alteracao)")
                continue
            # amostra do primeiro trecho antes/depois
            first_old = MOSMAP_RX.search(content)
            if first_old:
                snippet_old = first_old.group(0)[:180]
                snippet_new = convert(first_old)[:180]
                print(f"   antes: {snippet_old}")
                print(f"   depois: {snippet_new}")
            backup.append({"id": aid, "title": title, "content": content})
            updates.append((new_content, aid))
            print()

        if not updates:
            print("Nada a fazer.")
            return

        if not args.commit:
            print(f"\n[DRY-RUN] {len(updates)} artigos seriam actualizados. Correr com --commit para aplicar.")
            return

        # backup em disco
        ts = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
        backup_path = Path(__file__).parent / f"backup_mosmap_{ts}.json"
        backup_path.write_text(json.dumps(backup, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"Backup guardado em {backup_path}")

        with conn.cursor() as cur:
            for new_content, aid in updates:
                cur.execute('UPDATE articles SET "Content" = %s WHERE "Id" = %s', (new_content, aid))
        conn.commit()
        print(f"\n[OK] {len(updates)} artigos actualizados.")
    finally:
        conn.close()


if __name__ == "__main__":
    sys.exit(main() or 0)
