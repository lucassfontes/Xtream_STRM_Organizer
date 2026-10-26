#!/usr/bin/env python3
"""Teste isolado da migração da biblioteca para Biblioteca_IPTV/<Nome>."""
import json
import pathlib
import subprocess
import sys
import tempfile

script = pathlib.Path(__file__).resolve().parent.parent / 'migrar-biblioteca-zimaos.py'
with tempfile.TemporaryDirectory(prefix='xtream-migracao-') as directory:
    base = pathlib.Path(directory)
    (base / 'config').mkdir()
    profiles = [
        {'Id': '11111111', 'Name': 'Minha IPTV', 'LegacyRoot': False, 'FolderName': 'Minha IPTV-11111111'},
        {'Id': '22222222', 'Name': 'Filmes Brasil', 'LegacyRoot': False, 'FolderName': 'Filmes Brasil-22222222'},
    ]
    (base / 'config' / 'connections.json').write_text(json.dumps(profiles), encoding='utf-8')
    old = base / 'saida' / 'Biblioteca IPTV' / 'Conexoes'
    first = old / 'Minha IPTV-11111111' / 'Filmes' / 'Ação'
    first.mkdir(parents=True)
    (first / 'Filme.strm').write_text('original\n', encoding='utf-8')
    second = old / 'Filmes Brasil-22222222' / 'TV ao vivo' / 'Brasil'
    second.mkdir(parents=True)
    (second / 'Canal.strm').write_text('canal\n', encoding='utf-8')
    new = base / 'Biblioteca_IPTV' / 'Minha IPTV' / 'Filmes' / 'Ação'
    new.mkdir(parents=True)
    (new / 'Filme.strm').write_text('PERSONALIZADO\n', encoding='utf-8')
    run = lambda *extra: subprocess.run([sys.executable, str(script), '--base', str(base), *extra], text=True, capture_output=True, check=True)
    dry = run('--dry-run')
    assert '1 a copiar' in dry.stdout, dry.stdout
    assert (new / 'Filme.strm').read_text() == 'PERSONALIZADO\n'
    assert not (base / 'Biblioteca_IPTV' / 'Filmes Brasil').exists()
    apply = run()
    assert '1 copiados' in apply.stdout, apply.stdout
    assert (new / 'Filme.strm').read_text() == 'PERSONALIZADO\n'
    assert (base / 'Biblioteca_IPTV' / 'Filmes Brasil' / 'TV ao vivo' / 'Brasil' / 'Canal.strm').read_text() == 'canal\n'
    assert (first / 'Filme.strm').read_text() == 'original\n'
    again = run()
    assert '0 copiados' in again.stdout, again.stdout
    assert not (base / 'Biblioteca_IPTV' / 'Conexoes').exists()
    print('OK: migração idempotente, sem sobrescrever, apagar ou criar pasta Conexoes.')
