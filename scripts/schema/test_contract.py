"""Mutation probes for the fast contract gate; these must fail for the actual reasons."""
from copy import deepcopy
from pathlib import Path
import json
import unittest
import argparse
import tempfile
import os
from unittest.mock import patch
from validate_contract import validate, contract_path
from generate_schema import compile_metadata, metadata_sql, table_sql, main as generate_main

CONTRACT = json.loads((Path(__file__).resolve().parents[2] / 'docs/schema/schema-contract.json').read_text(encoding='utf-8'))


class ContractGateTests(unittest.TestCase):
    def test_metadata_timestamp_changes_only_when_descriptions_change(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'metadata.sql'
            self.assertEqual(0, compile_metadata(CONTRACT, output))
            old_timestamp = 1_600_000_000_000_000_000
            os.utime(output, ns=(old_timestamp, old_timestamp))
            self.assertEqual(0, compile_metadata(CONTRACT, output))
            self.assertEqual(old_timestamp, output.stat().st_mtime_ns)
            changed = deepcopy(CONTRACT)
            changed['tables'][0]['description'] += ' Updated description.'
            self.assertEqual(0, compile_metadata(changed, output))
            self.assertNotEqual(old_timestamp, output.stat().st_mtime_ns)
            self.assertIn('Updated description.', output.read_text(encoding='utf-8'))

    def test_metadata_preserves_all_reviewed_descriptions(self):
        sql = metadata_sql(CONTRACT)
        expected = sum(1 + len(t['columns']) + len(t['indexes']) for t in CONTRACT['tables'])
        self.assertEqual(expected, sql.count('EXEC sys.sp_addextendedproperty'))
        for table in CONTRACT['tables']:
            self.assertNotIn('sp_addextendedproperty', table_sql(table))
            descriptions = [table['description']]
            descriptions.extend(c['description'] for c in table['columns'])
            descriptions.extend(i['purpose'] for i in table['indexes'])
            for description in descriptions:
                self.assertIn("@value=N'" + description.replace("'", "''") + "'", sql)

    def test_metadata_compilation_escapes_unicode_and_apostrophes(self):
        contract = deepcopy(CONTRACT)
        contract['tables'][0]['description'] = "Owner's progress – retained history"
        self.assertIn("@value=N'Owner''s progress – retained history'", metadata_sql(contract))

    def test_metadata_command_writes_only_build_output_and_rejects_invalid_contract(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'obj' / 'descriptions.sql'
            with patch('sys.argv', ['generate_schema.py', '--metadata-output', str(output)]), patch('generate_schema.outputs') as sources:
                self.assertEqual(0, generate_main())
                sources.assert_not_called()
            original = output.read_text(encoding='utf-8')
            self.assertEqual(metadata_sql(CONTRACT), original)
            invalid = deepcopy(CONTRACT)
            invalid['tables'][0]['description'] = ''
            contract_file = Path(directory) / 'invalid.json'
            contract_file.write_text(json.dumps(invalid), encoding='utf-8')
            with patch('sys.argv', ['generate_schema.py', '--metadata-output', str(output)]), patch('generate_schema.CONTRACT', contract_file):
                self.assertEqual(1, generate_main())
            self.assertEqual(original, output.read_text(encoding='utf-8'))

    def test_contract_path_is_confined_after_resolution(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            repository=root/'repository'
            repository.mkdir()
            inside=repository/'contract.json'
            inside.write_text('{}',encoding='utf-8')
            outside=root/'outside.json'
            outside.write_text('{}',encoding='utf-8')
            with patch('validate_contract.REPOSITORY',repository.resolve()):
                self.assertEqual(inside.resolve(),contract_path(inside))
                with self.assertRaises(argparse.ArgumentTypeError):
                    contract_path(repository/'..'/'outside.json')
                with self.assertRaises(argparse.ArgumentTypeError):
                    contract_path(outside)

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
