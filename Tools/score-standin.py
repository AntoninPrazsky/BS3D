"""A stand-in for the score service (contract v1), for the result page's plate and the boards (#723, #734).

  python Tools/score-standin.py <port> <post-delay-seconds> [board-delay-seconds [placed [first-empty]]]

POST /v1/scores answers 201 after the delay with the rank body; GET /v1/boards/<file> answers a three-row page.
GET /v1/boards?period=month|all (#685, #898) answers the overview the High Scores page reads: a board for every level of
Game/Levels/Levels.json, keyed as the game keys it (the level file's hash), each with a leader.
`placed` names the levels the asker has a place on (a count N for the first N, or ranges such as 1-5,7-23, 1-based;
default: none), `first-empty` the first level, 1-based, from which no board exists - a level nobody has played (default:
none). The month's overview leaves out every fifth level.
Nothing here ever reaches the live service: it listens on loopback only.
"""
import hashlib
import json
import os
import sys
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PORT = int(sys.argv[1])
POST_DELAY = float(sys.argv[2])
BOARD_DELAY = float(sys.argv[3]) if len(sys.argv) > 3 else 0.0


def parse_levels(text):
    """'23' is levels 1..23; '1-5,7-23' is those ranges; both 1-based."""
    if text.isdigit():
        return set(range(1, int(text) + 1))
    placed = set()
    for part in text.split(','):
        low, _, high = part.partition('-')
        placed.update(range(int(low), int(high or low) + 1))
    return placed


PLACED = parse_levels(sys.argv[4]) if len(sys.argv) > 4 else set()
FIRST_EMPTY = int(sys.argv[5]) if len(sys.argv) > 5 else 10 ** 9

LEVELS = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Game', 'Levels')
NAMES = ['RDT', 'Tereza', 'Kuba', 'Mira', 'Rudi', 'Bepu', 'Anezka', 'Cannonball', 'Zdenek', 'Pavlina']


def level_key(entry):
    """LevelIdentity.Of: the file's bytes less every CR, then one line of the entry's rules, SHA-256, 16 hex characters."""
    def rule(name):
        return str(entry[name]) if entry.get(name) is not None else '-'

    with open(os.path.join(LEVELS, entry['file']), 'rb') as f:
        data = f.read().replace(bytes([13]), b'')
    rules = chr(10) + 'shots=' + rule('shots') + ';ceilingStep=' + rule('ceilingStep') + ';wildcardEvery=' + rule('wildcardEvery')
    return entry['file'], hashlib.sha256(data + rules.encode()).hexdigest()[:16]


def summary(period, player):
    with open(os.path.join(LEVELS, 'Levels.json'), encoding='utf-8') as f:
        entries = json.load(f)['levels']
    month = period != 'all'
    boards = []
    for i, entry in enumerate(entries):
        if i + 1 >= FIRST_EMPTY or (month and i % 5 == 4):
            continue
        file, digest = level_key(entry)
        top = 18000 + (i * 7919) % 31000 + (0 if month else 4000)
        board = {'file': file, 'hash': digest, 'rules': 1, 'total': 6 + (i * 5) % 40 + (0 if month else 30),
                 'top': {'rank': 1, 'name': NAMES[(i * 3) % len(NAMES)], 'score': top, 'stars': 3 + i % 2}}
        if player and i + 1 in PLACED:
            board['me'] = {'rank': 1 + i % 7, 'score': top - 900 * (i % 7), 'stars': 1 + i % 4}
        boards.append(board)
    return {'period': 'month' if month else 'all', 'month': '2026-10' if month else None, 'boards': boards}


class Handler(BaseHTTPRequestHandler):
    protocol_version = 'HTTP/1.1'

    def _send(self, status, body):
        data = json.dumps(body).encode()
        self.send_response(status)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_POST(self):
        length = int(self.headers.get('Content-Length', 0))
        raw = self.rfile.read(length)
        try:
            body = json.loads(raw)
        except Exception:
            body = {}
        print('POST', self.path, body.get('level'), body.get('score'), body.get('stars'), flush=True)
        time.sleep(POST_DELAY)
        self._send(201, {'accepted': True, 'personalBest': True, 'name': body.get('name', 'Test'),
                         'month': {'rank': 2, 'total': 3}, 'allTime': {'rank': 2, 'total': 3}})

    def do_GET(self):
        print('GET', self.path, flush=True)
        time.sleep(BOARD_DELAY)
        if self.path.startswith('/v1/boards?'):
            self._send(200, summary('all' if 'period=all' in self.path else 'month', 'player=' in self.path))
            return
        month = 'period=month' in self.path or 'period=all' not in self.path
        self._send(200, {
            'period': 'month' if month else 'all', 'month': '2026-10' if month else None, 'total': 3,
            'entries': [{'rank': 1, 'name': 'RDT', 'score': 41200, 'stars': 4},
                        {'rank': 2, 'name': 'Test', 'score': 30500, 'stars': 3},
                        {'rank': 3, 'name': 'Tereza', 'score': 12000, 'stars': 2}],
            'me': {'rank': 2, 'score': 30500, 'stars': 3}})

    def log_message(self, *a):
        pass


ThreadingHTTPServer(('127.0.0.1', PORT), Handler).serve_forever()
