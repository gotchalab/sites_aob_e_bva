"""Auditoria única de conteúdo BVA — não persiste nada."""
import psycopg2
import re

c = psycopg2.connect(host="localhost", port=5433, dbname="aob_dev",
                     user="aob_user", password="hFpiPBBrnNWIMIJXbr8BVhp4")
cur = c.cursor()

cur.execute('SELECT "Id", "Slug", "Content" FROM articles '
            'WHERE "SiteId"=2 AND "IsPublished"=true')
rows = cur.fetchall()
print(f"{len(rows)} artigos publicados no BVA\n")

phoca_ids, fb_ids, legacy_ids = [], [], []
phoca_ids_set, fb_ids_set, legacy_ids_set = set(), set(), set()

LEGACY = re.compile(r"bvaportugal\.pt|bva-portugal\.pt|bva\.pt/|aobarcelos\.com|joomla", re.I)

for aid, slug, content in rows:
    if not content:
        continue
    if "phocadownload" in content or "{phoca" in content:
        phoca_ids.append((aid, slug))
    if "facebook.com" in content or "fbcdn" in content or "scontent" in content:
        fb_ids.append((aid, slug))
    if LEGACY.search(content):
        legacy_ids.append((aid, slug))

def show(label, items, limit=10):
    print(f"[{len(items):>3}] {label}")
    for aid, slug in items[:limit]:
        print(f"       #{aid} {slug}")
    if len(items) > limit:
        print(f"       ... (+{len(items)-limit} mais)")
    print()

show("com {phocadownload ...} literal no HTML", phoca_ids)
show("com link para facebook.com/fbcdn/scontent no HTML", fb_ids)
show("com referencia a dominios legados", legacy_ids)

# Amostra: para um artigo com phoca, mostrar as ocorrências
if phoca_ids:
    aid, slug = phoca_ids[0]
    cur.execute('SELECT "Content" FROM articles WHERE "Id"=%s', (aid,))
    content = cur.fetchone()[0]
    matches = re.findall(r"\{phoca[^}]+\}", content)
    print(f"Amostra de placeholders em #{aid} {slug}:")
    for m in matches[:5]:
        print(f"  {m}")

c.close()
