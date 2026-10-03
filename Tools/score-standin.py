"""A stand-in for the score service (contract v1), for the result page's plate and the boards (#723, #734).

  python Tools/score-standin.py <port> <post-delay-seconds> [board-delay-seconds]

POST /v1/scores answers 201 after the delay with the rank body; GET /v1/boards/<file> answers a three-row page.
Nothing here ever reaches the live service: it listens on loopback only.
"""
import json
import sys
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PORT = int(sys.argv[1])
POST_DELAY = float(sys.argv[2])
BOARD_DELAY = float(sys.argv[3]) if len(sys.argv) > 3 else 0.0


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
