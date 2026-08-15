import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko, tarfile, time
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=60):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

LOCAL_BVA = Path("d:/PROJETOS/aob/frontends/bva-portugal")
REMOTE_BVA = "/opt/aob/bva-portugal"

EXCLUDE = {".cache", "cache"}

def upload_tar(local_dir: Path, remote_dir: str, label: str):
    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode="w:gz") as tar:
        for item in sorted(local_dir.rglob("*")):
            if not item.is_file():
                continue
            rel = item.relative_to(local_dir)
            if any(part in EXCLUDE for part in rel.parts):
                continue
            tar.add(item, arcname=str(rel).replace("\\", "/"))
    data = buf.getvalue()
    print(f"  Uploading {label} ({len(data)//1024}KB)...")
    sftp = ssh.open_sftp()
    sftp.putfo(io.BytesIO(data), "/tmp/bva_deploy.tar.gz")
    sftp.close()
    run(f"sudo tar xzf /tmp/bva_deploy.tar.gz -C {remote_dir}")
    run("sudo rm -f /tmp/bva_deploy.tar.gz")
    run(f"sudo chown -R aob-web:aob-web {remote_dir}")

print("Parando aob-bva-portugal...")
run("sudo systemctl stop aob-bva-portugal")
time.sleep(1)

# Enviar apenas o .next/ (com o manifesto corrigido)
print("Enviando .next/...")
upload_tar(LOCAL_BVA / ".next", f"{REMOTE_BVA}/.next", ".next/")

print("Iniciando aob-bva-portugal...")
run("sudo systemctl start aob-bva-portugal")
time.sleep(7)

out, _ = run("sudo systemctl is-active aob-bva-portugal")
print(f"Status: {out}")

# Verificar o manifesto no VPS
out, _ = run("cat /opt/aob/bva-portugal/.next/server/app-paths-manifest.json 2>&1 | head -20")
print(f"\nManifesto no VPS:\n{out}\n")

# Testar
out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://127.0.0.1:3001/ 2>&1")
print(f"bva:3001 / -> HTTP {out}")

out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://127.0.0.1:3001/contacto 2>&1")
print(f"bva:3001 /contacto -> HTTP {out}")

out, _ = run("curl -s --max-time 10 http://127.0.0.1:3001/ 2>&1 | head -c 150")
print(f"\nBVA home preview:\n{out}")

ssh.close()
