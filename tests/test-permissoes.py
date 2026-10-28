#!/usr/bin/env python3
"""Real UID regression when root/chown are available; Compose structure checked everywhere."""
import os, sys, subprocess, tempfile, shutil
from pathlib import Path
source = Path(__file__).resolve().parents[1]
commands=[]
for name in ('docker-compose.github.yml','docker-compose.zimaos.yml','docker-compose.zimaos-hd.yml','docker-compose.yml'):
    content=(source/name).read_text()
    assert 'condition: service_completed_successfully' in content
    assert 'find /media -xdev -type d -user root -exec chown' in content
    assert '-exec chmod u+rwx {} +' in content
    assert 'chown -R 1000:1000 /media' not in content
    assert 'chmod 777' not in content
    if name in ('docker-compose.github.yml','docker-compose.zimaos.yml'):
        block=content.split('    command:\n      - |\n',1)[1].split('\n\n',1)[0]
        commands.append('\n'.join(line[10:] for line in block.splitlines()))
assert commands[0]==commands[1]
print('OK: Compose prepara subpastas antes do aplicativo; sem alterar arquivos de mídia.')
with tempfile.TemporaryDirectory(prefix='xtream-permissions-') as temp:
    root=Path(temp);root.chmod(0o755)
    try:
        os.chown(root,1000,1000)
        os.chown(root,0,0)
    except OSError:
        print('NÃO VALIDADO aqui: troca real de UID/GID indisponível. O CI exige este teste com root.')
        if '--require-root' in sys.argv: raise
        sys.exit(0)
    dotnet=shutil.which('dotnet') or os.environ.get('DOTNET')
    assert dotnet, 'dotnet required'
    dll=source/'tests/MetadataTests/bin/Release/net8.0/MetadataTests.dll'
    media=root/'media'; media.mkdir(mode=0o755);os.chown(media,1000,1000)
    channel=media/'AMERICA_PLAY'/'TV ao vivo'/'CANAIS ESPN';channel.mkdir(parents=True)
    for directory in (media/'AMERICA_PLAY',channel.parent,channel):directory.chmod(0o755)
    strm=channel/'ESPN Extra FHD².strm';strm.write_text('preservado');strm.chmod(0o644)
    outside=root/'outside';outside.mkdir();(media/'link-externo').symlink_to(outside,target_is_directory=True)
    def probe():
        return subprocess.run([dotnet,str(dll),'--probe',str(media/'AMERICA_PLAY')],user=1000,group=1000,extra_groups=[],cwd='/tmp',capture_output=True,text=True)
    first=probe();assert first.returncode==2,first
    assert 'pasta' in first.stderr and 'permissão' in first.stderr,first.stderr
    command=commands[0].replace('/app/data',str(root/'config')).replace('/media',str(media))
    for _ in range(2):subprocess.run(['sh','-ec',command],check=True)
    second=probe();assert second.returncode==0,second.stderr
    assert channel.stat().st_uid==1000
    assert strm.stat().st_uid==0 and strm.read_text()=='preservado'
    assert outside.stat().st_uid==0
    assert not list(media.rglob('.xtream-write-test-*'))
    print('OK: falha com root/755 reproduzida; preparação corrige; UID1000 grava; STRM e link externo preservados; repetição segura.')
