# DRIT.Drawing examples

This project contains runnable DRIT.Drawing examples. Source files are grouped by category and use original, generated inputs.

Run all examples:

```powershell
dotnet run --project DRIT.Drawing.Examples
```

Run selected examples or choose an output directory:

```powershell
dotnet run --project DRIT.Drawing.Examples -- CreateImage GraphicsPath RecordSvg --output .\examples-output
```

The program prints the selected example names and writes PNG, SVG, GIF, JPEG, BMP, and TIFF outputs where applicable. These examples target .NET 8 and consume the public `DRIT.Drawing` project reference.

## Verify the examples

Run every example and verify its output:

```powershell
dotnet run --project DRIT.Drawing.Examples -- all --verify --output ./examples-output
```

The `--verify` option fails when an example writes a blank or single-colour image. The same check runs in the integration tests.

Text output depends on the installed fonts, so letterforms can differ slightly between machines.
