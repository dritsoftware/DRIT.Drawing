using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DRIT.Drawing.Examples;

internal static class Program
{
    private static readonly IReadOnlyDictionary<string, Action<ExampleContext>> Examples =
        new Dictionary<string, Action<ExampleContext>>(StringComparer.OrdinalIgnoreCase)
        {
            ["CreateImage"] = GettingStartedExamples.GettingStartedCreateImage,
            ["OpenInspectSave"] = GettingStartedExamples.GettingStartedOpenInspectSave,
            ["ResourceLifetime"] = GettingStartedExamples.GettingStartedResourceLifetime,
            ["StreamRoundTrip"] = GettingStartedExamples.GettingStartedStream,
            ["LinesAndPolylines"] = DrawingExamples.ShapesLinesAndPolylines,
            ["BasicShapes"] = DrawingExamples.ShapesBasicGeometry,
            ["ArcsAndPie"] = DrawingExamples.ShapesArcsAndPie,
            ["GraphicsPath"] = DrawingExamples.ShapesGraphicsPath,
            ["PenStyles"] = BrushesExamples.PensColourAndWidth,
            ["SolidAndHatchBrushes"] = BrushesExamples.BrushesSolidAndHatch,
            ["Antialiasing"] = RenderingExamples.RenderingAntialiasing,
            ["AlphaBlending"] = RenderingExamples.RenderingAlphaBlending,
            ["Clipping"] = RenderingExamples.RenderingClipping,
            ["Transforms"] = RenderingExamples.RenderingTransforms,
            ["TextMeasurement"] = TextExamples.TextDrawAndMeasure,
            ["TextLayout"] = TextExamples.TextAlignmentWrappingAndTrimming,
            ["RasterFormats"] = ImageExamples.ImagesSupportedRasterFormats,
            ["ScaleAndCrop"] = ImageExamples.ImagesScaleAndCrop,
            ["AnimationFrames"] = AnimationExamples.AnimationInspectFrames,
            ["RecordSvg"] = VectorExamples.VectorRecordAsSvg,
            ["CaptionedPhoto"] = RecipeExamples.RecipeCaptionedPhoto,
            ["GeometryAndColours"] = GettingStartedExamples.GeometryAndColours,
            ["SystemDrawingMigration"] = GettingStartedExamples.SystemDrawingMigration,
            ["DecodeLimits"] = GettingStartedExamples.DecodeLimits,
            ["BezierCurves"] = DrawingExamples.BezierCurves,
            ["CardinalCurves"] = DrawingExamples.CardinalAndClosedCurves,
            ["PathFillRules"] = DrawingExamples.PathFillRules,
            ["RegionOperations"] = DrawingExamples.RegionOperations,
            ["PenDashesAndJoins"] = BrushesExamples.DashesAndJoins,
            ["ArrowCaps"] = BrushesExamples.ArrowCap,
            ["LinearGradients"] = BrushesExamples.LinearGradients,
            ["PathGradients"] = BrushesExamples.PathGradients,
            ["TextureBrushes"] = BrushesExamples.TextureBrushes,
            ["CombinedClips"] = RenderingExamples.CombinedClips,
            ["MatrixTransforms"] = RenderingExamples.MatrixOrderAndInverse,
            ["GraphicsState"] = RenderingExamples.SaveRestoreState,
            ["FontStyles"] = TextExamples.FontFamiliesAndStyles,
            ["PrivateFonts"] = TextExamples.PrivateFontCollection,
            ["MultilingualText"] = TextExamples.BidiAndMultilingualText,
            ["FontFallback"] = TextExamples.FontFallback,
            ["RichText"] = TextExamples.RichTextRuns,
            ["TextOnPath"] = TextExamples.TextOnPath,
            ["RotateFlipClone"] = ImageExamples.RotateFlipAndClone,
            ["PixelAccess"] = ImageExamples.ReadWritePixels,
            ["LockBits"] = ImageExamples.DirectPixelAccess,
            ["ImageAttributes"] = ImageExamples.ImageAttributes,
            ["JpegQuality"] = ImageExamples.JpegQuality,
            ["Resolution"] = ImageExamples.ResolutionAndMetadata,
            ["DecodeLimitsForUploads"] = ImageExamples.DecodeLimitsForUploads,
            ["MultiStripTiff"] = ImageExamples.MultiStripTiff,
            ["PreserveAnimation"] = AnimationExamples.PreserveAnimation,
            ["ComposeFrames"] = AnimationExamples.FrameComposition,
            ["LoadSvg"] = VectorExamples.LoadAndRasterizeSvg,
            ["SvgOptions"] = VectorExamples.SvgOutputOptions,
            ["ReuseVectorScene"] = VectorExamples.ReuseVectorScene,
            ["ReadMetafile"] = VectorExamples.ReadMetafile,
            ["RecordMetafile"] = VectorExamples.RecordMetafile,
            ["FramedImage"] = RecipeExamples.FramedImage,
            ["Callout"] = RecipeExamples.Callout,
            ["ChartCard"] = RecipeExamples.ChartCard,
            ["VectorBadge"] = RecipeExamples.ReusableBadge,
            ["PathHitTesting"] = DrawingExamples.PathHitTesting,
            ["ResourceReuse"] = BrushesExamples.ResourceReuse,
            ["PageUnits"] = RenderingExamples.UnitsAndPageCoordinates,
            ["TextRendering"] = TextExamples.TextRenderingSettings,
            ["Icon"] = ImageExamples.WorkWithIcon,
            ["ExtractFrames"] = AnimationExamples.ExtractAnimationFrames,
            ["AnimationConstruction"] = AnimationExamples.InvestigateAnimationConstruction,
            ["ConvertVector"] = VectorExamples.ConvertSvgAndMetafile,
            ["CompareMetafile"] = VectorExamples.CompareMetafileOutput,
            ["MultiFormatExport"] = VectorExamples.DrawOnceExportMultiple,
            ["BatchThumbnails"] = RecipeExamples.BatchThumbnails,
            ["AnimatedBanner"] = AnimationExamples.AnimatedBanner,
        };

    public static int Main(string[] args)
    {
        if (args.Any(IsHelp) || args.Any(arg => arg.Equals("--list", StringComparison.OrdinalIgnoreCase)))
        {
            PrintUsage();
            return 0;
        }
        string output = GetOption(args, "--output") ?? Path.Combine(Directory.GetCurrentDirectory(), "examples-output");
        var requestedIds = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                if (args[i].Equals("--output", StringComparison.OrdinalIgnoreCase)) i++;
                continue;
            }
            requestedIds.Add(args[i]);
        }
        string[] requested = requestedIds.ToArray();
        if (requested.Length == 0 || requested.Any(arg => arg.Equals("all", StringComparison.OrdinalIgnoreCase))) requested = Examples.Keys.ToArray();
        var unknown = requested.Where(id => !Examples.ContainsKey(id)).ToArray();
        if (unknown.Length > 0)
        {
            Console.Error.WriteLine($"Unknown example name(s): {string.Join(", ", unknown)}");
            PrintUsage();
            return 2;
        }
        var context = new ExampleContext(output);
        Directory.CreateDirectory(context.OutputDirectory);
        Console.WriteLine($"Writing examples to {context.OutputDirectory}");
        foreach (string id in requested)
        {
            Console.WriteLine($"[{id}]");
            Examples[id](context);
        }
        Console.WriteLine($"Completed {requested.Length} example(s).");

        if (args.Any(arg => arg.Equals("--verify", StringComparison.OrdinalIgnoreCase)))
        {
            var problems = OutputVerifier.Verify(context.OutputDirectory);
            foreach (string problem in problems)
                Console.Error.WriteLine("VERIFY FAILED: " + problem);
            Console.WriteLine(problems.Count == 0 ? "Output verified." : $"{problems.Count} output problem(s).");
            return problems.Count == 0 ? 0 : 1;
        }
        return 0;
    }

    private static bool IsHelp(string arg) => arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase);

    private static string? GetOption(string[] args, string name)
    {
        int index = Array.FindIndex(args, arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --project DRIT.Drawing.Examples -- [ExampleName ...] [--output DIR] [--verify]");
        Console.WriteLine("Run without names to execute all examples. Use --list to list the catalog.");
        Console.WriteLine("--verify checks the written files and fails when an image is blank or a single flat colour.");
        Console.WriteLine(string.Join(", ", Examples.Keys));
    }
}

