using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using dnGREP.Common;
using NLog;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.Util;

namespace dnGREP.Engines.Pdf2
{
    /// <summary>
    /// Plug-in for searching PDF documents
    /// </summary>
    public class GrepEnginePdf2 : GrepEngineBase, IGrepPluginEngine
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public IList<string> DefaultFileExtensions => new string[] { "pdf" };

        public bool IsSearchOnly => true;

        public bool PreviewPlainText { get; set; }

        public List<GrepSearchResult> Search(string fileName, string searchPattern, SearchType searchType,
            GrepSearchOption searchOptions, Encoding encoding, PauseCancelToken pauseCancelToken)
        {
            using var input = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
            return Search(input, fileName, searchPattern, searchType, searchOptions, encoding, pauseCancelToken);
        }

        // the stream version will get called if the file is in an archive
        public List<GrepSearchResult> Search(Stream input, string fileName, string searchPattern,
            SearchType searchType, GrepSearchOption searchOptions, Encoding encoding,
            PauseCancelToken pauseCancelToken)
        {
            SearchDelegates.DoSearch searchMethodMultiline = DoTextSearch;
            switch (searchType)
            {
                case SearchType.PlainText:
                case SearchType.XPath:
                    searchMethodMultiline = DoTextSearch;
                    break;
                case SearchType.Regex:
                    searchMethodMultiline = DoRegexSearch;
                    break;
                case SearchType.Soundex:
                    searchMethodMultiline = DoFuzzySearch;
                    break;
            }

            List<GrepSearchResult> result = SearchMultiline(input, fileName, searchPattern, searchOptions,
                searchMethodMultiline, pauseCancelToken);
            return result;
        }

        private List<GrepSearchResult> SearchMultiline(Stream input, string file, string searchPattern,
            GrepSearchOption searchOptions, SearchDelegates.DoSearch searchMethod,
            PauseCancelToken pauseCancelToken)
        {
            List<GrepSearchResult> searchResults = new();

            try
            {
                List<int> wrapIndexes = new();
                var text = ExtractText(input, wrapIndexes);

                var matches = searchMethod(-1, 0, text, searchPattern,
                    searchOptions, true, pauseCancelToken);
                if (matches.Count > 0)
                {
                    text = WrapText(text, wrapIndexes);

                    GrepSearchResult result = new(file, searchPattern, matches, Encoding.UTF8);
                    using (StringReader reader = new(text))
                    {
                        result.SearchResults = Utils.GetLinesEx(reader, result.Matches, initParams.LinesBefore, initParams.LinesAfter, true);
                    }
                    result.IsReadOnlyFileType = true;
                    if (PreviewPlainText)
                    {
                        result.FileInfo.TempFile = WriteTempFile(text, file, "PDF");
                    }
                    searchResults.Add(result);
                }
            }
            catch (OperationCanceledException)
            {
                // expected exception
                searchResults.Clear();
            }
            catch (Exception ex)
            {
                logger.Error(ex, string.Format("Failed to search inside PDF file [{0}]", file));
                searchResults.Add(new GrepSearchResult(file, searchPattern, ex.Message, false));
            }

            return searchResults;
        }

        private static string WrapText(string text, List<int> wrapIndexes)
        {
            char[] chars = text.ToCharArray();
            foreach (int index in wrapIndexes)
            {
                chars[index] = '\n';
            }
            return new string(chars);
        }

        private static string WriteTempFile(string text, string filePath, string app)
        {
            string tempFolder = Path.Combine(Utils.GetTempFolder(), $"dnGREP-{app}");
            if (!Directory.Exists(tempFolder))
                Directory.CreateDirectory(tempFolder);

            // ensure each temp file is unique, even if the file name exists elsewhere in the search tree
            string fileName = Path.GetFileNameWithoutExtension(filePath) + "_" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + ".txt";
            string tempFileName = Path.Combine(tempFolder, fileName);

            File.WriteAllText(tempFileName, text, Encoding.UTF8);
            return tempFileName;
        }

        private static string ExtractText(Stream stream, List<int> wrapIndexes)
        {
            StringBuilder sb = new();
            using (var document = PdfDocument.Open(stream))
            {
                foreach (var page in document.GetPages())
                {
                    GetText(page, sb, wrapIndexes);
                }
            }
            return sb.ToString();
        }

        private static readonly HashSet<string> ReplaceableWhitespace = new()
        {
            "\t",
            "\v",
            "\r",
            "\f"
        };

        private static void GetText(Page page, StringBuilder sb, List<int> wrapIndexes)
        {
            var previous = default(Letter);
            var hasJustAddedWhitespace = true;

            var annotations = page.ExperimentalAccess.GetAnnotations()
                .Where(a => a != null && !string.IsNullOrEmpty(a.Content))
                .OrderByDescending(a => a.Rectangle.Top).ThenBy(a => a.Rectangle.Left)
                .ToList();

            if (page.Letters.Count > 0)
            {
                var letters = DuplicateOverlappingTextProcessor.Get(page.Letters);

                letters = GroupByLines(letters);

                for (var i = 0; i < letters.Count; i++)
                {
                    var letter = letters[i];

                    if (string.IsNullOrEmpty(letter.Value))
                    {
                        continue;
                    }

                    if (ReplaceableWhitespace.Contains(letter.Value))
                    {
                        letter = new Letter(
                            " ",
                            letter.GlyphRectangle,
                            letter.StartBaseLine,
                            letter.EndBaseLine,
                            letter.Width,
                            letter.FontSize,
                            letter.Font,
                            letter.RenderingMode,
                            letter.StrokeColor,
                            letter.FillColor,
                            letter.PointSize,
                            letter.TextSequence);
                    }

                    if (letter.Value == " " && !hasJustAddedWhitespace)
                    {
                        if (previous != null && IsNewline(previous, letter, out _))
                        {
                            continue;
                        }

                        sb.Append(' ');
                        previous = letter;
                        hasJustAddedWhitespace = true;
                        continue;
                    }

                    hasJustAddedWhitespace = false;

                    if (previous != null && letter.Value != " ")
                    {
                        var nwPrevious = GetNonWhitespacePrevious(letters, i);

                        if (IsNewline(nwPrevious, letter, out var isDoubleNewline))
                        {
                            while (sb[^1] == ' ')
                            {
                                sb.Remove(sb.Length - 1, 1);
                            }

                            if (isDoubleNewline)
                            {
                                sb.Append('\n');
                                sb.Append('\n');
                            }
                            else
                            {
                                sb.Append(' ');
                                wrapIndexes.Add(sb.Length - 1);
                            }

                            AddAnnotation(sb, previous, letter, annotations);

                            hasJustAddedWhitespace = true;
                        }
                        else if (previous.Value != " ")
                        {
                            var gap = letter.StartBaseLine.X - previous.EndBaseLine.X;

                            if (WhitespaceSizeStatistics.IsProbablyWhitespace(gap, previous))
                            {
                                sb.Append(' ');
                                hasJustAddedWhitespace = true;
                            }
                        }
                    }

                    sb.Append(SeparateLigature(letter.Value));
                    previous = letter;
                }
            }

            if (annotations.Any())
            {
                foreach (var annot in annotations)
                {
                    sb.Append('\n').Append(@"────────────").Append('\n');
                    if (!string.IsNullOrEmpty(annot.Content))
                    {
                        sb.Append(annot.Content
                            .Replace("\r\n", "\n", StringComparison.Ordinal))
                            .Append('\n');
                    }
                }
                sb.Append(@"────────────").Append('\n');
            }

            sb.Append('\n').Append('\f');// form feed
        }

        private static IReadOnlyList<Letter> GroupByLines(IReadOnlyList<Letter> letters)
        {
            List<TextRow> list = new();

            foreach (var letter in letters)
            {
                bool placed = false;
                foreach (var row in list)
                {
                    if (letter.Intersects(row))
                    {
                        row.AddLetter(letter);
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                {
                    list.Insert(0, new TextRow(letter));
                }
            }

            list.Sort((x, y) => y.Baseline.CompareTo(x.Baseline));
            return list.SelectMany(tr => tr.Letters).ToList();
        }

        private static Letter? GetNonWhitespacePrevious(IReadOnlyList<Letter> letters, int index)
        {
            for (var i = index - 1; i >= 0; i--)
            {
                var letter = letters[i];
                if (!string.IsNullOrWhiteSpace(letter.Value))
                {
                    return letter;
                }
            }

            return null;
        }

        private static bool IsNewline(Letter? previous, Letter letter, out bool isDoubleNewline)
        {
            isDoubleNewline = false;

            if (previous == null)
            {
                return false;
            }

            var ptSizePrevious = (int)Math.Round(previous.PointSize);
            var ptSize = (int)Math.Round(letter.PointSize);
            var minPtSize = ptSize < ptSizePrevious ? ptSize : ptSizePrevious;

            var gap = Math.Abs(previous.StartBaseLine.Y - letter.StartBaseLine.Y);

            if (gap > minPtSize * 1.7 && previous.StartBaseLine.Y > letter.StartBaseLine.Y)
            {
                isDoubleNewline = true;
            }

            return gap > minPtSize * 0.9;
        }

        private static void AddAnnotation(StringBuilder sb, Letter previous, Letter letter, List<Annotation> annotations)
        {
            var annotation = annotations.FirstOrDefault();

            if (annotation != null)
            {
                if (annotation.Rectangle.Top < previous.Location.Y &&
                    annotation.Rectangle.Top >= letter.Location.Y)
                {
                    sb.Append(@"────────────").Append('\n');
                    sb.Append(annotation.Content
                        .Replace("\r\n", "\n", StringComparison.Ordinal))
                        .Append('\n');
                    sb.Append(@"────────────").Append('\n');

                    annotations.RemoveAt(0);

                    // are there any more at this location?
                    AddAnnotation(sb, previous, letter, annotations);
                }
            }
        }

        private static string SeparateLigature(string input)
        {
            // this method expects 1 character strings (a single letter) as input
            // Quick test: if it's not in range then just keep current character
            if (input.Length != 1 || input[0] < '\u0080')
                return input;

            char c = input[0];

            if (Ligatures.TryGetValue(c, out var ligature))
            {
                return ligature;
            }

            return input;

        }

        // https://gist.github.com/andyraddatz/e6a396fb91856174d4e3f1bf2e10951c
        private static readonly Dictionary<char, string> Ligatures = new()
        {
            { '\uFB00', "ff" },  // ﬀ  [LATIN SMALL LIGATURE FF]
            { '\uFB03', "ffi" }, // ﬃ  [LATIN SMALL LIGATURE FFI]
            { '\uFB04', "ffl" }, // ﬄ  [LATIN SMALL LIGATURE FFL]
            { '\uFB01', "fi" },  // ﬁ [LATIN SMALL LIGATURE FI]
            { '\uFB02', "fl" },  // ﬂ [LATIN SMALL LIGATURE FL]
            { '\u0132', "IJ" },  // Ĳ  [LATIN CAPITAL LIGATURE IJ]
            { '\u0133', "ij" },  // ĳ  [LATIN SMALL LIGATURE IJ]
            { '\u0152', "OE" },  // Œ  [LATIN CAPITAL LIGATURE OE]
            { '\u0276', "OE" },  // ɶ  [LATIN LETTER SMALL CAPITAL OE]
            { '\u0153', "oe" },  // œ  [LATIN SMALL LIGATURE OE]
            { '\u1D14', "oe" },  // ᴔ  [LATIN SMALL LETTER TURNED OE]
            { '\uA74F', "oo" },  // ꝏ [LATIN SMALL LETTER OO]
            { '\uFB06', "st" },  // ﬆ  [LATIN SMALL LIGATURE ST]
        };

        public bool Replace(string sourceFile, string destinationFile, string searchPattern, string replacePattern, SearchType searchType,
            GrepSearchOption searchOptions, Encoding encoding, IEnumerable<GrepMatch> replaceItems, PauseCancelToken pauseCancelToken)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public Version? FrameworkVersion => Assembly.GetAssembly(typeof(IGrepEngine))?.GetName()?.Version;

        public void Unload()
        {
            //Do nothing
        }

        public override void OpenFile(OpenFileArgs args)
        {
            Utils.OpenFile(args);
        }
    }
}
