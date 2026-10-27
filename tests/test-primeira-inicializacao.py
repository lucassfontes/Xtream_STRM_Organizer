#!/usr/bin/env python3
"""Tests actual startup in a clean folder. Docker/sudo/chown are simulated; file creation and terminal are real."""
import hashlib
import os
from pathlib import Path
import pty
import select
import shutil
import subprocess
import tempfile
import time

source = Path(__file__).resolve().parents[1]
required = ["iniciar-linux.sh", "preparar-docker.sh", "permissoes-linux.sh", ".env.example", "docker-compose.yml", "Dockerfile", "XtreamStrmOrganizer.csproj"]
for name in required:
    assert (source/name).is_file(), f"Arquivo obrigatório ausente no pacote: {name}"
with tempfile.TemporaryDirectory(prefix="xtream-start-") as temp:
    root = Path(temp)
    for name in required: shutil.copy2(source/name, root/name)
    bin_dir = root/"fake-bin"; bin_dir.mkdir()
    (bin_dir/"docker").write_text("""#!/usr/bin/env bash
case "$*" in
  '--version') echo 'Docker simulado para teste' ;;
  'compose version') echo 'Docker Compose simulado para teste' ;;
  'info') exit 0 ;;
  'compose --ansi never up -d --build')
    if [ "${TEST_DOCKER_FAIL:-0}" = 1 ]; then
      echo 'Error response from daemon: Conflict. Container fixture already exists. FULL-ERROR-END' >&2
      exit 23
    fi
    echo 'Compose up simulado; nenhum contêiner real criado.' ;;
  *) echo "Comando inesperado no teste: $*" >&2; exit 99 ;;
esac
""")
    # No privilege escalation in tests. Files belong to the test user.
    (bin_dir/"sudo").write_text('#!/usr/bin/env bash\nexec "$@"\n')
    # Some sandbox user namespaces cannot chown to UID 1000.
    (bin_dir/"chown").write_text('#!/usr/bin/env bash\nexit 0\n')
    for path in bin_dir.iterdir(): path.chmod(0o755)
    env = dict(os.environ, PATH=str(bin_dir)+os.pathsep+os.environ["PATH"], XTREAM_NO_PAUSE="1")
    for key in ["MEDIA_PATH", "SUDO_UID", "SUDO_GID", "PUID", "PGID"]: env.pop(key, None)
    def run(fail=False):
        return subprocess.run(["bash",str(root/"iniciar-linux.sh")], cwd="/tmp", env=dict(env,TEST_DOCKER_FAIL="1" if fail else "0"), capture_output=True, text=True, timeout=20)
    first=run()
    assert first.returncode==0,first.stdout+first.stderr
    assert (root/".env").read_bytes()==(source/".env.example").read_bytes()
    assert (root/".env").stat().st_mode & 0o777 == 0o600
    with (root/".env").open("a") as f: f.write("# Minha configuração preservada\n")
    original=(root/".env").read_bytes()
    assert run().returncode==0
    assert (root/".env").read_bytes()==original
    failed=run(True)
    assert failed.returncode==23,failed
    assert "FULL-ERROR-END" in failed.stdout
    assert any("FULL-ERROR-END" in p.read_text() for p in (root/"logs").glob("*.log"))
    # A real PTY verifies the window waits for Enter after a failure and success.
    for should_fail in [True,False]:
        master,slave=pty.openpty()
        interactive=dict(env,XTREAM_NO_PAUSE="0",TEST_DOCKER_FAIL="1" if should_fail else "0")
        child=subprocess.Popen(["bash",str(root/"iniciar-linux.sh")],env=interactive,stdin=slave,stdout=slave,stderr=slave)
        os.close(slave)
        output=b""
        try:
            until=time.monotonic()+15
            while b"Pressione Enter" not in output and time.monotonic()<until:
                ready,_,_=select.select([master],[],[],.2)
                if ready: output+=os.read(master,65536)
            assert b"Pressione Enter" in output,output.decode(errors="replace")
            assert child.poll() is None,"A janela encerrou antes de Enter"
            os.write(master,b"\n")
            assert child.wait(timeout=5)==(23 if should_fail else 0)
        finally:
            if child.poll() is None: child.kill();child.wait()
            os.close(master)
print("PASS: template presente, primeira inicialização, .env preservado, erro completo no log e janela aguardando Enter no sucesso/erro. Docker simulado.")
