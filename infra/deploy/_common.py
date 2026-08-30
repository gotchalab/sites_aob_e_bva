# -*- coding: utf-8 -*-
"""Helpers partilhados por deploy.py e deploy_inicial.py.

Contem:
  - configuracao lida de env vars (host, user, chave SSH, PG)
  - constantes de versoes (Next, .NET)
  - paths do repo
  - SSH/SFTP helpers via paramiko
  - upload_tar (gzip em memoria + extract remoto)
  - deteccao de branch git para aviso pre-deploy

Nao contem targets nem logica de negocio; qualquer target vive no
`deploy.py` (corrente) ou `deploy_inicial.py` (bootstrap-only).
"""

import io
import os
import subprocess
import sys
import tarfile
from pathlib import Path

try:
    import paramiko
except ImportError:
    sys.exit("Instala paramiko: pip install paramiko")


# ---------------------------------------------------------------------------
# Configuracao (env-driven; defaults do ambiente actual)
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
    with tarfile.open(fileobj=buf, mode="w:gz") as tar:
        for item in _walk(local_dir):
            rel = item.relative_to(local_dir)
            if any(part in exclude for part in rel.parts):
                continue
            tar.add(item, arcname=str(rel).replace("\\", "/"))
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
# Git branch awareness
# ---------------------------------------------------------------------------

def current_branch() -> str | None:
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


def warn_if_not_on_main() -> None:
    """Avisa (nao bloqueia) se estamos a deployar de um branch != main.

    A convencao (ver CONTRIBUTING.md) e deployar apos merge de dev → main
    + tag. Deployar directo de dev/feature branch e legitimo para hotfixes
    ou testes pontuais, mas fica ruidoso no log.

    Para bloquear estritamente (CI, prod), definir AOB_STRICT_BRANCH=1
    — deployar de branch != main passa a ser erro.
    """
    branch = current_branch()
    if branch is None or branch == "main":
        return
    strict = os.environ.get("AOB_STRICT_BRANCH") in ("1", "true", "yes")
    banner = "=" * 52
    print()
    print(banner)
    print(f"  {'x' if strict else '!'}  A deployar de '{branch}', nao de 'main'.")
    print("     Convencao (CONTRIBUTING.md): deploy so a partir de main")
    print("     apos merge de dev + tag vX.Y.Z.")
    if strict:
        print("     AOB_STRICT_BRANCH activo - a abortar.")
        print(banner)
        sys.exit(1)
    print(banner)
