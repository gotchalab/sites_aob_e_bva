"""
Redeploy dos frontends Next.js para a VPS.

Pré-requisito local:
    cd frontends/aobarcelos && npm run build:prod   → cria aobarcelos/dist/
    cd frontends/bva-portugal && npm run build:prod → cria bva-portugal/dist/

O que faz:
    - Para o serviço no VPS
    - Faz upload do dist/ via tar+SFTP
    - Extrai para /opt/aob/{site}/
    - Reinicia o serviço
    - Testa HTTP
"""
import sys, io, tarfile, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=60):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

EXCLUDE_DIRS = {".cache", "cache"}

def upload_dir(local_dir: Path, remote_dir: str, label: str):
    if not local_dir.exists():
        print(f"  ERRO: {local_dir} não existe — corre primeiro 'npm run build:prod'")
        return False

    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode="w:gz") as tar:
        for item in sorted(local_dir.rglob("*")):
            if not item.is_file():
                continue
            rel = item.relative_to(local_dir)
            if any(part in EXCLUDE_DIRS for part in rel.parts):
                continue
            tar.add(item, arcname=str(rel).replace("\\", "/"))

    data = buf.getvalue()
    print(f"  A enviar {label} ({len(data)//1024} KB)...")
    sftp = ssh.open_sftp()
    sftp.putfo(io.BytesIO(data), "/tmp/frontend_deploy.tar.gz")
    sftp.close()
    run(f"sudo tar xzf /tmp/frontend_deploy.tar.gz -C {remote_dir}")
    run("sudo rm -f /tmp/frontend_deploy.tar.gz")
    run(f"sudo chown -R aob-web:aob-web {remote_dir}")
    return True

LOCAL = Path("d:/PROJETOS/aob/frontends")
REMOTE = "/opt/aob"

# === AOBARCELOS ===
print("=== AOBARCELOS ===")
dist_aob = LOCAL / "aobarcelos/dist"
if not dist_aob.exists():
    print("  dist/ não existe. A correr npm run build:prod primeiro...")
    import subprocess
    subprocess.run(
        ["npm", "run", "build:prod"],
        cwd=str(LOCAL / "aobarcelos"),
        check=True,
        shell=True
    )

run("sudo systemctl stop aob-aobarcelos")
time.sleep(1)

if upload_dir(dist_aob, f"{REMOTE}/aobarcelos", "aobarcelos dist/"):
    run("sudo systemctl start aob-aobarcelos")
    time.sleep(8)
    out, _ = run("sudo systemctl is-active aob-aobarcelos")
    print(f"  Status: {out}")
    out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://127.0.0.1:3000/")
    print(f"  aobarcelos:3000 / → HTTP {out}")
    out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://127.0.0.1:3000/artigos")
    print(f"  aobarcelos:3000 /artigos → HTTP {out}")
else:
    run("sudo systemctl start aob-aobarcelos")

# === BVA ===
print("\n=== BVA ===")
dist_bva = LOCAL / "bva-portugal/dist"
if not dist_bva.exists():
    print("  dist/ não existe. A correr npm run build:prod primeiro...")
    import subprocess
    subprocess.run(
        ["npm", "run", "build:prod"],
        cwd=str(LOCAL / "bva-portugal"),
        check=True,
        shell=True
    )

run("sudo systemctl stop aob-bva-portugal")
time.sleep(1)

if upload_dir(dist_bva, f"{REMOTE}/bva-portugal", "bva dist/"):
    run("sudo systemctl start aob-bva-portugal")
    time.sleep(8)
    out, _ = run("sudo systemctl is-active aob-bva-portugal")
    print(f"  Status: {out}")
    out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://127.0.0.1:3001/")
    print(f"  bva:3001 / → HTTP {out}")
    out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://127.0.0.1:3001/artigos")
    print(f"  bva:3001 /artigos → HTTP {out}")
else:
    run("sudo systemctl start aob-bva-portugal")

# === TESTES HTTPS FINAIS ===
print("\n=== TESTES HTTPS ===")
for domain, path in [
    ("aobarcelos.pt", "/"),
    ("aobarcelos.pt", "/artigos"),
    ("bva-p.aobarcelos.pt", "/"),
    ("bva-p.aobarcelos.pt", "/artigos"),
]:
    out, _ = run(f"curl -s -o /dev/null -w '%{{http_code}}' --max-time 10 https://{domain}{path}")
    status = "OK" if out == "200" else "PROBLEMA"
    print(f"  [{status}] https://{domain}{path} → {out}")

ssh.close()
