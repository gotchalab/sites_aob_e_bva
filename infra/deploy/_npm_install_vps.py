import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko, time
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=180):
    _, o, e = ssh.exec_command(cmd, timeout=timeout)
    out = o.read().decode(errors="replace").strip()
    err = e.read().decode(errors="replace").strip()
    return out, err

# Fazer upload do package-lock.json para poder usar npm ci
LOCAL = Path("d:/PROJETOS/aob/frontends")

for site, port in [("aobarcelos", 3000), ("bva-portugal", 3001)]:
    print(f"=== {site} ===")

    # Upload package-lock.json
    lock_file = LOCAL / site / "package-lock.json"
    if lock_file.exists():
        sftp = ssh.open_sftp()
        sftp.put(str(lock_file), "/tmp/package-lock.json")
        sftp.close()
        run(f"sudo mv /tmp/package-lock.json /opt/aob/{site}/package-lock.json")
        run(f"sudo chown aob-web:aob-web /opt/aob/{site}/package-lock.json")
        print(f"  package-lock.json enviado")
        install_cmd = f"cd /opt/aob/{site} && sudo -u aob-web npm ci --omit=dev 2>&1"
    else:
        print(f"  sem package-lock.json, a usar npm install...")
        install_cmd = f"cd /opt/aob/{site} && sudo -u aob-web npm install --omit=dev 2>&1"

    out, err = run(install_cmd, timeout=300)
    lines = out.splitlines()
    print("\n".join(lines[-10:]))

print()
print("A reiniciar servicos...")
run("sudo systemctl restart aob-aobarcelos aob-bva-portugal")
time.sleep(10)

print("Testes:")
for url in [
    "http://127.0.0.1:3000/",
    "http://127.0.0.1:3000/artigos",
    "http://127.0.0.1:3001/",
    "http://127.0.0.1:3001/artigos",
]:
    out, _ = run(f"curl -s -o /dev/null -w '%{{http_code}}' --max-time 10 '{url}'")
    print(f"  {url} -> {out}")

ssh.close()
