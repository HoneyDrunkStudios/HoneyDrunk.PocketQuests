"""Executable local structural gate. No database/network access, no file mutations."""
from pathlib import Path
import argparse
import json
import re
import sys

ROOT=Path(__file__).resolve().parent

def validate(data):
    errors=[]; reviews=[]; seen=set()
    tables={(t['schema'],t['name']):t for t in data['tables']}
    if len(tables) != len(data['tables']): errors.append('Duplicate table identity')
    external={(t['schema'],t['name']):t for t in data.get('external_tables',[])}
    def fail(msg): errors.append(msg)
    def named(name):
        if name in seen: fail('Duplicate schema object name: '+name)
        seen.add(name)
        if len(name)>128: fail('SQL object name exceeds 128 characters: '+name)
    for t in data['tables']:
        label=t['schema']+'.'+t['name']; cs={c['name']:c for c in t['columns']}
        if len(cs)!=len(t['columns']): fail(label+': duplicate column name')
        if not t.get('description','').strip() or 'One row is' not in t['description']: fail(label+': missing row/table description')
        if not t.get('retention') or not t.get('classification') or not t.get('kind'): fail(label+': missing lifecycle/classification/history contract')
        for c in t['columns']:
            if not c.get('description','').strip(): fail(label+'.'+c['name']+': missing column description')
            if c['nullable'] and 'null' not in c['description'].lower(): fail(label+'.'+c['name']+': nullable meaning not described')
            if c['type'].split('(')[0] in ('float','real') and not (
                label == 'pocketquests.SyncAnchor' and c['name'] == 'LastElapsedMilliseconds'
                and c['type'] == 'float(53)' and 'IEEE-754 double' in c['description']):
                fail(label+'.'+c['name']+': approximate numerical type')
            if c['default'] is not None: named('DF_'+t['name']+'_'+c['name'])
        keys=[t['primary_key'],*t['unique_keys']]
        for k in keys:
            named(k['name'])
            if not k['columns'] or len(k['columns'])!=len(set(k['columns'])): fail(k['name']+': empty/repeated key columns')
            for c in k['columns']:
                if c not in cs: fail(k['name']+': unknown key column '+c)
        for ck in t['checks']:
            named(ck['name'])
            if not ck['expression'] or not ck.get('purpose'): fail(ck['name']+': missing check expression/purpose')
            expression_without_literals=re.sub(r"N?'(?:''|[^'])*'", "''", ck['expression'])
            for c in re.findall(r'\[([^]]+)\]',expression_without_literals):
                if c not in cs: fail(ck['name']+': unknown check column '+c)
        all_indexes=[dict(k,filter=None,include=[],unique=True) for k in keys]+t['indexes']
        signatures={}
        for i in all_indexes:
            if i in t['indexes']:
                named(i['name'])
                if not i.get('purpose','').strip(): fail(i['name']+': missing query/FK/invariant justification')
            for c in i['columns']+i.get('include',[]):
                if c not in cs: fail(i['name']+': unknown index column '+c)
            sig=(tuple(i['columns']),i.get('filter'),tuple(sorted(i.get('include',[]))))
            if sig in signatures: fail(i['name']+': duplicate index shape of '+signatures[sig])
            signatures[sig]=i['name']
        referenced=set()
        for f in t['foreign_keys']:
            named(f['name']); referenced.update(f['columns'])
            if f['delete_action']!='NO ACTION': fail(f['name']+': unreviewed cascade')
            parent_key=(f['referenced_schema'],f['referenced_table']); parent=tables.get(parent_key)
            if parent:
                pcols={c['name']:c['type'] for c in parent['columns']}
                pcollations={c['name']:c.get('collation') for c in parent['columns']}
                pkeys=[parent['primary_key']['columns']]+[u['columns'] for u in parent['unique_keys']]
            elif parent_key in external:
                pcols=external[parent_key]['columns']; pkeys=external[parent_key]['unique_keys']
                pcollations=external[parent_key].get('collations',{})
            else:
                fail(f['name']+': missing referenced table'); continue
            if f['referenced_columns'] not in pkeys: fail(f['name']+': reference has no exact unfiltered unique key')
            if len(f['columns'])!=len(f['referenced_columns']): fail(f['name']+': FK arity mismatch'); continue
            for child,parentcol in zip(f['columns'],f['referenced_columns']):
                if child not in cs or parentcol not in pcols: fail(f['name']+': unknown child/parent column'); continue
                if cs[child]['type']!=pcols[parentcol]: fail(f['name']+': FK type mismatch '+child+' / '+parentcol)
                if cs[child].get('collation')!=pcollations.get(parentcol): fail(f['name']+': FK collation mismatch '+child+' / '+parentcol)
            def eligible(i):
                if i['columns'][:len(f['columns'])]!=f['columns']: return False
                pred=i.get('filter')
                if pred is None: return True
                return pred in ('['+c+'] IS NOT NULL' for c in f['columns'])
            if not any(eligible(i) for i in all_indexes): fail(f['name']+': missing ordered leading-prefix FK index')
        for c in t['columns']:
            if c['name'].endswith('Id') and c['name']!='Id' and c['name'] not in referenced and not c.get('external_source'):
                fail(label+'.'+c['name']+': reference-looking column has no FK or external source')
        # Prefix coverage is review-only: uniqueness, selectivity and hot read cost can differ.
        for i in t['indexes']:
            if i['unique']: continue
            for j in all_indexes:
                if i['name']==j['name'] or i.get('filter')!=j.get('filter'): continue
                if len(j['columns'])>len(i['columns']) and j['columns'][:len(i['columns'])]==i['columns']:
                    reviews.append(i['name']+' is a prefix of '+j['name']+'; retain only for measured read cost or drop after plan review.')
    return errors,sorted(set(reviews))

def main():
    p=argparse.ArgumentParser(); p.add_argument('--contract',type=Path,default=ROOT.parents[1]/'docs/schema/schema-contract.json'); args=p.parse_args()
    data=json.loads(args.contract.read_text(encoding='utf-8')); errors,reviews=validate(data)
    print(json.dumps({'status':'FAIL' if errors else 'PASS','tables':len(data['tables']),
        'columns':sum(len(t['columns']) for t in data['tables']),'errors':errors,'reviews':reviews},indent=2))
    return 1 if errors else 0

if __name__=='__main__': sys.exit(main())
