from pathlib import Path
import hashlib
import json
from jsonschema import Draft202012Validator, FormatChecker
from openapi_spec_validator import validate_spec

root = Path(__file__).resolve().parents[1]
contract = root / 'contracts/ai/v1/openapi.yaml'
spec = json.loads(contract.read_text(encoding='utf-8'))
validate_spec(spec)
for case in json.loads((contract.parent / 'fixtures.json').read_text()):
    schema = {'$ref': '#/components/schemas/Classification' + case['kind'].title(), 'components': spec['components']}
    errors = list(Draft202012Validator(schema, format_checker=FormatChecker()).iter_errors(case['payload']))
    assert (not errors) == case['valid'], case.get('name', case['kind'])
lock_path = root / 'contract.lock.json'
if lock_path.exists():
    lock = json.loads(lock_path.read_text())
    assert lock['version'] == spec['info']['version']
    assert lock['sha256'] == hashlib.sha256(contract.read_bytes()).hexdigest()
print('OpenAPI 3.1, fixtures and pinned checksum validated.')
