import json
import os

path = os.path.expandvars(r'%USERPROFILE%\AppData\LocalLow\DefaultCompany\AICO\config\settings.json')
if os.path.exists(path):
    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    data['server_id'] = 'arona655'
    data['server_type_idx'] = 10
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=4)
    print("Success")
else:
    print("File not found:", path)
