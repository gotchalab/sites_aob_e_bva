#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import sys, io
# stdout forcado para UTF-8 quando corrido como script (suporta o ● do systemctl).
# Nao aplicar se estiver a ser importado (evita fechar o stdout de outro modulo).
if __name__ == "__main__" and hasattr(sys.stdout, "buffer"):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
"""
AOB Deploy - fluxo corrente (Windows-compatible, sem rsync).

Este script cobre APENAS o deploy do dia-a-dia contra um VPS ja
provisionado. Para bootstrap de um VPS novo, ver `deploy_inicial.py`.

Uso:
    python deploy.py [target [target ...]]

Targets:
    api         - build + deploy AOB.Api + AOB.Migrator; corre migrations
                  (AOB.Migrator db-update) automaticamente antes do restart
    admin       - build + deploy AOB.Admin
    aobarcelos  - deploy .next/ do frontend aobarcelos.pt
    bva         - deploy .next/ do frontend bva-p.aobarcelos.pt
    uploads     - sincroniza /uploads/ local para /var/www/uploads/ (raro;
                  correr so quando se semeou algo local que nao passa pela
                  BD, ex: fixtures de imagens)
    migrations  - corre AOB.Migrator db-update sozinho (raro; api ja o faz)
    infra       - copia nginx.conf + systemd units + daemon-reload
    services    - para Apache2, arranca nginx e todos os servicos AOB
    all         - infra + api + admin + uploads + aobarcelos + bva + services

Deploy corrente tipico:
    python deploy.py infra api admin aobarcelos bva services

Env opcional:
    AOB_SSH_HOST           - default: 51.83.40.43
    AOB_SSH_USER           - default: debian
    AOB_SSH_KEY            - caminho da chave SSH (default: ~/.ssh/id_ed25519)
    AOB_SKIP_MIGRATIONS=1  - salta AOB.Migrator db-update em deploy api
                             (raro; documenta o porque num commit se usares)
    AOB_STRICT_BRANCH=1    - aborta se nao estiveres em 'main'
"""

import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from _common import (
    BACKEND, FRONTENDS, INFRA, VPS_USER,
    connect, run, sudo, sftp_bytes, upload_file, upload_tar,
    warn_if_not_on_main, _walk,
)


# ---------------------------------------------------------------------------
# Helpers locais
# ---------------------------------------------------------------------------

def _dotnet_publish(project_name: str, out_dir: Path) -> None:
    subprocess.run(
        ["dotnet", "publish",
         str(BACKEND / "src" / project_name / f"{project_name}.csproj"),
         "-c", "Release", "-o", str(out_dir),
         "--nologo", "--self-contained", "false"],
        check=True
    )


# ---------------------------------------------------------------------------
# Targets
# ---------------------------------------------------------------------------

def run_migrations(ssh) -> None:
    """Corre EF Core `AOB.Migrator db-update` no VPS. Idempotente.

    Usa o mesmo user (aob-api) e EnvironmentFile (/etc/aob/api.env) do
    servico aob-api, para partilhar ConnectionStrings__Default. Se nao
    houver migrations pendentes, o Migrator imprime uma linha e sai 0.

    Set AOB_SKIP_MIGRATIONS=1 para saltar (raramente util; documenta o
    porque na mensagem de commit se o fizeres).
    """
    if os.environ.get("AOB_SKIP_MIGRATIONS") in ("1", "true", "yes"):
        print("\n[migrations] AOB_SKIP_MIGRATIONS activo - a saltar db-update.")
        return

    print("\n[migrations] AOB.Migrator db-update (root, /etc/aob/api.env)")
    # Corre como root porque /etc/aob/api.env e 0640 (root:root ou
    # root:aob-api) - o systemd EnvironmentFile do aob-api.service e lido
    # pelo systemd como root antes de descer para aob-api, mas nos aqui
    # precisamos de fazer o source manualmente e o aob-api nao tem read
    # directo ao ficheiro. O Migrator so faz DDL EF Core e as conexoes
    # usam 'aobapp' via ConnectionStrings__Default, portanto o UID do
    # processo e irrelevante para o efeito.
    #
    # 'set -a' exporta automaticamente as vars definidas pelo source do
    # envfile; 'set +a' desliga. cwd tem de ser /opt/aob/api para o
    # AppDbContext carregar appsettings.json.
    inner = (
        "set -a && . /etc/aob/api.env && set +a && "
        "cd /opt/aob/api && "
        "/opt/dotnet/dotnet AOB.Migrator.dll db-update"
    )
    sudo(ssh, inner)


def deploy_api(ssh) -> None:
    """dotnet publish AOB.Api + AOB.Migrator -> /opt/aob/api, aplica
    migrations pendentes, depois restart do aob-api.

    A ordem (upload -> migrations -> restart) e deliberada:
      - upload primeiro para ter o Migrator novo no VPS
      - migrations depois, com o Migrator novo mas antes do restart
      - se migrations falharem, o restart nao acontece e a API antiga
        continua a servir (rollback natural)
    """
    print("\n[api] dotnet publish AOB.Api")
    out = Path(tempfile.mkdtemp())
    _dotnet_publish("AOB.Api", out)

    print("[api] dotnet publish AOB.Migrator (mesmo diretorio)")
    _dotnet_publish("AOB.Migrator", out)

    print("[api] Upload -> /opt/aob/api")
    upload_tar(ssh, out, "/opt/aob/api")
    sudo(ssh, "chown -R aob-api:aob-api /opt/aob/api")

    run_migrations(ssh)

    print("[api] Restart aob-api")
    sudo(ssh, "systemctl restart aob-api && systemctl status --no-pager aob-api | head -6")
    shutil.rmtree(out, ignore_errors=True)


def deploy_admin(ssh) -> None:
    """dotnet publish AOB.Admin -> /opt/aob/admin + restart."""
    print("\n[admin] dotnet publish AOB.Admin")
    out = Path(tempfile.mkdtemp())
    _dotnet_publish("AOB.Admin", out)

    print("[admin] Upload -> /opt/aob/admin")
    upload_tar(ssh, out, "/opt/aob/admin")
    sudo(ssh, "chown -R aob-admin:aob-admin /opt/aob/admin")
    sudo(ssh, "systemctl restart aob-admin && systemctl status --no-pager aob-admin | head -6")
    shutil.rmtree(out, ignore_errors=True)


def deploy_frontend(ssh, name: str, local_dir: Path,
                    remote_dir: str, service: str) -> None:
    """Upload .next/ + public/ + package.json + next.config.mjs; sem node_modules."""
    print(f"\n[{name}] Upload .next/ -> {remote_dir}")

    next_dir = local_dir / ".next"
    if not next_dir.exists():
        raise FileNotFoundError(
            f"Build nao encontrado: {next_dir}\n"
            "Corre primeiro: npm run build  (o postbuild patch-build.mjs valida envs)"
        )

    # Permitir upload como debian, depois chown
    sudo(ssh, f"mkdir -p {remote_dir} && chown {VPS_USER}:{VPS_USER} {remote_dir}")

    upload_tar(ssh, next_dir,  f"{remote_dir}/.next",  exclude=["cache"])
    upload_tar(ssh, local_dir / "public", f"{remote_dir}/public") \
        if (local_dir / "public").exists() else None

    for fname in ("package.json", "next.config.mjs"):
        fpath = local_dir / fname
        if fpath.exists():
            upload_file(ssh, fpath, f"{remote_dir}/{fname}")

    sudo(ssh, f"chown -R aob-web:aob-web {remote_dir}")
    # public/uploads -> /var/www/uploads. Sem isto, o Next.js image optimizer
    # devolve 400 "received null" para URLs relativos /uploads/... porque
    # tenta le-los do FS local em public/ (nao via HTTP). Requer aob-web
    # no grupo www-data (feito no bootstrap inicial).
    sudo(ssh, f"mkdir -p {remote_dir}/public && "
              f"ln -sfn /var/www/uploads {remote_dir}/public/uploads && "
              f"chown -h aob-web:aob-web {remote_dir}/public/uploads")
    sudo(ssh, f"systemctl restart {service} && systemctl status --no-pager {service} | head -6")
    print(f"[{name}] Concluido.")


def deploy_infra(ssh) -> None:
    """Copia infra/ para VPS, instala nginx configs e systemd units."""
    print("\n[infra] Upload infra/ -> /opt/aob/infra")
    sudo(ssh, f"mkdir -p /opt/aob/infra && chown {VPS_USER}:{VPS_USER} /opt/aob/infra")
    upload_tar(ssh, INFRA, "/opt/aob/infra")

    print("[infra] systemd units")
    sudo(ssh, "cp /opt/aob/infra/systemd/*.service /etc/systemd/system/ 2>/dev/null || true",
         check=False)
    sudo(ssh, "systemctl daemon-reload")

    print("[infra] nginx")
    sudo(ssh, "mkdir -p /etc/nginx/snippets /etc/nginx/conf.d")
    # Vhosts
    sudo(ssh, "for f in /opt/aob/infra/nginx/*.pt.conf; do "
              "cp \"$f\" /etc/nginx/sites-available/; done", check=False)
    # Snippet server-level
    sudo(ssh, "cp /opt/aob/infra/nginx/_common.conf /etc/nginx/snippets/aob-common.conf",
         check=False)
    # Zonas de rate limit (http-level)
    sudo(ssh, "cp /opt/aob/infra/nginx/aob-zones.conf /etc/nginx/conf.d/aob-zones.conf",
         check=False)
    # Mapas de redirect legacy (URLs antigas Joomla -> URLs actuais)
    for mf in ("redirects.aob.map", "redirects.bva.map"):
        sudo(ssh, f"test -f /opt/aob/infra/nginx/{mf} && "
                  f"cp /opt/aob/infra/nginx/{mf} /etc/nginx/ || true", check=False)
    # Pagina de manutencao servida como error_page 503 do vhost bva-p-socios.
    # Sem isto, o vhost devolve 503 com corpo vazio.
    sudo(ssh,
        "if [ -f /opt/aob/infra/nginx/bva-p-socios-maintenance.html ]; then "
        "  mkdir -p /var/www/aob-maintenance/bva-p-socios && "
        "  cp /opt/aob/infra/nginx/bva-p-socios-maintenance.html "
        "     /var/www/aob-maintenance/bva-p-socios/_maintenance.html && "
        "  chown -R www-data:www-data /var/www/aob-maintenance; "
        "fi",
        check=False)
    # Symlinks sites-enabled
    for domain in ("aobarcelos.pt", "bva-p.aobarcelos.pt", "api.aobarcelos.pt",
                   "admin.aobarcelos.pt", "bva-p-socios.aobarcelos.pt"):
        sudo(ssh,
            f"test -f /etc/nginx/sites-available/{domain}.conf && "
            f"ln -sf /etc/nginx/sites-available/{domain}.conf "
            f"       /etc/nginx/sites-enabled/{domain}.conf || true",
            check=False)
    sudo(ssh, "rm -f /etc/nginx/sites-enabled/default", check=False)
    sudo(ssh, "nginx -t")  # valida config antes do reload
    # Reload em vez de restart - nao interrompe pedidos em curso.
    sudo(ssh, "systemctl reload nginx")
    print("[infra] Concluido.")


def deploy_uploads(ssh) -> None:
    """Sincroniza /uploads/ local (Uploads:RootPath) para /var/www/uploads/ no VPS.

    Idempotente. Usa --keep-newer-files na extraccao - nunca sobrescreve
    ficheiros mais recentes criados no VPS pelo backoffice de admin.

    Uso corrente e raro: so quando se semeou algo local (ex.: fixtures de
    imagens) que precisa chegar ao VPS por fora do backoffice.
    """
    import io, json, tarfile
    from _common import REPO_ROOT

    cfg = REPO_ROOT / "backend" / "src" / "AOB.Api" / "appsettings.Development.json"
    try:
        local_root = Path(json.loads(cfg.read_text(encoding="utf-8"))["Uploads"]["RootPath"])
    except (FileNotFoundError, KeyError, json.JSONDecodeError) as e:
        raise RuntimeError(f"Nao foi possivel ler Uploads:RootPath de {cfg}: {e}")

    if not local_root.exists():
        raise FileNotFoundError(f"Uploads root local nao existe: {local_root}")

    files = list(local_root.rglob("*"))
    n_files = sum(1 for f in files if f.is_file())
    size_mb = sum(f.stat().st_size for f in files if f.is_file()) / 1_048_576
    print(f"\n[uploads] {local_root} -> /var/www/uploads/ ({n_files} ficheiros, {size_mb:.1f} MB)")

    # Empacota em memoria e envia via SFTP para /tmp/, depois sudo cp -a.
    # Nao pode fazer upload directo para /var/www/uploads/ porque o SSH user
    # (debian) nao tem write access la.
    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode="w:gz") as tar:
        for item in _walk(local_root):
            rel = item.relative_to(local_root)
            tar.add(item, arcname=str(rel).replace("\\", "/"))
    buf.seek(0)
    tar_size_mb = buf.getbuffer().nbytes / 1_048_576
    print(f"    upload tar.gz {tar_size_mb:.1f} MB")

    stage = "/tmp/aob_uploads_stage"
    sudo(ssh, f"rm -rf {stage} && mkdir -p {stage} && chown {VPS_USER}:{VPS_USER} {stage}")
    sftp_bytes(ssh, buf.getvalue(), f"{stage}.tar.gz")
    sudo(ssh, f"tar xzf {stage}.tar.gz -C {stage} && rm -f {stage}.tar.gz")

    # --keep-newer-files nao existe em `cp`. Usamos tar novamente: cria tar
    # em {stage} e extrai em /var/www/uploads/ com --keep-newer-files. Assim
    # ficheiros mais recentes no VPS (ex.: criados pelo admin backoffice)
    # sao preservados.
    sudo(ssh, f"cd {stage} && tar cf - . | tar xf - -C /var/www/uploads/ --keep-newer-files 2>/dev/null || true")
    # Ownership + setgid propaga o grupo www-data para novos subdirs.
    # Mode 2770 permite ao aob-admin (CKEditor picker) escrever no tree.
    sudo(ssh, "chown -R aob-api:www-data /var/www/uploads/")
    sudo(ssh, "find /var/www/uploads -type d -exec chmod 2770 {} +")
    sudo(ssh, "find /var/www/uploads -type f -exec chmod 0660 {} +")
    sudo(ssh, f"rm -rf {stage}")
    print("[uploads] Concluido.")


def start_services(ssh) -> None:
    """Para Apache2, arranca Nginx e todos os servicos AOB."""
    print("\n[services] Parar Apache2")
    sudo(ssh, "systemctl stop apache2 2>/dev/null || true", check=False)
    sudo(ssh, "systemctl disable apache2 2>/dev/null || true", check=False)

    print("[services] Nginx")
    sudo(ssh, "systemctl enable nginx && systemctl restart nginx")

    print("[services] Servicos AOB")
    for svc in ("aob-api", "aob-admin", "aob-aobarcelos", "aob-bva-portugal"):
        sudo(ssh, f"systemctl enable {svc} && systemctl start {svc}", check=False)
        sudo(ssh, f"systemctl status --no-pager {svc} | head -4", check=False)

    print("[services] Concluido.")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

TARGETS: dict = {
    "api":        deploy_api,
    "admin":      deploy_admin,
    "aobarcelos": lambda ssh: deploy_frontend(
        ssh, "aobarcelos", FRONTENDS / "aobarcelos",
        "/opt/aob/aobarcelos", "aob-aobarcelos"),
    "bva":        lambda ssh: deploy_frontend(
        ssh, "bva-portugal", FRONTENDS / "bva-portugal",
        "/opt/aob/bva-portugal", "aob-bva-portugal"),
    "uploads":    deploy_uploads,
    "infra":      deploy_infra,
    "services":   start_services,
    # Migrations correm sempre dentro do deploy_api antes do restart. Este
    # target sozinho serve para aplicar migrations sem redeploy da API
    # (raro, mas util em investigacao).
    "migrations": run_migrations,
}

ALL_ORDER = ["infra", "api", "admin", "uploads", "aobarcelos", "bva", "services"]


def main() -> None:
    args = sys.argv[1:] if len(sys.argv) > 1 else ["all"]

    if args == ["all"]:
        order = ALL_ORDER
    else:
        unknown = [a for a in args if a not in TARGETS]
        if unknown:
            print(f"Targets desconhecidos: {unknown}")
            print("Validos:", ", ".join(TARGETS) + ", all")
            print("(Para bootstrap de VPS novo, ver deploy_inicial.py.)")
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

    print("\nDeploy concluido com sucesso.")


if __name__ == "__main__":
    main()
