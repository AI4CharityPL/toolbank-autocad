import json, collections
p=r'C:\Users\DELL\.cursor\projects\c-Users-DELL-Dev-autocad-mcp\agent-tools\5e77dffc-1115-4177-901d-59226b975cb3.txt'
d=json.load(open(p,encoding='utf-8'))
logs=d['IterationLogs'][0]['Steps']
layers=logs[0]['output']
ents=logs[1]['output']
print('layers step keys:', list(layers.keys())[:20] if isinstance(layers,dict) else type(layers))
print('ents step keys:', list(ents.keys())[:20] if isinstance(ents,dict) else type(ents))
