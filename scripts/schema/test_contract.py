"""Mutation probes for the fast contract gate; these must fail for the actual reasons."""
from copy import deepcopy
from pathlib import Path
import json
import unittest
from validate_contract import validate

CONTRACT = json.loads((Path(__file__).resolve().parents[2] / 'docs/schema/schema-contract.json').read_text(encoding='utf-8'))


class ContractGateTests(unittest.TestCase):
    def reject(self, mutation, message):
        contract = deepcopy(CONTRACT)
        mutation(contract)
        errors, _ = validate(contract)
        self.assertTrue(any(message in error for error in errors), errors)

    def test_valid_contract(self):
        self.assertEqual(([], []), validate(CONTRACT))

    def test_duplicate_table(self):
        self.reject(lambda c: c['tables'].append(deepcopy(c['tables'][0])), 'Duplicate table')

    def test_approximate_number_requires_exact_wire_exception(self):
        self.reject(lambda c: next(t for t in c['tables'] if t['name']=='XpBalance')['columns'][7].update(type='float(53)'), 'approximate numerical type')
        self.reject(lambda c: next(x for t in c['tables'] if t['name']=='SyncAnchor' for x in t['columns'] if x['name']=='LastElapsedMilliseconds').update(type='real'), 'approximate numerical type')

    def test_missing_table_description(self):
        self.reject(lambda c: c['tables'][0].update(description=''), 'missing row/table description')

    def test_missing_column_description(self):
        self.reject(lambda c: c['tables'][0]['columns'][0].update(description=''), 'missing column description')

    def test_foreign_key_type_drift(self):
        self.reject(lambda c: next(t for t in c['tables'] if t['name']=='SystemQuest')['columns'][1].update(type='varchar(41)'), 'FK type mismatch')

    def test_foreign_key_collation_drift(self):
        self.reject(lambda c: next(t for t in c['tables'] if t['name']=='SystemQuest')['columns'][1].update(collation='DATABASE_DEFAULT'), 'FK collation mismatch')

    def test_missing_foreign_key_index(self):
        self.reject(lambda c: next(t for t in c['tables'] if t['name']=='SystemQuest').update(indexes=[]), 'missing ordered leading-prefix FK index')

    def test_duplicate_index(self):
        def mutation(c):
            table=next(t for t in c['tables'] if t['name']=='SystemQuest')
            index=deepcopy(table['indexes'][0])
            index['name']='IX_Test_Duplicate'
            table['indexes'].append(index)
        self.reject(mutation, 'duplicate index shape')


if __name__ == '__main__':
    unittest.main()
