import json, sys
d = json.load(open(sys.argv[1], encoding='utf-8'))
img = d['images'][0]
print(f"score={img['score']} verdict={img['verdict']} gaps={len(img['fatal_gaps'])}")
print(f"threshold_note: {img['threshold_note']}")
print()
for c in img['criteria']:
    print(f"{c['id']:2d}. {c['label']:<20} {c['score']:.1f}  {c['note'][:180]}")
