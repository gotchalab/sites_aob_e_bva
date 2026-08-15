import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd):
    _, stdout, stderr = ssh.exec_command(cmd)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Ultimas 80 linhas do journal API
out, _ = run("sudo journalctl -u aob-api -n 80 --no-pager")
print("=== API journal (80 linhas) ===")
print(out[-3000:])  # Ultimas 3000 chars

# Testar conectividade DB directamente
out, _ = run("sudo -u aob-api /opt/dotnet/dotnet --version")
print(f"\ndotnet version: {out}")

# Ver env file da API
out, _ = run("sudo cat /etc/aob/api.env | grep -v Password | grep -v Key | grep -v SigningKey | grep -v SmtpPassword")
print(f"\nAPI env (sem segredos):\n{out}")

# Testar ligacao PG directamente
out, _ = run("sudo -u postgres psql -d aob_prod -c 'SELECT COUNT(*) FROM \"Sites\";' 2>&1")
print(f"\nSites count: {out}")

ssh.close()
