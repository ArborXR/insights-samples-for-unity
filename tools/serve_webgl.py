#!/usr/bin/env python3
"""Serve a WebGL build locally with caching disabled, so browsers always load the latest build.

The build's files are Brotli-compressed (.br). A browser only reads them when the server names the compression
in Content-Encoding, which this one does; most static servers don't.

Usage:
    python3 tools/serve_webgl.py [BUILD_DIR] [--port 8765] [--bind 127.0.0.1]

BUILD_DIR defaults to Builds/WebGL-local. Open http://localhost:8765/ and add deep-link parameters,
for example ?sample=02-assessments&autopilot=pass.
"""
import argparse
import functools
import http.server
import os

CONTENT_ENCODINGS = {".br": "br", ".gz": "gzip"}


class BuildHandler(http.server.SimpleHTTPRequestHandler):
    content_encoding = None

    def send_head(self):
        self.content_encoding = None
        return super().send_head()

    def guess_type(self, path):
        # Build.wasm.br is application/wasm, compressed: the type comes from the name without .br.
        inner, extension = os.path.splitext(path)
        self.content_encoding = CONTENT_ENCODINGS.get(extension)
        return super().guess_type(inner if self.content_encoding else path)

    def send_error(self, *args, **kwargs):
        self.content_encoding = None
        super().send_error(*args, **kwargs)

    def end_headers(self):
        if self.content_encoding:
            self.send_header("Content-Encoding", self.content_encoding)
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("build_dir", nargs="?", default="Builds/WebGL-local")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--bind", default="127.0.0.1")
    args = parser.parse_args()

    directory = os.path.abspath(args.build_dir)
    if not os.path.isfile(os.path.join(directory, "index.html")):
        parser.error(f"no index.html in {directory}; build first (Insights Samples > Build > WebGL)")

    handler = functools.partial(BuildHandler, directory=directory)
    with http.server.ThreadingHTTPServer((args.bind, args.port), handler) as server:
        print(f"Serving {directory} at http://{args.bind}:{args.port}/", flush=True)
        server.serve_forever()


if __name__ == "__main__":
    main()
