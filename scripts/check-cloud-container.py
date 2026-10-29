#!/usr/bin/env python3
"""Exercise the built production image on a random loopback-published port; no secret output."""
import http.client
import json
import os
import secrets
import subprocess
import time

docker = ['docker', '--host=unix:///var/run/docker.sock']
environment = dict(os.environ)
for key in ['DOCKER_HOST', 'DOCKER_CONTEXT', 'DOCKER_TLS', 'DOCKER_TLS_VERIFY', 'DOCKER_CERT_PATH']:
    environment.pop(key, None)
environment['NORDLINK_ADMIN_PASSWORD'] = secrets.token_hex(32)
environment['NORDLINK_PUBLIC_ORIGIN'] = 'https://nordlink.example'
name = 'nordlink-cloud-check-' + secrets.token_hex(6)
def command(*args):
    return subprocess.check_output([*docker, *args], env=environment, text=True).strip()
checks = 0
def expect(condition, message):
    global checks
    if not condition: raise AssertionError(message)
    checks += 1
try:
    command('run', '--detach', '--rm', '--name', name, '-p', '127.0.0.1::8080',
        '-e', 'PORT=8080', '-e', 'NORDLINK_ADMIN_PASSWORD', '-e', 'NORDLINK_PUBLIC_ORIGIN', 'nordlink-cloud:local')
    port = int(command('port', name, '8080/tcp').rsplit(':', 1)[1])
    cookie = None
    def request(method, path, body=None, override=None):
        headers = {'Host': 'nordlink.example', 'Origin': environment['NORDLINK_PUBLIC_ORIGIN']}
        if cookie: headers['Cookie'] = cookie
        if body is not None: headers['Content-Type'] = 'application/json'
        if override: headers.update(override)
        connection = http.client.HTTPConnection('127.0.0.1', port, timeout=5)
        connection.request(method, path, json.dumps(body) if body is not None else None, headers)
        response = connection.getresponse(); status = response.status; headers = dict(response.getheaders()); data = response.read(); connection.close()
        return status, headers, data
    for attempt in range(100):
        try:
            if request('GET', '/health')[0] == 200: break
        except OSError: pass
        time.sleep(.1)
    else: raise AssertionError('Production container failed readiness')
    expect(command('exec', name, 'id', '-u') != '0', 'Image must run as a non-root user')
    status, headers, html = request('GET', '/')
    expect(status == 200 and b'/assets/' in html, 'Container serves production UI')
    expect(headers.get('Cache-Control') == 'no-store', 'No credential/session caching')
    expect(request('GET', '/api/status')[0] == 401, 'Operational authentication required')
    expect(request('GET', '/health', override={'Host': 'wrong.invalid'})[0] == 403, 'Host validated behind router')
    expect(request('POST', '/api/cloud/login', {'password': 'invalid'})[0] == 401, 'Invalid sign-in rejected')
    status, headers, _ = request('POST', '/api/cloud/login', {'password': environment['NORDLINK_ADMIN_PASSWORD']})
    expect(status == 204, 'Production sign-in works')
    cookie = headers['Set-Cookie'].split(';')[0]
    expect(all(flag in headers['Set-Cookie'].lower() for flag in ['secure', 'httponly', 'samesite=strict']), 'Production cookie requires TLS/HttpOnly/strict same-site')
    expect(request('GET', '/api/cloud/session')[0] == 200, 'Signed-in cloud session works')
    expect(request('POST', '/api/cloud/pairing', {})[0] == 200, 'Production pairing can be created')
    expect(request('POST', '/api/credentials/test/reveal', {})[0] == 409, 'Remote PIN reveal blocked')
    expect(request('GET', '/api/features')[0] == 200, 'Authenticated feature register served')
    expect(request('GET', '/api/status')[0] == 503, 'No manufactured hardware result without a helper')
    expect(request('POST', '/api/cloud/logout', {})[0] == 200, 'Logout works')
    expect(request('GET', '/api/cloud/session')[0] == 401, 'Signed-out session rejected')
    print(f'Production cloud container: {checks} checks passed.')
finally:
    subprocess.run([*docker, 'rm', '--force', name], env=environment, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
