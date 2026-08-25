#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Backup remoto pre-deploy: chama /opt/aob/infra/deploy/backup.sh no VPS."""
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

import os
from pathlib import Path
import paramiko

VPS_HOST = os.environ.get("AOB_SSH_HOST", "51.83.40.43")
VPS_USER = os.environ.get("AOB_SSH_USER", "debian")
VPS_KEY  = os.environ.get("AOB_SSH_KEY",  str(Path.home() / ".ssh" / "id_ed25519"))

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect(VPS_HOST, username=VPS_USER, key_filename=VPS_KEY, timeout=30)

def run(cmd):
    _, o, e = ssh.exec_command(cmd, get_pty=False)
    out = o.read().decode(errors="replace")
    err = e.read().decode(errors="replace")
    code = o.channel.recv_exit_status()
    if out.strip(): print(out.rstrip())
    if err.strip(): print("[stderr]", err.rstrip())
    return code

print(f"SSH -> {VPS_USER}@{VPS_HOST}")
print("\n[backup] listar backups actuais em /var/backups/aob")
run("sudo ls -lh /var/backups/aob/ 2>/dev/null | tail -10 || echo '(vazio)'")

print("\n[backup] executar backup.sh no VPS")
code = run("sudo bash /opt/aob/infra/deploy/backup.sh")
if code != 0:
    print(f"\n[erro] backup.sh saiu com {code}")
    sys.exit(code)

print("\n[backup] listar backups apos execucao")
run("sudo ls -lh /var/backups/aob/ | tail -10")

ssh.close()
print("\nBackup concluido.")
