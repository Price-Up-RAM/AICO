import urllib.request
import urllib.error

req = urllib.request.Request('https://arona655.60000123.xyz/health')
try:
    resp = urllib.request.urlopen(req)
    print(resp.read().decode('utf-8', errors='ignore'))
except urllib.error.HTTPError as e:
    print(e.code)
    print(e.read().decode('utf-8', errors='ignore'))
