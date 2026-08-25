#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Corre AOB.Migrator no VPS (aplica migrations pendentes)."""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
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
print("\n[migrator] correr AOB.Migrator com env da API")
# Reutiliza /etc/aob/api.env para o Migrator (mesma DB connection string)
# systemd-run reutiliza o parser do systemd para EnvironmentFile — evita o
# bug de bash `.` a partir connection strings com `;` em pedacos.
code = run("sudo systemd-run --pipe --wait --collect --quiet "
           "--uid=aob-api --property=EnvironmentFile=/etc/aob/api.env "
           "/opt/dotnet/dotnet /opt/aob/api/AOB.Migrator.dll db-update")
if code != 0:
    print(f"\n[erro] Migrator saiu com {code}")
    sys.exit(code)

print("\n[migrator] restart aob-api (para carregar novo schema)")
run("sudo systemctl restart aob-api && sudo systemctl status --no-pager aob-api | head -6")

ssh.close()
print("\nMigration concluida.")
