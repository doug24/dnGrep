﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using dnGREP.Common;
using dnGREP.Common.UI;
using dnGREP.Localization;
using Resources = dnGREP.Localization.Properties.Resources;

namespace dnGREP.WPF
{
    public partial class CommandLineArgs
    {
        private const string appName = "dnGREP.exe";

        public CommandLineArgs(string commandLine)
        {
            // Getting the arguments from Environment.GetCommandLineArgs() or StartupEventArgs 
            // does strange things with quoted strings, so parse them here:
            string[] args = SplitCommandLine(commandLine);
            Count = args.Length;
            if (args.Length > 0)
            {
                EvaluateArgs(args);
            }
        }

        public int Count { get; private set; }

        private static string[] SplitCommandLine(string line)
        {
            List<string> result = [];
            foreach (string arg in ParseLine(line))
            {
                string token = arg.Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    result.Add(token);
                }
            }
            return result.Skip(1).ToArray(); // skip the app and return array of all strings
        }

        internal static IEnumerable<string> ParseLine(string input)
        {
            int startPosition = 0;
            bool isInQuotes = false;
            char prevChar = '\0';
            for (int currentPosition = 0; currentPosition < input.Length; currentPosition++)
            {
                // checking prevChar for quote allows this pattern:
                // -folder ""c:\folder 1";"c:\folder 2""

                if (input[currentPosition] == '\"' && prevChar != '\"')
                {
                    isInQuotes = !isInQuotes;
                }
                else if (input[currentPosition] == ' ' && !isInQuotes)
                {
                    yield return input[startPosition..currentPosition];
                    startPosition = currentPosition + 1;
                }

                prevChar = input[currentPosition];
            }

            string lastToken = input[startPosition..];
            if (!string.IsNullOrWhiteSpace(lastToken))
            {
                yield return lastToken;
            }
            else
            {
                yield break;
            }
        }

        private static readonly List<char> separators = [',', ';'];

        private static string FormatPathArgs(string input)
        {
            if (input.IndexOfAny([.. separators]) > -1)
            {
                List<string> parts = [];
                string[] split = input.Split([.. separators]);

                foreach (string part in split)
                {
                    string token = part.Replace("\"\"", "\"", StringComparison.Ordinal);
                    if (token.StartsWith('\"') && !token.EndsWith('\"'))
                    {
                        token = token.TrimStart('\"');
                    }
                    if (token.EndsWith('\"') && !token.StartsWith('\"'))
                    {
                        token = token.TrimEnd('\"');
                    }
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        parts.Add(token);
                    }
                }

                return string.Join(";", parts);
            }
            else
            {
                string path = StripQuotes(input);
                path = UiUtils.QuoteIfNeeded(path);
                return path;
            }
        }

        internal static string StripQuotes(string input)
        {
            if (input.Length > 2 && input.StartsWith('"') && input.EndsWith('"'))
            {
                input = input[1..^1];
            }
            return input;
        }

        private static readonly HashSet<string> pathFlags = [ "/f", "-f", "-folder" ];

        private void EvaluateArgs(string[] args)
        {
            TextInfo ti = CultureInfo.InvariantCulture.TextInfo;

            for (int idx = 0; idx < args.Length; idx++)
            {
                string arg = args[idx];

                if (!string.IsNullOrEmpty(arg))
                {
                    // old style command line args
                    if (idx < 2 && !(arg.StartsWith('/') || arg.StartsWith('-')))
                    {
                        if (idx == 0)
                        {
                            SearchPath = FormatPathArgs(arg);
                        }
                        else if (idx == 1)
                        {
                            SearchFor = StripQuotes(arg);
                            TypeOfSearch = SearchType.Regex;
                            ExecuteSearch = true;
                        }
                    }
                    else
                    {
                        string value = string.Empty;
                        if (idx + 1 < args.Length && !string.IsNullOrEmpty(args[idx + 1]))
                        {
                            value = args[idx + 1];

                            if (pathFlags.Contains(arg.ToLowerInvariant()))
                            {
                                value = FormatPathArgs(value);
                            }
                            else
                            {
                                value = StripQuotes(value);
                            }
                        }

                        switch (arg.ToLowerInvariant())
                        {
                            case "/warmup":
                                WarmUp = true;
                                break;

                            case "/sc":
                            case "-sc":
                            case "-script":
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    Script = value;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }

                                break;

                            case "/f":
                            case "-f":
                            case "-folder":
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    SearchPath = value;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }

                                break;

                            case "/pm":
                            case "-pm":
                            case "-pathtomatch":
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    NamePatternToInclude = value;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }

                                break;

                            case "/pi":
                            case "-pi":
                            case "-pathtoignore":
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    NamePatternToExclude = value;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }

                                break;

                            case "/pt":
                            case "-pt":
                            case "-patterntype":
                                if (!string.IsNullOrWhiteSpace(value) &&
                                    Enum.TryParse(value, out FileSearchType tofs) &&
                                    Enum.IsDefined(tofs))
                                {
                                    TypeOfFileSearch = tofs;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;


                            case "/s":
                            case "-s":
                            case "-searchfor":
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    SearchFor = value;
                                    ExecuteSearch = true;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }

                                break;

                            case "/st":
                            case "-st":
                            case "-searchtype":
                                if (!string.IsNullOrWhiteSpace(value) &&
                                    Enum.TryParse(value, out SearchType tos) &&
                                    Enum.IsDefined(tos))
                                {
                                    TypeOfSearch = tos;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/cs":
                            case "-cs":
                            case "-casesensitive":
                                if (!string.IsNullOrWhiteSpace(value) && bool.TryParse(ti.ToTitleCase(value), out bool caseSensitive))
                                {
                                    CaseSensitive = caseSensitive;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/ww":
                            case "-ww":
                            case "-wholeword":
                                if (!string.IsNullOrWhiteSpace(value) && bool.TryParse(ti.ToTitleCase(value), out bool wholeWord))
                                {
                                    WholeWord = wholeWord;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/ml":
                            case "-ml":
                            case "-multiline":
                                if (!string.IsNullOrWhiteSpace(value) && bool.TryParse(ti.ToTitleCase(value), out bool multiline))
                                {
                                    Multiline = multiline;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/dn":
                            case "-dn":
                            case "-dotasnewline":
                                if (!string.IsNullOrWhiteSpace(value) && bool.TryParse(ti.ToTitleCase(value), out bool dotAsNewline))
                                {
                                    DotAsNewline = dotAsNewline;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/bo":
                            case "-bo":
                            case "-booleanoperators":
                                if (!string.IsNullOrWhiteSpace(value) && bool.TryParse(ti.ToTitleCase(value), out bool booleanOperators))
                                {
                                    BooleanOperators = booleanOperators;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/mode":
                            case "-mode":
                            case "-reportmode":
                                if (!string.IsNullOrWhiteSpace(value) &&
                                    Enum.TryParse(value, out ReportMode rm) &&
                                    Enum.IsDefined(rm))
                                {
                                    ReportMode = rm;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/fi":
                            case "-fi":
                            case "-fileinformation":
                                if (!string.IsNullOrWhiteSpace(value) && bool.TryParse(ti.ToTitleCase(value), out bool fileInformation))
                                {
                                    IncludeFileInformation = fileInformation;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/trim":
                            case "-trim":
                            case "-trimwhitespace":
                                if (!string.IsNullOrWhiteSpace(value) && bool.TryParse(ti.ToTitleCase(value), out bool trim))
                                {
                                    TrimWhitespace = trim;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/unique":
                            case "-unique":
                            case "-uniqueValues":
                                if (!string.IsNullOrWhiteSpace(value) && bool.TryParse(ti.ToTitleCase(value), out bool unique))
                                {
                                    FilterUniqueValues = unique;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/scope":
                            case "-scope":
                            case "-uniquescope":
                                if (!string.IsNullOrWhiteSpace(value) &&
                                    Enum.TryParse(value, out UniqueScope scope) &&
                                    Enum.IsDefined(scope))
                                {
                                    UniqueScope = scope;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/sl":
                            case "-sl":
                            case "-separatelines":
                                if (!string.IsNullOrWhiteSpace(value) && bool.TryParse(ti.ToTitleCase(value), out bool separatelines))
                                {
                                    OutputOnSeparateLines = separatelines;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/sep":
                            case "-sep":
                            case "-listitemseparator":
                                if (!string.IsNullOrEmpty(value))
                                {
                                    ListItemSeparator = value;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }

                                break;

                            case "/rpt":
                            case "-rpt":
                            case "-report":
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    ReportPath = value;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/txt":
                            case "-txt":
                            case "-text":
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    TextPath = value;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/csv":
                            case "-csv":
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    CsvPath = value;
                                    idx++;
                                }
                                else
                                {
                                    InvalidArgument = true;
                                    ShowHelp = true;
                                }
                                break;

                            case "/x":
                            case "-x":
                            case "-exit":
                                Exit = true;
                                break;

                            case "/h":
                            case "-h":
                            case "-help":
                            case "/v":
                            case "-v":
                            case "-version":
                                ShowHelp = true;
                                break;

                            default:
                                InvalidArgument = true;
                                ShowHelp = true;
                                break;
                        }
                    }
                }
                if (ShowHelp || WarmUp)
                    break;
            }
        }

        public bool InvalidArgument { get; private set; }
        public bool WarmUp { get; private set; }
        public bool ShowHelp { get; private set; }
        public string? SearchFor { get; private set; }
        public string? SearchPath { get; private set; }
        public SearchType? TypeOfSearch { get; private set; }
        public string? NamePatternToInclude { get; private set; }
        public string? NamePatternToExclude { get; private set; }
        public FileSearchType? TypeOfFileSearch { get; private set; }
        public bool? CaseSensitive { get; private set; }
        public bool? WholeWord { get; private set; }
        public bool? Multiline { get; private set; }
        public bool? DotAsNewline { get; private set; }
        public bool? BooleanOperators { get; private set; }
        public bool ExecuteSearch { get; private set; }
        public string? Script { get; private set; }
        public string? ReportPath { get; private set; }
        public string? TextPath { get; private set; }
        public string? CsvPath { get; private set; }
        public ReportMode? ReportMode { get; private set; }
        public bool? IncludeFileInformation { get; private set; }
        public bool? TrimWhitespace { get; private set; }
        public bool? FilterUniqueValues { get; private set; }
        public UniqueScope? UniqueScope { get; private set; }
        public bool? OutputOnSeparateLines { get; private set; }
        public string? ListItemSeparator { get; private set; }

        public bool Exit { get; private set; }

        public void ApplyArgs()
        {
            if (!string.IsNullOrWhiteSpace(SearchPath))
            {
                GrepSettings.Instance.Set(GrepSettings.Key.SearchFolder, SearchPath);
            }

            if (!string.IsNullOrWhiteSpace(SearchFor))
            {
                GrepSettings.Instance.Set(GrepSettings.Key.SearchFor, SearchFor);
            }

            if (!string.IsNullOrWhiteSpace(NamePatternToInclude))
            {
                GrepSettings.Instance.Set(GrepSettings.Key.FilePattern, NamePatternToInclude);
            }

            if (!string.IsNullOrWhiteSpace(NamePatternToExclude))
            {
                GrepSettings.Instance.Set(GrepSettings.Key.FilePatternIgnore, NamePatternToExclude);
            }

            if (TypeOfSearch.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.TypeOfSearch, TypeOfSearch.Value);
            }

            if (TypeOfFileSearch.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.TypeOfFileSearch, TypeOfFileSearch.Value);
            }

            if (CaseSensitive.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.CaseSensitive, CaseSensitive.Value);
            }

            if (WholeWord.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.WholeWord, WholeWord.Value);
            }

            if (Multiline.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.Multiline, Multiline.Value);
            }

            if (DotAsNewline.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.Singleline, DotAsNewline.Value);
            }

            if (BooleanOperators.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.BooleanOperators, BooleanOperators.Value);
            }

            if (ReportMode.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.ReportMode, ReportMode);
            }

            if (IncludeFileInformation.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.IncludeFileInformation, IncludeFileInformation);
            }

            if (TrimWhitespace.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.TrimWhitespace, TrimWhitespace);
            }

            if (FilterUniqueValues.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.FilterUniqueValues, FilterUniqueValues);
            }

            if (UniqueScope.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.UniqueScope, UniqueScope);
            }

            if (OutputOnSeparateLines.HasValue)
            {
                GrepSettings.Instance.Set(GrepSettings.Key.OutputOnSeparateLines, OutputOnSeparateLines);
            }

            if (!string.IsNullOrEmpty(ListItemSeparator))
            {
                GrepSettings.Instance.Set(GrepSettings.Key.ListItemSeparator, ListItemSeparator);
            }
        }

        public static string GetHelpString()
        {
            string assemblyVersion = Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? string.Empty;
            string buildDate = AboutViewModel.AssemblyBuildDate?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;

            StringBuilder sb = new();

            sb.AppendLine(Resources.Help_CmdLineHeader).AppendLine();
            sb.AppendLine(TranslationSource.Format(Resources.Help_CmdLineVersion, assemblyVersion, buildDate)).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineUsage).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineExample1).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineExample2).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineDefaultArguments).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineArguments).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineScript).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineFolder).AppendLine();
            sb.AppendLine(Resources.Help_CmdLinePathToMatch).AppendLine();
            sb.AppendLine(Resources.Help_CmdLinePathToIgnore).AppendLine();
            sb.AppendLine(Resources.Help_CmdLinePatternType).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineSearchFor).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineSearchType).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineCaseSensitive).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineWholeWord).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineMultiline).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineDotAsNewline).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineBooleanOperators).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineReportMode).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineFileInformation).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineTrimWhitespace).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineUniqueValues).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineUniqueScope).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineSeparateLines).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineListItemSeparator).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineReport).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineTextReport).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineCSVReport).AppendLine();
            sb.AppendLine(Resources.Help_CmdLineExit);

            return sb.ToString();
        }
    }
}
