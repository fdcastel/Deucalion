# Self-hosted fonts

The default theme uses IBM Plex Sans (display + UI) and IBM Plex Mono. They are
vendored here so the status page paints without a third-party stylesheet -- the
documented deployment is an intranet Docker container, and a blocking Google
Fonts request stalls first paint on an air-gapped network (issue #28).

## Files

Latin subset only, `normal` style, one static instance per weight actually
referenced by the stylesheets (`400`, `500`, `600`). No italics: the IBM Plex
preset renders the display accents upright (`--display-italic: normal`).

| File                                  | Bytes  | SHA-256 (first 16)  |
| ------------------------------------- | -----: | ------------------- |
| `ibm-plex-sans-latin-400-normal.woff2` | 22 588 | `3b646991d30055a9` |
| `ibm-plex-sans-latin-500-normal.woff2` | 24 184 | `0717336fb31fcdcd` |
| `ibm-plex-sans-latin-600-normal.woff2` | 24 252 | `8960851d691c054e` |
| `ibm-plex-mono-latin-400-normal.woff2` | 14 708 | `08949f728dc52d52` |
| `ibm-plex-mono-latin-500-normal.woff2` | 14 888 | `01d285447409c8a5` |
| `ibm-plex-mono-latin-600-normal.woff2` | 15 620 | `0d1f0b8d0722224e` |

The `@font-face` rules live in `../tokens.css`. Vite content-hashes the files
into `dist/assets/`, so they ride the same immutable-cache path as the JS and
CSS bundles. WOFF2 is already Brotli-compressed internally, so no `.br`/`.gz`
sidecars are generated for them (the compression plugin's default filter only
matches `js|mjs|json|css|html`).

## Provenance

Copied verbatim (one-time `npm pack`, not a dependency) from:

- `@fontsource/ibm-plex-sans@5.3.0` -- `files/ibm-plex-sans-latin-{400,500,600}-normal.woff2`
- `@fontsource/ibm-plex-mono@5.3.0` -- `files/ibm-plex-mono-latin-{400,500,600}-normal.woff2`

The `unicode-range` in `tokens.css` is the one those packages declare for their
`latin` subset. To refresh, `npm pack` a newer version into a scratch folder,
copy the same six files, and update this table.

## License

IBM Plex is licensed under the SIL Open Font License 1.1 -- see [LICENSE](LICENSE).
Copyright 2017-2019 IBM Corp.

## The other families in the tweaks panel

Newsreader, Inter, JetBrains Mono, Space Grotesk and Space Mono are **not**
vendored. They are fetched from Google Fonts on demand, only after a user picks
them in the (console-summoned) tweaks panel -- see `google` in
`src/components/tweaks/fonts.ts`. The default page never contacts a third
party; on an air-gapped network those faces simply fall back to their stacks.
