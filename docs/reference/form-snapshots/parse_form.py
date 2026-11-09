import re, json, sys
html = open(sys.argv[1], encoding='utf-8').read()
m = re.search(r'FB_PUBLIC_LOAD_DATA_ = (.*?);</script>', html, re.S)
d = json.loads(m.group(1))
info = d[1]
print("TITLE:", d[1][8] if len(d[1])>8 else None)
print("DESC:", info[0])
print("REQUIRES LOGIN flag:", d[18] if len(d)>18 else None)
types = {0:'short',1:'para',2:'radio',3:'dropdown',4:'checkbox',5:'scale',6:'title/section-text',7:'grid',8:'section',9:'date',10:'time',11:'image',12:'video',13:'file'}
for q in info[1]:
    qid, title, desc, typ = q[0], q[1], q[2], q[3]
    print(f"\n[{types.get(typ,typ)}] {title}  (item {qid})")
    if desc: print("   desc:", desc)
    if q[4]:
        for e in q[4]:
            entry = e[0]; req = e[2] if len(e)>2 else None
            opts = [o[0] for o in (e[1] or [])]
            nav = [o[2] if len(o)>2 else None for o in (e[1] or [])]
            print(f"   entry.{entry} required={req}")
            for o,n in zip(opts,nav): print(f"     - {o!r}" + (f"  -> goto {n}" if n not in (None,) else ""))
