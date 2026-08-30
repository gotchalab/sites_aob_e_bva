#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import sys, io
# stdout forcado para UTF-8 quando corrido como script (suporta o ● do systemctl).
if __name__ == "__main__" and hasattr(sys.stdout, "buffer"):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
"""
AOB Deploy Inicial - Bootstrap-only, ja NAO precisa correr em producao.

>>> ATENCAO <<<
    Este script prepara o VPS do zero: instala dependencias, cria users,
    e restaura a BD dev na BD prod. E DESTRUTIVO se corrido contra um
    VPS ja em producao (o target 'db' apaga aob_prod e restaura da dev).

    Ja foi corrido em 2026-08 no VPS actual (51.83.40.43). NAO CORRER.

    Se precisares de reprovisionar do zero (novo VPS, restore de disaster
    recovery): corres explicitamente com AOB_ALLOW_BOOTSTRAP=1 na env, senao
    o script recusa por seguranca.

Uso (bootstrap real de VPS novo):
    AOB_ALLOW_BOOTSTRAP=1 python deploy_inicial.py [target [target ...]]

Targets:
    setup       - instala dependencias no VPS (dotnet 10, next@15.5.4, users, dirs, PG role/db)
    db          - faz pg_dump da BD local e restaura em aob_prod (DESTRUTIVO)
    all         - setup + db

Depois de correr este script uma vez, TODO o trabalho corrente e feito
com `deploy.py` (nunca voltar aqui excepto para reprovisionar).

Pre-requisitos:
    - AOB_SSH_HOST, AOB_SSH_USER (default 51.83.40.43 / debian)
    - chave SSH em ~/.ssh/id_ed25519 (ou AOB_SSH_KEY)
    - pg_dump instalado localmente (procura em C:/Program Files/PostgreSQL/*/bin/)
    - paramiko

Depois do primeiro bootstrap (manual):
    1. Criar /etc/aob/smtp.env (fonte unica da SMTP key, partilhado por
       aob-api e aob-admin), /etc/aob/api.env, /etc/aob/admin.env a partir
       dos samples em infra/deploy/env-samples/
    2. certbot --nginx -d aobarcelos.pt -d www.aobarcelos.pt \\
         -d bva-p.aobarcelos.pt -d api.aobarcelos.pt -d admin.aobarcelos.pt
    3. Correr `python deploy.py infra api admin aobarcelos bva services`
       para colocar o codigo actual em producao (migrations correm dentro de api).
"""

import os
import subprocess
import tempfile
from pathlib import Path

from _common import (
    AOB_PG_LOCAL, AOB_PG_PASS, DOTNET_CHANNEL, NEXT_VERSION,
    connect, exec_sql, run, sftp_file, sudo,
    warn_if_not_on_main,
)


# ---------------------------------------------------------------------------
# Targets
# ---------------------------------------------------------------------------

def setup(ssh) -> None:
    """Instala dependencias no VPS: dotnet 10, next@15.5.4, users, dirs, PG role."""
    print("\n[setup] .NET 10 em /opt/dotnet")
    dotnet_ok = run(ssh, "test -x /opt/dotnet/dotnet && echo yes || echo no", check=False).strip()
    if dotnet_ok != "yes":
        sudo(ssh,
            "curl -fsSL https://dot.net/v1/dotnet-install.sh | "
            f"bash -s -- --runtime aspnetcore --channel {DOTNET_CHANNEL} --install-dir /opt/dotnet"
        )
        sudo(ssh, "chmod +x /opt/dotnet/dotnet")
    else:
        print("  ja instalado")

    print("\n[setup] next@" + NEXT_VERSION)
    next_ver = run(ssh, "next --version 2>/dev/null || echo none", check=False).strip()
    if NEXT_VERSION not in next_ver:
        sudo(ssh, f"npm install -g next@{NEXT_VERSION}")
    else:
        print(f"  ja instalado ({next_ver})")

    print("\n[setup] Utilizadores de servico")
    for u in ("aob-api", "aob-admin", "aob-web"):
        sudo(ssh, f"id -u {u} >/dev/null 2>&1 || useradd -r -M -s /usr/sbin/nologin {u}", check=False)
    # aob-web tem de ler /var/www/uploads via symlink public/uploads para o
    # Next.js image optimizer funcionar (Next 15 le URLs /uploads/... do FS).
    # aob-admin tambem tem de escrever (CKEditor download picker no backoffice).
    sudo(ssh, "usermod -aG www-data aob-web")
    sudo(ssh, "usermod -aG www-data aob-admin")

    print("\n[setup] Diretorios")
    for cmd in (
        "install -d -o aob-api   -g aob-api   -m 0755 /opt/aob/api",
        "install -d -o aob-admin -g aob-admin -m 0755 /opt/aob/admin",
        "install -d -o aob-web   -g aob-web   -m 0755 /opt/aob/aobarcelos",
        "install -d -o aob-web   -g aob-web   -m 0755 /opt/aob/bva-portugal",
        # 2770 = setgid + group rwx. setgid faz novos subdirs herdarem grupo
        # www-data (para aob-web ler via symlink public/uploads); group write
        # permite ao aob-admin (CKEditor picker) e ao aob-api criar ficheiros
        # no mesmo tree.
        "install -d -o aob-api   -g www-data  -m 2770 /var/www/uploads",
        "install -d -o root      -g root      -m 0755 /etc/aob",
    ):
        sudo(ssh, cmd)
    # Reaplica setgid + group + mode em subdirs/ficheiros ja existentes (idempotente).
    sudo(ssh, "find /var/www/uploads -type d -exec chgrp www-data {} + -exec chmod 2770 {} +")
    sudo(ssh, "find /var/www/uploads -type f -exec chmod 0660 {} +")

    print("\n[setup] PostgreSQL - role aobapp + BD aob_prod")
    # Em PG 13, CREATE ROLE dentro de DO $$ precisa de EXECUTE.
    # A password nao contem aspas, logo o escaping e trivial.
    pg_pass_esc = AOB_PG_PASS.replace("'", "''")
    exec_sql(ssh, f"""
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aobapp') THEN
    EXECUTE 'CREATE ROLE aobapp WITH LOGIN PASSWORD ''{pg_pass_esc}''';
  END IF;
END $$;
ALTER ROLE aobapp WITH LOGIN PASSWORD '{pg_pass_esc}';
""")
    # CREATE DATABASE nao pode correr dentro de transacao - usar shell condicional
    run(ssh, "sudo -u postgres psql -l | grep -q aob_prod "
             "|| sudo -u postgres createdb -O aobapp aob_prod")

    print("\n[setup] Nginx")
    sudo(ssh, "which nginx >/dev/null 2>&1 || (apt-get update && apt-get -y install nginx)", check=False)
    sudo(ssh, "systemctl enable nginx", check=False)

    print("\n[setup] Concluido.")


def restore_db(ssh) -> None:
    """DESTRUTIVO: faz pg_dump da BD local e restaura no VPS (--clean --if-exists)."""
    print("\n[db] pg_dump local")

    pg_dump = ""
    for v in ("17", "16", "15"):
        candidate = Path(rf"C:\Program Files\PostgreSQL\{v}\bin\pg_dump.exe")
        if candidate.exists():
            pg_dump = str(candidate)
            break
    if not pg_dump:
        pg_dump = "pg_dump"

    dump_file = Path(tempfile.mktemp(suffix=".sql"))
    result = subprocess.run(
        [pg_dump, "--no-owner", "--no-privileges", "--clean", "--if-exists", "-Fp",
         "-d", AOB_PG_LOCAL, "-f", str(dump_file)],
        capture_output=True, text=True
    )
    if result.returncode != 0:
        print("[erro]", result.stderr)
        raise RuntimeError("pg_dump falhou")

    mb = dump_file.stat().st_size / 1_048_576
    remote_dump = "/tmp/aob_restore.sql"
    print(f"  upload dump {mb:.1f} MB -> VPS")
    sftp_file(ssh, dump_file, remote_dump)
    dump_file.unlink(missing_ok=True)

    print("  restaurar em aob_prod")
    run(ssh, f"sudo -u postgres psql -d aob_prod -f {remote_dump}")
    run(ssh, f"sudo rm -f {remote_dump}")
    print("[db] Concluido.")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

TARGETS = {
    "setup": setup,
    "db":    restore_db,
}

ALL_ORDER = ["setup", "db"]


def _refuse_unless_allowed() -> None:
    """Recusa correr sem AOB_ALLOW_BOOTSTRAP=1 na env (guard contra dedos travessos)."""
    if os.environ.get("AOB_ALLOW_BOOTSTRAP") in ("1", "true", "yes"):
        return
    banner = "=" * 60
    print(banner)
    print("  RECUSA: deploy_inicial.py e bootstrap-only.")
    print("  Ja foi corrido no VPS actual. Correr de novo iria:")
    print("    - reinstalar dotnet/next (idempotente, mas inutil)")
    print("    - APAGAR aob_prod e restaurar da BD dev (DESTRUTIVO)")
    print()
    print("  Se e mesmo bootstrap de VPS novo, define AOB_ALLOW_BOOTSTRAP=1:")
    print("    AOB_ALLOW_BOOTSTRAP=1 python deploy_inicial.py ...")
    print(banner)
    sys.exit(1)


def main() -> None:
    _refuse_unless_allowed()

    args = sys.argv[1:] if len(sys.argv) > 1 else ["all"]

    if args == ["all"]:
        order = ALL_ORDER
    else:
        unknown = [a for a in args if a not in TARGETS]
        if unknown:
            print(f"Targets desconhecidos: {unknown}")
            print("Validos:", ", ".join(TARGETS) + ", all")
            sys.exit(1)
        order = args

    warn_if_not_on_main()

    ssh = connect()
    try:
        for t in order:
            print(f"\n{'='*52}")
            print(f"  TARGET: {t.upper()}")
            print(f"{'='*52}")
            TARGETS[t](ssh)
    finally:
        ssh.close()

    print("\nBootstrap concluido.")
    print("\nProximos passos manuais no VPS:")
    print("  1. Criar /etc/aob/smtp.env, /etc/aob/api.env, /etc/aob/admin.env")
    print("     (ver infra/deploy/env-samples/*.sample). smtp.env e a fonte")
    print("     unica da Brevo SMTP key, partilhada por aob-api e aob-admin.")
    print("  2. certbot --nginx -d aobarcelos.pt -d www.aobarcelos.pt \\")
    print("       -d bva-p.aobarcelos.pt -d api.aobarcelos.pt -d admin.aobarcelos.pt")
    print("  3. Deploy do codigo actual:")
    print("     python deploy.py infra api admin aobarcelos bva services")


if __name__ == "__main__":
    main()
