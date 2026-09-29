"""Local static preview with the production headers and custom 404. Python 3 only."""
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import argparse

root = Path(__file__).resolve().parents[1] / 'public'
headers = []
for line in (root / '_headers').read_text().splitlines():
    if line.startswith('  ') and ':' in line:
        headers.append(tuple(part.strip() for part in line.strip().split(':', 1)))


class Preview(SimpleHTTPRequestHandler):
    def end_headers(self):
        for name, value in headers:
            self.send_header(name, value)
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

    def send_error(self, code, message=None, explain=None):
        if code != 404:
            return super().send_error(code, message, explain)
        body = (root / '404.html').read_bytes()
        self.send_response(404)
        self.send_header('Content-Type', 'text/html; charset=utf-8')
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        if self.command != 'HEAD':
            self.wfile.write(body)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=8765)
    args = parser.parse_args()
    server = ThreadingHTTPServer(('127.0.0.1', args.port), partial(Preview, directory=str(root)))
    print(f'Local preview: http://127.0.0.1:{args.port}', flush=True)
    server.serve_forever()
