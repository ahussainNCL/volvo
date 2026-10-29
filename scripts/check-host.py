#!/usr/bin/env python3
"""Exercise the published host over real HTTP. Never prints launch/session credentials."""
import base64
import hashlib
import http.client
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.parse

ROOT = Path(__file__).resolve().parent.parent
APP = ROOT / 'artifacts/gateway/Nordlink.Gateway.dll'
checks = 0

def expect(value, message):
    global checks
    if not value:
        raise AssertionError(message)
    checks += 1

with tempfile.TemporaryDirectory(prefix='nordlink-host-') as directory:
    launch = Path(directory) / 'launch.json'
    def start():
        p = subprocess.Popen(['bash', str(ROOT / 'scripts/start.sh'), '--data-root', directory, '--launch-file', str(launch)], stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
        deadline = time.monotonic() + 20
        while not launch.exists():
            if p.poll() is not None or time.monotonic() > deadline:
                p.kill(); raise AssertionError('Published gateway did not become ready: ' + p.stderr.read().decode()[-1000:])
            time.sleep(.05)
        descriptor = json.loads(launch.read_text())
        return p, descriptor
    process, descriptor = start()
    try:
        origin = descriptor['origin']; port = urllib.parse.urlsplit(origin).port
        token = urllib.parse.parse_qs(urllib.parse.urlsplit(descriptor['url']).fragment)['session'][0]
        cookie = None
        def request(method, path, body=None, auth=True, use_origin=True, extra=None):
            headers = {}
            if auth and cookie: headers['Cookie'] = cookie
            if use_origin: headers['Origin'] = origin
            if body is not None: headers['Content-Type'] = 'application/json'
            if extra: headers.update(extra)
            conn = http.client.HTTPConnection('127.0.0.1', port, timeout=10)
            conn.request(method, path, body=None if body is None else json.dumps(body), headers=headers)
            response = conn.getresponse(); data = response.read(); status = response.status; received = dict(response.getheaders()); conn.close()
            return status, received, data
        def command(path, body):
            status, _, data = request('POST', '/api/' + path, body)
            expect(status in (200, 204), 'Command failed: ' + path + ': ' + str(status))
            return json.loads(data) if data else None
        expect(origin.startswith('http://127.0.0.1:'), 'Loopback binding')
        duplicate = subprocess.run(['dotnet', str(APP), '--data-root', directory], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=10)
        expect(duplicate.returncode != 0, 'Second gateway cannot create another scheduler for the same workspace')
        expect(request('GET', '/api/status', auth=False)[0] == 401, 'Operational auth required')
        expect(request('POST', '/api/session', {'token': token}, auth=False, use_origin=False)[0] == 403, 'Bootstrap Origin required')
        expect(request('POST', '/api/session', {'token': 'bad'}, auth=False)[0] == 401, 'Bad bootstrap rejected')
        status, headers, _ = request('POST', '/api/session', {'token': token}, auth=False)
        expect(status == 204, 'Valid bootstrap exchanged')
        cookie = headers['Set-Cookie'].split(';')[0]
        expect('httponly' in headers['Set-Cookie'].lower() and 'samesite=strict' in headers['Set-Cookie'].lower(), 'Private strict session cookie')
        expect(not launch.exists(), 'Bootstrap file removed after exchange')
        expect(request('POST', '/api/session', {'token': token}, auth=False)[0] == 401, 'Bootstrap single use')
        expect(request('GET', '/api/status', extra={'Host': 'attacker.invalid'})[0] == 403, 'Host rejected')
        expect(request('GET', '/api/status', extra={'Origin': 'https://attacker.invalid'})[0] == 403, 'Origin rejected')
        expect(request('POST', '/api/disconnect', {}, use_origin=False)[0] == 403, 'Mutation Origin required')
        expect(request('POST', '/api/disconnect', {}, extra={'Origin': 'null'})[0] == 403, 'Opaque Origin rejected')
        expect(request('GET', '/api/status', extra={'Cookie': 'nordlink-session=forged'})[0] == 401, 'Forged session rejected')
        expect(request('OPTIONS', '/api/status')[0] == 403, 'No CORS preflight')
        status, headers, html = request('GET', '/', auth=False, use_origin=False)
        expect(status == 200 and b'<div id="root">' in html and b'/assets/' in html, 'Published production frontend served')
        expect(headers.get('Cache-Control') == 'no-store' and token.encode() not in html, 'No token in cached assets')
        expect('frame-ancestors' in headers.get('Content-Security-Policy', ''), 'CSP applied')
        expect(request('GET', '/api/not-a-route')[0] == 404, 'Unknown API is not SPA fallback')
        expect(request('POST', '/hubs/progress/negotiate?negotiateVersion=1', {}, auth=False)[0] == 401, 'SignalR requires auth')
        expect(request('POST', '/hubs/progress/negotiate?negotiateVersion=1', {}, use_origin=False)[0] == 403, 'SignalR requires Origin')
        expect(request('POST', '/hubs/progress/negotiate?negotiateVersion=1', {})[0] == 200, 'SignalR auth negotiation works')
        expect(request('POST', '/api/session', {'token': None}, auth=False)[0] == 400, 'Null bootstrap is structured rejection')
        status, _, data = request('GET', '/api/status')
        expect(status == 200 and json.loads(data)['vehicle'] is None, 'No manufactured vehicle identity')
        command('environment', {'environment': 'simulation'})
        fixture = command('connect', {'driverPath': None})
        expect(fixture['vehicle']['vin'] is None and fixture['vehicle']['context']['environment'] == 'simulation', 'Simulation provenance explicit')
        expect(request('POST', '/api/environment', {'environment': 'hardware'})[0] == 409, 'Switch requires disconnect')
        review_body = {'capability': 'simulation.exercise', 'vehicleId': 'simulation-v40', 'moduleId': 'simulation-cem', 'inputs': {'note': 'HTTP validation'}}
        invalid, _, invalid_body = request('POST', '/api/operations/review', {**review_body, 'inputs': {'note': None}})
        expect(invalid == 400 and 'reason' in json.loads(invalid_body), 'Null note receives a structured error without creating an operation')
        reviewed = command('operations/review', review_body)
        result = command('operations/' + reviewed['job']['id'] + '/execute', {'approvalToken': reviewed['approvalToken']})
        expect(result['state'] == 'completed', 'Reviewed operation completed')
        expect(request('POST', '/api/operations/' + reviewed['job']['id'] + '/execute', {'approvalToken': reviewed['approvalToken']})[0] == 409, 'Approval cannot be replayed')
        captured = command('recordings/simulation-fixture', {})
        expect(len(captured['samples']) == 25 and sum(s['gap'] for s in captured['samples']) == 2, 'Recording preserves explicit gaps')
        status, _, csv = request('GET', '/api/recordings/' + captured['id'] + '/csv', use_origin=False)
        expect(status == 200 and csv.count(b',,true') == 2, 'Authenticated download without Origin preserves gaps')
        raw = b'licensed intake fixture'; artifact = {'contentBase64': base64.b64encode(raw).decode(), 'source': 'test', 'licence': 'test-only', 'moduleApplicability': ['fixture'], 'expectedSha256': hashlib.sha256(raw).hexdigest(), 'kind': 'definitions'}
        imported = command('artifacts', artifact); expect(imported['sha256'] == artifact['expectedSha256'], 'Artifact hash persisted')
        artifact['expectedSha256'] = 'bad'
        expect(request('POST', '/api/artifacts', artifact)[0] == 400, 'Hash mismatch rejected')
        expect(request('POST', '/api/capabilities/cem-algorithm/run', {})[0] == 409, 'Missing algorithm explicitly blocked')
        pending = command('operations/review', review_body)
        process.terminate(); process.wait(timeout=10)
        process, descriptor = start(); port = urllib.parse.urlsplit(descriptor['origin']).port; origin = descriptor['origin']
        expect(request('GET', '/api/status')[0] == 401, 'Prior launch session expired on restart')
        token2 = urllib.parse.parse_qs(urllib.parse.urlsplit(descriptor['url']).fragment)['session'][0]
        status, headers, _ = request('POST', '/api/session', {'token': token2}, auth=False)
        expect(status == 204, 'Fresh launch bootstrap works')
        cookie = headers['Set-Cookie'].split(';')[0]
        command('environment', {'environment': 'simulation'})
        status, _, data = request('GET', '/api/operations')
        expect(any(j['id'] == pending['job']['id'] and j['state'] == 'interrupted' for j in json.loads(data)), 'Interrupted review persisted across restart')
        command('environment', {'environment': 'hardware'})
        expect(json.loads(request('GET', '/api/vehicles')[2]) == [], 'Hardware vehicle records remain empty')
        expect(json.loads(request('GET', '/api/recordings')[2]) == [], 'Simulation captures do not enter hardware store')
        expect(json.loads(request('GET', '/api/credentials')[2]) == [], 'Routine credential endpoint contains metadata only')
    finally:
        process.terminate()
        try: process.wait(timeout=10)
        except subprocess.TimeoutExpired: process.kill(); process.wait()
    for args, overrides in [(['--urls', 'http://0.0.0.0:9999'], {}), ([], {'ASPNETCORE_URLS': 'http://0.0.0.0:9999'}), ([], {'Kestrel__Endpoints__Public__Url': 'http://0.0.0.0:9999'})]:
        rejected = subprocess.run(['dotnet', str(APP), *args], env={**os.environ, **overrides}, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=10)
        expect(rejected.returncode != 0, 'Network-wide override rejected')
print(f'Published host: {checks} HTTP, lifecycle and isolation checks passed.')
