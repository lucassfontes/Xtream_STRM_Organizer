#!/usr/bin/env python3
"""Servidor Xtream fake para testar o programa sem lista real."""
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

CATEGORIES = {
    'get_live_categories': [
        {'category_id':'1','category_name':'BRASIL'},
        {'category_id':'2','category_name':'XXX'},
    ],
    'get_vod_categories': [
        {'category_id':'10','category_name':'Ação'},
        {'category_id':'11','category_name':'18+'},
    ],
    'get_series_categories': [
        {'category_id':'20','category_name':'Drama'},
    ],
    'get_live_streams': [
        {'stream_id':101,'name':'Canal Teste','category_id':'1'},
        {'stream_id':102,'name':'Canal Adulto','category_id':'2'},
    ],
    'get_vod_streams': [
        {'stream_id':201,'name':'Filme Legal','category_id':'10','container_extension':'mkv'},
        {'stream_id':202,'name':'Filme Restrito','category_id':'11','container_extension':'mp4'},
    ],
    'get_series': [
        {'series_id':301,'name':'Série Teste','category_id':'20','last_modified':'1720000000'},
    ],
    'get_series_info': {
        'episodes': {
            '1':[
                {'id':'4001','episode_num':1,'title':'O começo','container_extension':'mp4'},
                {'id':'4002','episode_num':10,'title':'O final','container_extension':'mkv'},
            ],
            '2':[
                {'id':'4003','episode_num':1,'title':'Nova fase','container_extension':'mp4'},
            ],
        }
    }
}

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        path=urlparse(self.path)
        query=parse_qs(path.query)
        username=query.get('username',[''])[0]
        password=query.get('password',[''])[0]
        if path.path != '/player_api.php' or (username,password) not in (('teste','teste123'),('stress','stress123')):
            self.send_response(403)
            self.end_headers()
            return
        action=query.get('action',[''])[0]
        result=CATEGORIES.get(action, {'user_info':{'auth':1,'status':'Active'}})
        if username=='stress' and action=='get_vod_streams':
            result=[{'stream_id':10000+i,'name':f'Filme Stress {i:04d}', 'category_id':'10', 'container_extension':'mp4'} for i in range(750)]
        content=json.dumps(result,ensure_ascii=False).encode('utf8')
        self.send_response(200)
        self.send_header('Content-Type','application/json; charset=utf-8')
        self.send_header('Content-Length',str(len(content)))
        self.end_headers()
        self.wfile.write(content)
    def log_message(self,*args): pass

if __name__ == '__main__':
    ThreadingHTTPServer(('0.0.0.0',9876),Handler).serve_forever()
