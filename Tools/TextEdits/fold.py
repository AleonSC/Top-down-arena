#!/usr/bin/env python3
"""Writes text edited in the game (TextOverrides.txt, from the F4 dev text editor) into the C# code.

Each edit replaces every string literal in Assets/Scripts that is exactly the original text (with "#" in an
edit standing for any number, which the literal keeps). Edits that were written are removed from
TextOverrides.txt; the rest stay there (they still work in the game) and are listed: those are texts the code
builds from pieces (e.g. "Wave " + n), which need changing by hand.

Usage: python3 Tools/TextEdits/fold.py [--dry-run] [repo root]
"""
import os
import re
import sys

HEADER = ('#! Text edited in the game (F4). One "old<TAB>new" line each; # = a number the game fills in.\n'
          '#! Write them into the code with: python3 Tools/TextEdits/fold.py\n')


def unescape_line(s):
    out, i = [], 0
    while i < len(s):
        if s[i] == '\\' and i + 1 < len(s):
            c = s[i + 1]
            out.append('\n' if c == 'n' else '\t' if c == 't' else c)
            i += 2
        else:
            out.append(s[i])
            i += 1
    return ''.join(out)


def escape_line(s):
    return s.replace('\\', '\\\\').replace('\n', '\\n').replace('\t', '\\t')


def read_overrides(path):
    edits = []
    if not os.path.exists(path):
        return edits
    with open(path, encoding='utf-8') as f:
        for line in f.read().split('\n'):
            line = line.rstrip('\r')
            if not line or line.startswith('#!') or '\t' not in line:
                continue
            old, new = line.split('\t', 1)
            edits.append((unescape_line(old), unescape_line(new)))
    return edits


def write_overrides(path, edits):
    with open(path, 'w', encoding='utf-8') as f:
        f.write(HEADER)
        for old, new in sorted(edits):
            f.write(escape_line(old) + '\t' + escape_line(new) + '\n')


C_ESC = {'n': '\n', 't': '\t', 'r': '\r', '0': '\0', '\\': '\\', '"': '"', "'": "'"}


def decode_literal(body):
    out, i = [], 0
    while i < len(body):
        c = body[i]
        if c == '\\' and i + 1 < len(body):
            e = body[i + 1]
            if e == 'u' and i + 5 < len(body):
                out.append(chr(int(body[i + 2:i + 6], 16)))
                i += 6
                continue
            out.append(C_ESC.get(e, e))
            i += 2
        else:
            out.append(c)
            i += 1
    return ''.join(out)


def encode_literal(text):
    out = []
    for c in text:
        if c == '\\':
            out.append('\\\\')
        elif c == '"':
            out.append('\\"')
        elif c == '\n':
            out.append('\\n')
        elif c == '\t':
            out.append('\\t')
        elif c == '\r':
            out.append('\\r')
        else:
            out.append(c)
    return ''.join(out)


def string_literals(src):
    """(start, end, text) of every plain "..." literal (the quotes are src[start] and src[end - 1]),
    skipping comments, char literals and verbatim or interpolated strings."""
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c == '/' and src.startswith('//', i):
            j = src.find('\n', i)
            i = n if j < 0 else j
        elif c == '/' and src.startswith('/*', i):
            j = src.find('*/', i + 2)
            i = n if j < 0 else j + 2
        elif c == "'":
            j = i + 1
            while j < n and src[j] != "'":
                j += 2 if src[j] == '\\' else 1
            i = j + 1
        elif c == '"':
            prefix = src[max(0, i - 2):i]
            verbatim = prefix.endswith('@') or prefix == '@$' or prefix == '$@'
            j = i + 1
            while j < n:
                if verbatim and src[j] == '"':
                    if j + 1 < n and src[j + 1] == '"':
                        j += 2
                        continue
                    break
                if not verbatim and src[j] == '\\':
                    j += 2
                    continue
                if src[j] == '"' or (not verbatim and src[j] == '\n'):
                    break
                j += 1
            if not prefix.endswith('@') and not prefix.endswith('$'):
                yield i, j + 1, decode_literal(src[i + 1:j])
            i = j + 1
        else:
            i += 1


def matcher(old):
    """A function: literal text -> the edited text, or None if it isn't this edit's original."""
    if '#' not in old:
        return lambda text: None
    pattern = re.compile('^' + ''.join(r'(\d+)' if ch == '#' else re.escape(ch) for ch in old) + '$', re.S)

    def m(text):
        hit = pattern.match(text)
        return hit.groups() if hit else None
    return m


def fill(template, nums):
    out, k = [], 0
    for ch in template:
        if ch == '#' and k < len(nums):
            out.append(nums[k])
            k += 1
        else:
            out.append(ch)
    return ''.join(out)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    dry = '--dry-run' in sys.argv
    root = os.path.abspath(args[0] if args else os.path.join(os.path.dirname(__file__), '..', '..'))
    path = os.path.join(root, 'TextOverrides.txt')
    edits = read_overrides(path)
    if not edits:
        print('No edits in ' + path)
        return 0
    files = []
    for d, _, names in os.walk(os.path.join(root, 'Assets', 'Scripts')):
        files += [os.path.join(d, n) for n in names if n.endswith('.cs')]
    folded = {old: 0 for old, _ in edits}
    matchers = [(old, new, matcher(old)) for old, new in edits]
    for fp in sorted(files):
        with open(fp, encoding='utf-8') as f:
            src = f.read()
        parts, last, changed = [], 0, 0
        for start, end, text in string_literals(src):
            for old, new, m in matchers:
                repl = None
                if text == old:
                    repl = new  # no numbers in it (or the same ones)
                elif '#' in old:
                    nums = m(text)
                    if nums is not None:
                        repl = fill(new, nums)
                if repl is not None and repl != text:
                    parts.append(src[last:start])
                    parts.append('"' + encode_literal(repl) + '"')
                    last = end
                    folded[old] += 1
                    changed += 1
                    break
        if changed:
            parts.append(src[last:])
            print('  %s: %d' % (os.path.relpath(fp, root), changed))
            if not dry:
                with open(fp, 'w', encoding='utf-8') as f:
                    f.write(''.join(parts))
    left = [(old, new) for old, new in edits if folded[old] == 0]
    for old, new in edits:
        print('%s %r -> %r%s' % ('written' if folded[old] else 'KEPT   ', old, new, ' (%d places)' % folded[old] if folded[old] else
                                 '  (no literal with exactly this text: built from pieces; it stays in TextOverrides.txt)'))
    if not dry:
        write_overrides(path, left)
    print('%d of %d edits written into the code%s.' % (len(edits) - len(left), len(edits), ' (dry run: nothing changed)' if dry else ''))
    return 0


if __name__ == '__main__':
    sys.exit(main())
