#!/usr/bin/env bash
# Teste real com Docker + API Xtream simulada, sem usar uma conta de IPTV.
set -euo pipefail
cd "$(dirname "$0")"
# shellcheck disable=SC1091
source ./preparar-docker.sh
# shellcheck disable=SC1091
source ./permissoes-linux.sh
xtream_uid_gid
xtream_preparar_docker
if ! command -v python3 >/dev/null 2>&1; then
  xtream_info 'Python 3 ausente: instalando dependencia do teste...'
  xtream_preparar_apt
  xtream_root apt-get install -y python3
fi
command -v python3 >/dev/null 2>&1 || { xtream_erro 'Python 3 nao disponivel.'; exit 1; }
# Cada teste recebe pastas próprias: não remover nem alterar bibliotecas anteriores.
mkdir -p test-saida test-config
TEST_RUN="execucao-$(date +%Y%m%d-%H%M%S)-$$"
export XTREAM_TEST_MEDIA_PATH="$(pwd)/test-saida/$TEST_RUN"
export XTREAM_TEST_CONFIG_PATH="$(pwd)/test-config/$TEST_RUN"
mkdir -p "$XTREAM_TEST_MEDIA_PATH" "$XTREAM_TEST_CONFIG_PATH"
printf 'Teste isolado, sem apagar dados anteriores: %s\n' "$XTREAM_TEST_MEDIA_PATH"
if [ "${XTREAM_COMPOSE[0]}" = sudo ]; then
  XTREAM_COMPOSE=(sudo env "PUID=$PUID" "PGID=$PGID" "XTREAM_TEST_MEDIA_PATH=$XTREAM_TEST_MEDIA_PATH" "XTREAM_TEST_CONFIG_PATH=$XTREAM_TEST_CONFIG_PATH" "${XTREAM_COMPOSE[@]:1}")
fi
COMPOSE=("${XTREAM_COMPOSE[@]}" -p xtream-strm-teste -f tests/docker-compose.test.yml)
"${COMPOSE[@]}" up -d --build
python3 - <<'PY'
import json, pathlib, time, urllib.request, urllib.error, http.cookiejar, os
base='http://127.0.0.1:18686'
jar=http.cookiejar.CookieJar()
opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))

def call(path,method='GET',payload=None):
    b = json.dumps(payload).encode() if payload is not None else None
    req=urllib.request.Request(base+path, data=b, method=method, headers={'Content-Type':'application/json'})
    with opener.open(req,timeout=10) as response: return json.load(response)

for attempt in range(60):
    try:
        call('/health')
        break
    except (urllib.error.URLError,ConnectionError): time.sleep(1)
else: raise SystemExit('O aplicativo não iniciou (verifique os logs do Docker).')

# A API privada não é acessível sem autenticação por cookie.
try:
    call('/api/settings')
except urllib.error.HTTPError as ex:
    assert ex.code==401, f'Era esperado HTTP 401; obtido {ex.code}'
else: raise AssertionError('API não autenticada ficou acessível.')

assert call('/api/auth/setup-status')['initialized'] is False
# Senha mínima de 6 caracteres: 5 falha e 6 é aceita.
try: call('/api/auth/setup','POST',{'username':'testeadmin','password':'12345'})
except urllib.error.HTTPError as ex: assert ex.code==400
else: raise AssertionError('Senha com cinco caracteres foi aceita!')
assert call('/api/auth/setup','POST',{'username':'testeadmin','password':'123456'})['ok']
assert call('/api/auth/setup-status')['initialized'] is True
try: call('/api/auth/setup','POST',{'username':'outro','password':'outra-senha-123'})
except urllib.error.HTTPError as ex: assert ex.code==409
else: raise AssertionError('A conta inicial pôde ser substituída!')
assert call('/api/auth/login','POST',{'username':'testeadmin','password':'123456'})['ok']
assert call('/api/auth/me')['username']=='testeadmin'
p=call('/api/settings','POST',{'name':'Lista Teste','url':'http://mock:9876','username':'teste','password':'teste123','live':True,'movies':True,'series':True,'liveExtension':'ts',
    'autoUpdate':True,'intervalHours':12,'runTimeLocal':'03:15','autoLive':True,'autoMovies':False,'autoSeries':True})
profile_id=p['id']
assert p['autoUpdate'] and p['intervalHours']==12 and p['autoMovies'] is False
assert p['nextRunUtc'] and p['folderName']=='Lista Teste', p
assert p['runTimeLocal']=='03:15' and 'Europe/Brussels' in p['serverTimeZone'],p
assert p['nextRunServer'] and 'às' in p['nextRunServer'],p
from datetime import datetime, timezone
next_time=datetime.fromisoformat(p['nextRunUtc'].replace('Z','+00:00'))
assert 0 < (next_time-datetime.now(timezone.utc)).total_seconds() < 12*3600+60, p
# Salvar a mesma programação preserva a data/hora já estabelecida.
p_same=call('/api/settings','POST',{'id':profile_id,'name':'Lista Teste','url':'http://mock:9876','username':'teste','password':'',
  'live':True,'movies':True,'series':True,'autoUpdate':True,'intervalHours':12,'runTimeLocal':'03:15',
  'autoLive':True,'autoMovies':False,'autoSeries':True})
assert p_same['nextRunUtc']==p['nextRunUtc'],(p,p_same)
# Alterar o horário faz um novo cálculo, com o relógio configurável.
p_shift=call('/api/settings','POST',{'id':profile_id,'name':'Lista Teste','url':'http://mock:9876','username':'teste','password':'',
  'live':True,'movies':True,'series':True,'autoUpdate':True,'intervalHours':12,'runTimeLocal':'04:30',
  'autoLive':True,'autoMovies':False,'autoSeries':True})
assert p_shift['runTimeLocal']=='04:30' and p_shift['nextRunUtc'] != p['nextRunUtc'],p_shift
assert p_shift['nextRunServer'].endswith(('04:30:00','16:30:00')), p_shift

assert len(call('/api/profiles'))==1
for attempt in range(30):
    try:
        assert call('/api/test','POST',{'profileId':profile_id})['ok']
        break
    except urllib.error.HTTPError:
        time.sleep(1)
else: raise SystemExit('API Xtream simulada não respondeu (veja logs dos contêineres).')
def run(force=False, selected_id=profile_id):
    call('/api/start','POST',{'forceRefresh':force,'profileId':selected_id})
    for attempt in range(120):
        state=call('/api/status')
        if not state['running']: return state
        time.sleep(0.5)
    raise SystemExit('A organização não terminou em 60 segundos.')
s=run(False)
assert s['seriesFetched']==1 and s['seriesCached']==0, s
assert s['filesPending']==0 and s['writeWorkers']==4, s
assert s['filesWritten']>=7, s
print('Status:',s['phase'])
print('Contadores:',{k:s[k] for k in ('channels','movies','series','episodes','errors')})
assert (s['channels'],s['movies'],s['series'],s['episodes'],s['errors'])==(2,2,1,3,0), s
root=pathlib.Path(os.environ['XTREAM_TEST_MEDIA_PATH'])/p['folderName']
expect={
  'TV ao vivo/BRASIL/Canal Teste.strm':'/live/teste/teste123/101.ts',
  'TV ao vivo/XXX/Canal Adulto.strm':'/live/teste/teste123/102.ts',
  'Filmes/Ação/Filme Legal.strm':'/movie/teste/teste123/201.mkv',
  'Filmes/18+/Filme Restrito.strm':'/movie/teste/teste123/202.mp4',
  'Series/Drama/Série Teste/Temporada 01/S01E01 - O começo.strm':'/series/teste/teste123/4001.mp4',
  'Series/Drama/Série Teste/Temporada 01/S01E10 - O final.strm':'/series/teste/teste123/4002.mkv',
  'Series/Drama/Série Teste/Temporada 02/S02E01 - Nova fase.strm':'/series/teste/teste123/4003.mp4',
}
for subpath,suffix in expect.items():
    f=root/subpath
    assert f.is_file(),f'Arquivo não encontrado: {f}'
    actual=f.read_text().strip()
    assert actual=='http://mock:9876'+suffix, (f,actual)
assert not (root/'TV ao vivo'/'Adultos').exists()
assert not (root/'Filmes'/'Adultos').exists()
# Regression: um arquivo criado manualmente e um STRM modificado nunca podem sumir nem ser alterados.
sentinel=root/'Filmes'/'Categoria Antiga'/'Filme Que Sumiu Da API.strm'
sentinel.parent.mkdir(parents=True,exist_ok=True)
sentinel.write_text('conteudo-preservado\n',encoding='utf8')
existing=root/'Filmes'/'Ação'/'Filme Legal.strm'
existing.write_text('MEU LINK PERSONALIZADO NAO PODE SER SUBSTITUIDO\n',encoding='utf8')
existing_mtime=existing.stat().st_mtime_ns
# 2ª geração: todos os 7 STRM existem, logo nenhum deles pode ser regravado.
s2=run(False)
assert (s2['seriesCached'], s2['seriesFetched'], s2['filesReused'], s2['filesWritten'])==(1,0,7,0), s2
assert s2['filesPending']==0, s2
assert (s2['channels'],s2['movies'],s2['series'],s2['episodes'],s2['errors'])==(2,2,1,3,0), s2
assert existing.read_text(encoding='utf8')=='MEU LINK PERSONALIZADO NAO PODE SER SUBSTITUIDO\n'
assert existing.stat().st_mtime_ns==existing_mtime
assert sentinel.read_text(encoding='utf8')=='conteudo-preservado\n'
assert any('Ignorado (já existe' in x for x in s2['messages']), s2['messages']
assert any('Arquivos existentes ignorados' in x for x in s2['messages']), s2['messages']
# Forçar atualização consulta o servidor sem alterar arquivos .strm idênticos.
s3=run(True)
assert (s3['seriesCached'], s3['seriesFetched'], s3['filesReused'], s3['filesWritten'])==(0,1,7,0), s3
assert s3['errors']==0, s3
assert existing.read_text(encoding='utf8')=='MEU LINK PERSONALIZADO NAO PODE SER SUBSTITUIDO\n'
assert existing.stat().st_mtime_ns==existing_mtime
assert sentinel.is_file()
# 4ª geração: não selecionar Filmes/Séries não pode apagar dados de execuções anteriores.
call('/api/settings','POST',{'id':profile_id,'name':'Lista Teste','url':'http://mock:9876','username':'teste','password':'',
    'live':True,'movies':False,'series':False,'liveExtension':'ts','seriesConcurrency':8,
    'autoUpdate':True,'intervalHours':12,'autoLive':True,'autoMovies':False,'autoSeries':False})
s4=run(False)
assert s4['filesReused']==2 and s4['filesWritten']==0, s4
assert s4['errors']==0, s4
assert all((root/x).is_file() for x in expect), 'Um STRM foi apagado ao desmarcar categorias!'
assert sentinel.read_text(encoding='utf8')=='conteudo-preservado\n'
assert existing.read_text(encoding='utf8')=='MEU LINK PERSONALIZADO NAO PODE SER SUBSTITUIDO\n'
print('Cache validado: primeira consulta, segunda geração em cache e terceira forçada.')
# Testa um segundo perfil e agenda de 24h sem alterar a biblioteca anterior.
p2=call('/api/settings','POST',{'name':'Outra Lista','url':'http://mock:9876','username':'teste','password':'teste123',
  'live':True,'movies':True,'series':True,'autoUpdate':True,'intervalHours':24,'runTimeLocal':'21:00',
  'autoLive':False,'autoMovies':True,'autoSeries':False})
assert p2['id'] != profile_id and p2['intervalHours']==24 and p2['autoMovies']
assert p2['folderName']=='Outra Lista',p2
try:
    call('/api/settings','POST',{'name':'Outra: Lista','url':'http://mock:9876', 'username':'teste', 'password':'teste123', 'live':True})
except urllib.error.HTTPError as ex: assert ex.code==400
else: raise AssertionError('Conexões com nomes de pasta iguais foram aceitas!')
assert p2['runTimeLocal']=='21:00' and p2['nextRunServer'] and p2['serverTimeZone'],p2
assert len(call('/api/profiles'))==2
assert (root/'Filmes'/'Ação'/'Filme Legal.strm').is_file()

# Escrita acelerada: 750 filmes, mais do que a capacidade da fila (512),
# todos em um diretório. Repetição não pode regravar um único STRM.
stress=call('/api/settings','POST',{'name':'Teste Gravação 750','url':'http://mock:9876',
    'username':'stress','password':'stress123','live':False,'movies':True,'series':False,
    'autoUpdate':False,'intervalHours':24})
s_first=run(False, stress['id'])
assert s_first['movies']==750 and s_first['filesWritten']==750 and s_first['filesReused']==0, s_first
assert s_first['filesPending']==0 and s_first['errors']==0, s_first
stress_root=pathlib.Path(os.environ['XTREAM_TEST_MEDIA_PATH'])/stress['folderName']/'Filmes'/'Ação'
assert len(list(stress_root.glob('*.strm')))==750, 'Os 750 filmes não foram gravados'
protected=stress_root/'Filme Stress 0300.strm'
protected.write_text('LINK PRESERVADO\n',encoding='utf8')
protected_mtime=protected.stat().st_mtime_ns
s_again=run(False,stress['id'])
assert s_again['movies']==750 and s_again['filesWritten']==0 and s_again['filesReused']==750, s_again
assert s_again['filesPending']==0 and s_again['errors']==0, s_again
assert protected.read_text(encoding='utf8')=='LINK PRESERVADO\n'
assert protected.stat().st_mtime_ns==protected_mtime
assert len(list(stress_root.glob('*.strm')))==750
print('GRAVAÇÃO OTIMIZADA: 750 arquivos novos, depois 750 preservados, nenhum sobrescrito.')

# As credenciais ficam em ./config, não na imagem Docker.
config=pathlib.Path(os.environ['XTREAM_TEST_CONFIG_PATH'])
assert (config/'account.json').is_file()
assert (config/'connections.json').is_file()
assert (config/'keys').is_dir()
expected_uid = int(os.environ.get('PUID', os.getuid()))
for f in [config/'account.json', config/'connections.json', root/'Filmes'/'Ação'/'Filme Legal.strm']:
    assert f.stat().st_uid == expected_uid, f'{f} pertence a UID {f.stat().st_uid} (esperado {expected_uid})'
a=json.loads((config/'account.json').read_text())
assert a['Username']=='testeadmin' and '123456' not in (config/'account.json').read_text()
assert 'teste123' in (config/'connections.json').read_text()  # credencial Xtream persistida para sincronizar

assert call('/api/auth/logout','POST')['ok']
try: call('/api/settings')
except urllib.error.HTTPError as ex: assert ex.code==401
else: raise AssertionError('Logout não encerrou a sessão.')
print('Agenda: horários 03:15 / 04:30 em 12h e 21:00 em 24h; data, fuso e agendamento preservado validados.')
print('TESTE CONCLUÍDO: 757 arquivos .strm + sentinela preservados; fila 512, 4 gravadores, cache, atualização completa e Atividade recente.')
print('Interface de teste: http://localhost:18686 | usuário testeadmin | senha 123456')
print('Para encerrar: ./parar-teste-linux.sh')
print('Arquivos do teste mantidos em:',root)
PY
