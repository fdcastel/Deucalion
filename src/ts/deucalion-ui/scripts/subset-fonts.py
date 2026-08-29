#!/usr/bin/env python3
"""Regenerate the self-hosted fonts in src/styles/fonts/.

Two files ship with the page:

  ibm-plex-sans-wght.woff2   IBM Plex Sans, variable (weight axis), from @fontsource-variable/ibm-plex-sans
  ibm-plex-mono-500.woff2    IBM Plex Mono 500, static, from @fontsource/ibm-plex-mono
                             (no variable Plex Mono is published; one weight serves every mono rule)

Both are subset to the glyphs the UI can render: printable ASCII plus the handful of symbols
the components emit (non-breaking space, degree, middle dot, dashes, ellipsis, arrows, "greater or
equal"). That halves each file compared with fontsource's "latin" subset, which carries glyphs
this page never draws. Glyphs outside the subset (an accented letter in a monitor name) fall back
to the system font for that character only -- deliberate: the Latin-1 supplement alone costs
+16 kB across the two files.

Requires Python 3 with `fonttools` and `brotli` (`pip install fonttools brotli`) and npm.
Run from anywhere:  python src/ts/deucalion-ui/scripts/subset-fonts.py
"""

from __future__ import annotations

import shutil
import subprocess
import sys
import tarfile
import tempfile
from pathlib import Path

from fontTools import subset

PACKAGES = {
    # package: (file inside the tarball, output name)
    "@fontsource-variable/ibm-plex-sans@5.3.0": ("package/files/ibm-plex-sans-latin-wght-normal.woff2", "ibm-plex-sans-wght.woff2"),
    "@fontsource/ibm-plex-mono@5.3.0": ("package/files/ibm-plex-mono-latin-500-normal.woff2", "ibm-plex-mono-500.woff2"),
}

# Keep in sync with the `unicode-range` in src/styles/tokens.css.
UNICODES = "U+0020-007E,U+00A0,U+00B0,U+00B7,U+2013,U+2014,U+2026,U+2191-2193,U+2265"

FONTS_DIR = Path(__file__).resolve().parents[1] / "src" / "styles" / "fonts"


def npm_pack(package: str, into: Path) -> Path:
    npm = shutil.which("npm") or shutil.which("npm.cmd")
    if npm is None:
        sys.exit("npm not found on PATH")
    out = subprocess.run([npm, "pack", package, "--pack-destination", str(into), "--silent"], check=True, capture_output=True, text=True)
    return into / out.stdout.strip().splitlines()[-1]


def main() -> None:
    with tempfile.TemporaryDirectory() as tmp:
        tmp_path = Path(tmp)
        for package, (member, output_name) in PACKAGES.items():
            tarball = npm_pack(package, tmp_path)
            with tarfile.open(tarball) as tar:
                tar.extract(member, tmp_path / output_name, filter="data")
            source = tmp_path / output_name / member
            target = FONTS_DIR / output_name

            subset.main([
                str(source),
                f"--unicodes={UNICODES}",
                "--flavor=woff2",
                "--layout-features=kern,liga,tnum",
                f"--output-file={target}",
            ])
            print(f"{package}: {source.stat().st_size:>6} -> {target.stat().st_size:>6} bytes  {target.name}")


if __name__ == "__main__":
    main()
