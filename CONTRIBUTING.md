# Contributing to DRIT.Drawing

Thank you for your interest in contributing to DRIT.Drawing! This document covers the basics of getting started.

## Development Model

DRIT.Drawing is developed internally in Azure DevOps. The public GitHub repository is a filtered mirror that is synced monthly. Contributions via pull requests on GitHub are welcome and will be reviewed for inclusion in the next monthly sync.

## How to Contribute

### Reporting Bugs

1. Check the [existing issues](../../issues) to avoid duplicates.
2. Open a new issue with a clear title and description.
3. Include a minimal reproduction (code snippet, input file, expected vs. actual behavior).
4. If applicable, attach the smallest image file that reproduces the issue.

### Suggesting Features

1. Open an issue with the `enhancement` label.
2. Describe the use case and the proposed API surface.
3. Reference the relevant specification if applicable (e.g., SVG 1.1, MS-EMF, PNG spec).

### Submitting Pull Requests

1. Fork the repository and create a feature branch from `main`.
2. Keep changes focused — one feature or fix per PR.
3. Follow the existing code style. The project uses StyleCop analyzers; warnings will fail the build.
4. Add or update unit tests in `DRIT.Drawing.Tests/` to cover your changes.
5. Ensure all tests pass:
   ```bash
   dotnet test DRIT.Drawing.sln -c Release
   ```
6. Write a clear commit message following [Conventional Commits](https://www.conventionalcommits.org/):
   - `feat: add support for WebP read`
   - `fix: handle zero-length ICC profile in PNG`
   - `docs: clarify SvgImage conversion lifecycle`

## Code Style

- C# with `LangVersion` set to `latest`.
- XML documentation comments on all public types and members.
- No `var` for primitive types; prefer explicit types.
- 4-space indentation, no tabs.
- Braces on new lines (Allman style).

## Project Structure

| Directory | Purpose |
|---|---|
| `DRIT.Drawing/` | Core library — graphics, image codecs, font engine, vector model, metafile. |
| `DRIT.Drawing.Tests/` | Unit tests. |
| `DRIT.Drawing.IntegrationTests/` | Integration tests. |

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).