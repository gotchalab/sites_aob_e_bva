#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# sys.stdout forçado para UTF-8 para suportar caracteres como ● do systemctl
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
"""
AOB Deploy Script — Windows-compatible (paramiko, sem rsync).

Uso:
    python deploy.py [target [target ...]]

Targets:
    setup       — instala dependencias no VPS (dotnet 10, next@15.5.4, users, dirs)
    db          — faz dump da BD local e restaura no VPS (DESTRUTIVO; so em bootstrap)
    api         — build + deploy AOB.Api + AOB.Migrator; corre migrations
                  (AOB.Migrator db-update) automaticamente antes do restart
    admin       — build + deploy AOB.Admin
    aobarcelos  — deploy .next/ do frontend aobarcelos.pt
    bva         — deploy .next/ do frontend bva-p.aobarcelos.pt
    uploads     — sincroniza /uploads/ local (Uploads:RootPath) para /var/www/uploads/
                  (correr apos AOB.Migrator; nunca sobrescreve ficheiros mais recentes
                  no VPS — respeita uploads criados pelo backoffice de admin)
    migrations  — corre AOB.Migrator db-update sozinho (raro; api ja o faz)
    infra       — copia nginx.conf + systemd units + daemon-reload
    services    — para Apache2, arranca nginx e todos os servicos AOB
    all         — setup + db + infra + api + admin + uploads + aobarcelos + bva + services

Env opcional:
    AOB_SKIP_MIGRATIONS=1  — salta AOB.Migrator db-update em deploy api
                              (raro; documenta o porque num commit se usares)

Variaveis de ambiente (opcionais):
    AOB_SSH_HOST  — default: 51.83.40.43
    AOB_SSH_USER  — default: debian
    AOB_SSH_KEY   — caminho da chave SSH privada (default: ~/.ssh/id_ed25519)
    AOB_PG_LOCAL  — connection string local pg_dump (default: postgres://postgres@localhost/aob)
    AOB_PG_PASS   — password do user aobapp no VPS (para criar BD)

Pre-deploy (manual, uma so vez):
    Criar /etc/aob/api.env, /etc/aob/admin.env, /etc/aob/aobarcelos.env,
    /etc/aob/bva-portugal.env a partir dos env.sample em infra/deploy/env-samples/.
"""

import io
import os
import shutil
import subprocess
import sys
import tarfile
import tempfile
from pathlib import Path

try:
    import paramiko
except ImportError:
    sys.exit("Instala paramiko: pip install paramiko")

# ---------------------------------------------------------------------------
# Configuracao
# ---------------------------------------------------------------------------

VPS_HOST     = os.environ.get("AOB_SSH_HOST", "51.83.40.43")
VPS_USER     = os.environ.get("AOB_SSH_USER", "debian")
VPS_KEY      = os.environ.get("AOB_SSH_KEY",  str(Path.home() / ".ssh" / "id_ed25519"))
AOB_PG_LOCAL = os.environ.get("AOB_PG_LOCAL", "postgres://postgres@localhost/aob")
AOB_PG_PASS  = os.environ.get("AOB_PG_PASS",  "EM3A31tTxtXVpPfJOc2DmcWfoyE+FKm2")

NEXT_VERSION   = "15.5.4"
DOTNET_CHANNEL = "10.0"

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
BACKEND   = REPO_ROOT / "backend"
FRONTENDS = REPO_ROOT / "frontends"
INFRA     = REPO_ROOT / "infra"


# ---------------------------------------------------------------------------
# SSH / SFTP helpers
# ---------------------------------------------------------------------------

def connect() -> paramiko.SSHClient:
    print(f"  SSH -> {VPS_USER}@{VPS_HOST}")
    ssh = paramiko.SSHClient()
    ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    ssh.connect(VPS_HOST, username=VPS_USER, key_filename=VPS_KEY, timeout=30)
    return ssh


def run(ssh: paramiko.SSHClient, cmd: str, check: bool = True) -> str:
    """Corre um comando remoto sem sudo automatico."""
    _, stdout, stderr = ssh.exec_command(cmd, get_pty=False)
    out  = stdout.read().decode(errors="replace")
    err  = stderr.read().decode(errors="replace")
    code = stdout.channel.recv_exit_status()
    if out.strip():
        print(out.rstrip())
    if err.strip():
        print("[stderr]", err.rstrip())
    if check and code != 0:
        raise RuntimeError(f"Comando remoto falhou (exit {code}): {cmd[:120]}")
    return out


def sudo(ssh: paramiko.SSHClient, cmd: str, check: bool = True) -> str:
    """Corre cmd com sudo no VPS. Nao fazer escaping manual antes de chamar esta funcao."""
    escaped = "'" + cmd.replace("'", "'\\''") + "'"
    return run(ssh, f"sudo bash -c {escaped}", check=check)


def sftp_bytes(ssh: paramiko.SSHClient, data: bytes, remote_path: str) -> None:
    """Faz upload de bytes para remote_path via SFTP (como usuario SSH)."""
    sftp = ssh.open_sftp()
    try:
        sftp.putfo(io.BytesIO(data), remote_path)
    finally:
        sftp.close()


def sftp_file(ssh: paramiko.SSHClient, local: Path, remote_path: str) -> None:
    """Faz upload de um ficheiro local para remote_path (como usuario SSH)."""
    sftp = ssh.open_sftp()
    try:
        sftp.put(str(local), remote_path)
    finally:
        sftp.close()


def exec_sql(ssh: paramiko.SSHClient, sql: str, db: str = "postgres") -> None:
    """Executa SQL no PostgreSQL remoto via ficheiro temporario (evita quoting aninhado)."""
    tmp = "/tmp/aob_setup.sql"
    sftp_bytes(ssh, sql.encode(), tmp)
    run(ssh, f"sudo -u postgres psql -d {db} -f {tmp}")
    run(ssh, f"sudo rm -f {tmp}")


def upload_tar(ssh: paramiko.SSHClient, local_dir: Path,
               remote_dir: str, exclude: list[str] | None = None) -> None:
    """Cria tar.gz da local_dir e extrai em remote_dir no VPS.

    O tar e criado em memoria para directorios pequenos/medios.
    Para .next/ sem cache tipicamente 20-80 MB.
    """
    exclude = exclude or []
    buf = io.BytesIO()
    size = 0
    with tarfile.open(fileobj=buf, mode="w:gz") as tar:
        for item in _walk(local_dir):
            rel = item.relative_to(local_dir)
            if any(part in exclude for part in rel.parts):
                continue
            tar.add(item, arcname=str(rel).replace("\\", "/"))
            size += item.stat().st_size
    buf.seek(0)

    mb = buf.getbuffer().nbytes / 1_048_576
    tmp = f"/tmp/aob_deploy_{local_dir.name}.tar.gz"
    print(f"    upload {mb:.1f} MB -> {remote_dir}")
    sftp_bytes(ssh, buf.getvalue(), tmp)

    sudo(ssh, f"mkdir -p {remote_dir}")
    sudo(ssh, f"tar xzf {tmp} -C {remote_dir} && rm -f {tmp}")


def upload_file(ssh: paramiko.SSHClient, local: Path, remote_path: str) -> None:
    """Upload de um ficheiro singular com sudo mv para o destino final."""
    tmp = f"/tmp/aob_file_{local.name}"
    sftp_file(ssh, local, tmp)
    sudo(ssh, f"mv {tmp} {remote_path}")


def _walk(root: Path):
    for p in root.rglob("*"):
        if p.is_file():
            yield p


# ---------------------------------------------------------------------------
# Targets
# ---------------------------------------------------------------------------

def setup(ssh: paramiko.SSHClient) -> None:
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

    print("\n[setup] PostgreSQL — role aobapp + BD aob_prod")
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
    # CREATE DATABASE nao pode correr dentro de transacao — usar shell condicional
    run(ssh, "sudo -u postgres psql -l | grep -q aob_prod "
             "|| sudo -u postgres createdb -O aobapp aob_prod")

    print("\n[setup] Nginx")
    sudo(ssh, "which nginx >/dev/null 2>&1 || (apt-get update && apt-get -y install nginx)", check=False)
    sudo(ssh, "systemctl enable nginx", check=False)

    print("\n[setup] Concluido.")


def restore_db(ssh: paramiko.SSHClient) -> None:
    """Faz pg_dump da BD local e restaura no VPS."""
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


def _dotnet_publish(project_name: str, out_dir: Path) -> None:
    subprocess.run(
        ["dotnet", "publish",
         str(BACKEND / "src" / project_name / f"{project_name}.csproj"),
         "-c", "Release", "-o", str(out_dir),
         "--nologo", "--self-contained", "false"],
        check=True
    )


def run_migrations(ssh: paramiko.SSHClient) -> None:
    """Corre EF Core `AOB.Migrator db-update` no VPS. Idempotente.

    Usa o mesmo user (aob-api) e EnvironmentFile (/etc/aob/api.env) do
    serviço aob-api, para partilhar ConnectionStrings__Default. Se nao
    houver migrations pendentes, o Migrator imprime uma linha e sai 0.

    Set AOB_SKIP_MIGRATIONS=1 para saltar (raramente util; documenta o
    porque na mensagem de commit se o fizeres).
    """
    if os.environ.get("AOB_SKIP_MIGRATIONS") in ("1", "true", "yes"):
        print("\n[migrations] AOB_SKIP_MIGRATIONS activo — a saltar db-update.")
        return

    print("\n[migrations] AOB.Migrator db-update (aob-api, /etc/aob/api.env)")
    # 'set -a' exporta automaticamente as vars definidas pelo source do
    # envfile; 'set +a' desliga. cwd tem de ser /opt/aob/api para o
    # AppDbContext carregar appsettings.json correcto.
    sudo(ssh,
        "-u aob-api bash -c '"
        "set -a && . /etc/aob/api.env && set +a && "
        "cd /opt/aob/api && "
        "/opt/dotnet/dotnet AOB.Migrator.dll db-update"
        "'"
    )


def deploy_api(ssh: paramiko.SSHClient) -> None:
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


def deploy_admin(ssh: paramiko.SSHClient) -> None:
    """dotnet publish AOB.Admin -> /opt/aob/admin + restart."""
    print("\n[admin] dotnet publish AOB.Admin")
    out = Path(tempfile.mkdtemp())
    _dotnet_publish("AOB.Admin", out)

    print("[admin] Upload -> /opt/aob/admin")
    upload_tar(ssh, out, "/opt/aob/admin")
    sudo(ssh, "chown -R aob-admin:aob-admin /opt/aob/admin")
    sudo(ssh, "systemctl restart aob-admin && systemctl status --no-pager aob-admin | head -6")
    shutil.rmtree(out, ignore_errors=True)


def deploy_frontend(ssh: paramiko.SSHClient, name: str, local_dir: Path,
                    remote_dir: str, service: str) -> None:
    """Upload .next/ + public/ + package.json + next.config.mjs; sem node_modules."""
    print(f"\n[{name}] Upload .next/ -> {remote_dir}")

    next_dir = local_dir / ".next"
    if not next_dir.exists():
        raise FileNotFoundError(
            f"Build nao encontrado: {next_dir}\n"
            "Corre primeiro: npx next build (com NEXT_PUBLIC_API_URL correcto)"
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
    # no grupo www-data (feito no setup).
    sudo(ssh, f"mkdir -p {remote_dir}/public && "
              f"ln -sfn /var/www/uploads {remote_dir}/public/uploads && "
              f"chown -h aob-web:aob-web {remote_dir}/public/uploads")
    sudo(ssh, f"systemctl restart {service} && systemctl status --no-pager {service} | head -6")
    print(f"[{name}] Concluido.")


def deploy_infra(ssh: paramiko.SSHClient) -> None:
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
    # Mapas de redirect legacy
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
    # Reload em vez de restart — nao interrompe pedidos em curso.
    sudo(ssh, "systemctl reload nginx")
    print("[infra] Concluido.")


def deploy_uploads(ssh: paramiko.SSHClient) -> None:
    """Sincroniza /uploads/ local (Uploads:RootPath) para /var/www/uploads/ no VPS.

    Correr sempre a seguir a `AOB.Migrator` (que grava ficheiros no filesystem
    local mas nao chega ao VPS de outra forma). Idempotente. Usa --keep-newer-files
    na extraccao — nunca sobrescreve ficheiros mais recentes criados no VPS
    pelo backoffice de admin.
    """
    import json
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


def start_services(ssh: paramiko.SSHClient) -> None:
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
    "setup":      setup,
    "db":         restore_db,
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

ALL_ORDER = ["setup", "db", "infra", "api", "admin", "uploads", "aobarcelos", "bva", "services"]


def _current_branch() -> str | None:
    """Devolve o nome do branch git actual, ou None se falhar/detached."""
    try:
        out = subprocess.check_output(
            ["git", "rev-parse", "--abbrev-ref", "HEAD"],
            cwd=str(REPO_ROOT), stderr=subprocess.DEVNULL,
        )
        name = out.decode().strip()
        return None if name in ("", "HEAD") else name
    except (subprocess.CalledProcessError, FileNotFoundError):
        return None


def _warn_if_not_on_main() -> None:
    """Avisa (nao bloqueia) se estamos a deployar de um branch != main.

    A convencao (ver CONTRIBUTING.md) e deployar apos merge de dev → main
    + tag. Deployar directo de dev/feature branch e legitimo para hotfixes
    ou testes pontuais, mas fica ruidoso no log.

    Para bloquear estritamente (CI, prod), definir AOB_STRICT_BRANCH=1
    — deployar de branch != main passa a ser erro.
    """
    branch = _current_branch()
    if branch is None or branch == "main":
        return
    strict = os.environ.get("AOB_STRICT_BRANCH") in ("1", "true", "yes")
    banner = "=" * 52
    print()
    print(banner)
    print(f"  {'✗' if strict else '⚠'}  A deployar de '{branch}', nao de 'main'.")
    print("     Convencao (CONTRIBUTING.md): deploy so a partir de main")
    print("     apos merge de dev + tag vX.Y.Z.")
    if strict:
        print("     AOB_STRICT_BRANCH activo — a abortar.")
        print(banner)
        sys.exit(1)
    print(banner)


def main() -> None:
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

    _warn_if_not_on_main()

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
    if "setup" in order:
        # Proximos passos so relevantes em bootstrap inicial (target setup).
        print("\nProximos passos manuais no VPS (so no primeiro provisioning):")
        print("  1. Criar /etc/aob/*.env (ver infra/deploy/env-samples/)")
        print("  2. systemctl restart aob-api aob-admin")
        print("  3. certbot --nginx -d aobarcelos.pt -d www.aobarcelos.pt \\")
        print("       -d bva-p.aobarcelos.pt -d api.aobarcelos.pt -d admin.aobarcelos.pt")
        print("     (migrations correm agora automaticamente dentro de deploy api)")


if __name__ == "__main__":
    main()
