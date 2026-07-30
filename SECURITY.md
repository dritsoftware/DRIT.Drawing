# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in DRIT.Drawing, please report it responsibly:

1. **Do not** open a public GitHub issue.
2. Email **security@dritsoftware.com** with a description of the vulnerability and, if possible, a proof of concept.
3. You will receive an acknowledgment within 48 hours.

We ask that you give us a reasonable window to investigate and publish a fix before disclosing the issue publicly.

## Scope

The following are in scope:

- Decompression bombs, integer overflows, or other memory-safety issues in the image codecs (PNG, JPEG, GIF, TIFF, BMP, ICO, APNG).
- Parsing flaws in the font engine (TrueType, OpenType, CFF, Type1, WOFF, WOFF2).
- Parsing flaws in the metafile engine (WMF, EMF, EMF+).
- XML external entity (XXE) or other injection vulnerabilities in the SVG reader.
- Path traversal or arbitrary file write via any API.

The following are out of scope:

- Vulnerabilities in dependencies (DRIT.Drawing has none).
- Issues requiring a malicious font file under the attacker's control (the font engine trusts font data within format bounds).

## Supported Versions

Only the latest release on the `main` branch receives security fixes.