import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

for c in [
    "sudo systemctl daemon-reload",
    "sudo systemctl restart aob-api",
    "sudo systemctl status aob-api --no-pager",
]:
    print(f"\n=== {c} ===")
    _, stdout, stderr = ssh.exec_command(c)
    print(stdout.read().decode(errors="replace"))
    err = stderr.read().decode(errors="replace")
    if err.strip():
        print("[err]", err)

ssh.close()
