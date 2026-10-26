#!/usr/bin/env python3
"""Migração opcional e não destrutiva da biblioteca antiga Xtream para Biblioteca_IPTV/Nome.

Nunca apaga ou sobrescreve arquivos existentes. Requer acesso aos diretórios e Python 3.
"""
import argparse
import json
import os
import re
import shutil
import sys
from pathlib import Path


def field(obj, name, default=None):
    return obj.get(name, obj.get(name[0].lower() + name[1:], default))


def folder_name(name):
    clean = ''.join(' ' if ch in '/\\:*?"<>|' or ord(ch) < 32 else ch for ch in name)
    clean = re.sub(r'\s+', ' ', clean).strip(' .')
    if not clean or clean in ('.', '..'):
        clean = 'Lista'
    if clean.split('.')[0].upper() in {'CON', 'PRN', 'AUX', 'NUL'} | {f'{kind}{n}' for kind in ('COM', 'LPT') for n in range(1, 10)}:
        clean += '_'
    return clean[:130].rstrip(' .') or 'Lista'


def merge_only_new(source, destination, dry_run):
    counters = {'copiados': 0, 'existentes': 0, 'erros': 0}
    for current, dirs, files in os.walk(source, followlinks=False):
        base = Path(current)
        dirs[:] = [d for d in dirs if not (base / d).is_symlink()]
        relative = base.relative_to(source)
        target = destination / relative
        if target.is_symlink():
            print(f'ATENÇÃO: symlink no destino, ignorando: {target}')
            counters['erros'] += len(files)
            dirs.clear()
            continue
        if not dry_run:
            try:
                target.mkdir(parents=True, exist_ok=True)
            except (OSError, PermissionError) as e:
                print(f'ERRO: não foi possível criar {target}: {e}')
                counters['erros'] += len(files)
                dirs.clear()
                continue
        for filename in files:
            src = base / filename
            dst = target / filename
            if src.is_symlink() or not src.is_file():
                continue
            if dst.exists() or dst.is_symlink():
                counters['existentes'] += 1
                continue
            if dry_run:
                counters['copiados'] += 1
                continue
            try:
                with src.open('rb') as read:
                    with dst.open('xb') as write:
                        shutil.copyfileobj(read, write, 1024 * 1024)
                shutil.copystat(src, dst)
                counters['copiados'] += 1
            except FileExistsError:
                counters['existentes'] += 1
            except OSError as e:
                print(f'ERRO copiando {src}: {e}')
                # Remove exclusivamente cópia incompleta recém-criada quando não houve sucesso.
                # Nunca toca em um arquivo que já existia no destino.
                counters['erros'] += 1
    return counters


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base', type=Path, default=Path('/DATA/AppData/XtreamStrmOrganizer'))
    parser.add_argument('--old-library', type=Path, help='pasta antiga "Biblioteca IPTV" se o destino antigo era personalizado')
    parser.add_argument('--dry-run', action='store_true', help='apenas mostrar contagem sem copiar')
    args = parser.parse_args()
    old = args.old_library or (args.base / 'saida' / 'Biblioteca IPTV')
    new = args.base / 'Biblioteca_IPTV'
    cfg = args.base / 'config'
    profiles_file = cfg / 'connections.json'
    try:
        if profiles_file.exists():
            profiles = json.loads(profiles_file.read_text(encoding='utf-8'))
        else:
            legacy_file = cfg / 'config.json'
            if not legacy_file.exists():
                raise ValueError('Não encontrei connections.json nem config.json em config/.')
            profiles = [json.loads(legacy_file.read_text(encoding='utf-8'))]
            profiles[0]['Name'] = 'Lista importada'
            profiles[0]['LegacyRoot'] = True
        if not isinstance(profiles, list):
            raise ValueError('connections.json não contém uma lista de conexões')
        if not old.is_dir():
            raise ValueError(f'Biblioteca antiga não encontrada: {old}')
        names = [folder_name(str(field(profile, 'Name', 'Lista') or 'Lista')) for profile in profiles]
        if len(set(n.casefold() for n in names)) != len(names):
            raise ValueError('Conexões com nomes de pasta repetidos; renomeie antes da migração.')
    except (OSError, ValueError, json.JSONDecodeError) as e:
        print(f'[Xtream] ERRO: {e}', file=sys.stderr)
        return 1
    total = {'copiados': 0, 'existentes': 0, 'erros': 0}
    found = 0
    for profile, name in zip(profiles, names):
        dst = new / name
        if field(profile, 'LegacyRoot', False):
            # Migração muito antiga: copie APENAS seções da biblioteca.
            # Nunca transfira a pasta de outras conexões para dentro desta lista.
            sources = [(old / section, dst / section) for section in ('TV ao vivo', 'Filmes', 'Series', 'Séries')]
        else:
            older_name = str(field(profile, 'FolderName', '') or field(profile, 'Id', ''))
            sources = [(old / 'Conexoes' / older_name, dst)]
        for src, out in sources:
            if not src.is_dir():
                continue
            if src.resolve() == out.resolve():
                print('[Xtream] Origem e destino coincidem; nada a migrar.')
                continue
            found += 1
            print(f'[Xtream] {src}  =>  {out}')
            status = merge_only_new(src, out, args.dry_run)
            print(f"  Novos: {status['copiados']} | Já existiam: {status['existentes']} | Erros: {status['erros']}")
            for k in total:
                total[k] += status[k]
    print(f"[Xtream] Total: {total['copiados']} {'a copiar' if args.dry_run else 'copiados'}; {total['existentes']} preservados; {total['erros']} erros.")
    print('[Xtream] Nada foi apagado nem sobrescrito. Não exclua a pasta antiga sem conferir seus dados.')
    if found == 0:
        print('[Xtream] Nenhuma conexão com pasta antiga encontrada. Confira o caminho antigo e seus perfis.')
        return 2
    return 1 if total['erros'] else 0


if __name__ == '__main__':
    sys.exit(main())
