"""Disposable loopback Emby 4.11 test only; requires prepared temporary admin credentials."""
from pathlib import Path
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json, threading, urllib.request, urllib.error

BASE = 'http://127.0.0.1:18775/emby/'
auth = json.loads(Path('/tmp/wtw-emby-test-auth.json').read_text())
key = auth['Token']
posts = []
status = {'auth': 200}

class FakeSeerr(BaseHTTPRequestHandler):
    def log_message(self, *args): pass
    def reply(self, value, code=200):
        body=json.dumps(value).encode()
        self.send_response(code); self.send_header('Content-Type','application/json');self.end_headers();self.wfile.write(body)
    def do_GET(self):
        if self.headers.get('X-Api-Key') != 'local-test-key': return self.reply({},401)
        if self.path == '/api/v1/auth/me': return self.reply({'id':1},status['auth'])
        return self.reply({'mediaInfo':{'status':1},'seasons':[{'seasonNumber':1,'name':'Season 1'}]})
    def do_POST(self):
        assert self.path == '/api/v1/request'
        posts.append(json.loads(self.rfile.read(int(self.headers['Content-Length']))))
        self.reply({'status':1},201)

def call(path, body=None, token=key):
    headers={'X-Emby-Token':token} if token else {}
    if body is not None: headers['Content-Type']='application/json'
    request=urllib.request.Request(BASE+path, data=json.dumps(body).encode() if body is not None else None, headers=headers)
    try:
        with urllib.request.urlopen(request,timeout=25) as response:
            assert response.status == 200, (path,response.status)
            return json.loads(response.read())
    except urllib.error.HTTPError as error:
        return {'httpStatus':error.code}

config=call('Plugins/4df372d4-8179-4f05-9312-419434bd835d/Configuration')
assert config['Options']['SeerrUrl'] == 'http://127.0.0.1:18778'
assert config['Options']['SeerrApiKey'] == 'local-test-key', 'Use disposable loopback Seerr settings only'
server=ThreadingHTTPServer(('127.0.0.1',18778),FakeSeerr)
threading.Thread(target=server.serve_forever,daemon=True).start()
try:
    assert call('JustWatch/Seerr/Test')['Success'] is True
    assert not posts
    status['auth']=401
    rejected=call('JustWatch/Seerr/Test')
    assert rejected['Success'] is False and 'local-test-key' not in rejected['Message']
    status['auth']=200
    details=call('JustWatch/Seerr/Details?TmdbId=329865&MediaType=movie')
    assert details['State']=='notRequested' and details['CanRequest']
    movie=call('JustWatch/Seerr/Request',{'TmdbId':329865,'MediaType':'movie','Seasons':[]})
    assert movie['Success'] is True and len(posts)==1
    assert call('JustWatch/Seerr/Test',token=None)['httpStatus']==401
    search=call('JustWatch/Search?Title=Arrival&Year=2016&MediaType=movie&Country=CA')
    assert search['Results'] and search['Results'][0]['Country']=='CA'
    print('Authenticated Emby checks passed: JSON connection success/auth failure, status, fake request, anonymous rejection, and live title search. No real Seerr requests.')
finally: server.shutdown();server.server_close()
