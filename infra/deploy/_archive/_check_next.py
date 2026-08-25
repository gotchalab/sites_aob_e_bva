import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

for svc in ("aob-aobarcelos", "aob-bva-portugal"):
    print(f"\n{'='*60}")
    print(f"=== journalctl {svc} (50 linhas) ===")
    _, stdout, _ = ssh.exec_command(f"sudo journalctl -u {svc} -n 50 --no-pager")
    print(stdout.read().decode(errors="replace"))

ssh.close()
