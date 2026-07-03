import os
from pathlib import Path

TEMPLATE = Path(__file__).resolve().parents[1] / 'parsers' / 'templates' / 'new_parser_template.py'

def generate(name: str):
    dest = TEMPLATE.parent.parent / f"{name.lower()}.py"
    if dest.exists():
        print("Parser existe déjà:", dest)
        return
    code = TEMPLATE.read_text()
    code = code.replace('new_parser_template', name.lower())
    dest.write_text(code)
    print("Nouveau parser créé:", dest)

if __name__ == '__main__':
    import sys
    if len(sys.argv) < 2:
        print('Usage: python generate_parser.py FournisseurName')
        sys.exit(1)
    generate(sys.argv[1])
