// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Microsoft.Docs.Build;

internal class PdfBuilder
{
    private readonly ErrorBuilder _errors;
    private readonly Output _output;
    private readonly TocMap _tocMap;
    private readonly TocLoader _tocLoader;
    private readonly DocumentProvider _documentProvider;

    private readonly Scoped<ConcurrentDictionary<FilePath, string>> _pages = new();

    public PdfBuilder(ErrorBuilder errors, Output output, TocMap tocMap, TocLoader tocLoader, DocumentProvider documentProvider)
    {
        _errors = errors;
        _output = output;
        _tocMap = tocMap;
        _tocLoader = tocLoader;
        _documentProvider = documentProvider;
    }

    public void AddPage(FilePath file, string html)
    {
        Watcher.Write(() => _pages.Value.TryAdd(file, html));
    }

    public void Build()
    {
        using var scope = Progress.Start($"Building PDF");

        ParallelUtility.ForEach(scope, _errors, _tocMap.GetFiles(), toc => BuildCore(scope, toc));

        void BuildCore(LogScope scope, FilePath toc)
        {
            var (node, _, _, _) = _tocLoader.Load(toc);

            var outputPath = Path.ChangeExtension(_documentProvider.GetOutputPath(toc), ".pdf.html");
            using var writer = new StreamWriter(_output.WriteStream(outputPath));
            WriteTocNode(writer, node);

            void WriteTocNode(TextWriter writer, TocNode node)
            {
                if (node.Document is null || !_pages.Value.TryGetValue(node.Document, out var page))
                {
                    return;
                }

                writer.Write(page);
                foreach (var item in node.Items)
                {
                    WriteTocNode(writer, item);
                }
            }
        }
    }
}
