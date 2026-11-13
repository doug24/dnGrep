# dnGREP.Benchmarks

Benchmarks for the non-multiline text search path (reading one line at a time), mainly to measure
memory use and GC pressure with very long lines. Always run in **Release** (x64), outside the debugger.

Test files are generated on first use in `%TEMP%\dnGREP.Benchmarks` and reused. The default file size is
64 MB; set the `DNGREP_BENCH_MB` environment variable to change it. A line length of `0` means the
whole file is a single line.

## BenchmarkDotNet micro benchmarks

```
dotnet run -c Release -p:Platform=x64 -- --filter *EolReaderBenchmarks*
dotnet run -c Release -p:Platform=x64 -- --filter *PlainTextEngineBenchmarks*
dotnet run -c Release -p:Platform=x64 -- --list flat
```

* `EolReaderBenchmarks`: the line reader alone, compared with `StreamReader.ReadLine`.
* `PlainTextEngineBenchmarks`: one file through `GrepEnginePlainText` (plain text, whole word, regex)
  for combinations of line length, line ending and match density.

The memory diagnoser is on. Compare `Allocated` and the `Gen2` column: large object heap allocations
are counted as Gen2. The job is a short run; add `--iterationCount 15 --warmupCount 3` for more precision.
Save a baseline before changing code, e.g. `--exporters json`, and compare the results folders
under `BenchmarkDotNet.Artifacts`.

## Peak memory of a whole search

BenchmarkDotNet does not report peak process memory, so this runs the real `GrepCore` search (with
encoding detection and the parallel search option) over several files in one process:

```
dotnet run -c Release -p:Platform=x64 -- peak --length 0 --files 8 --mb 64 --density Sparse
dotnet run -c Release -p:Platform=x64 -- peak --help
```

It reports peak private bytes, peak working set, peak managed heap, total allocations, GC counts and
pause time, and the memory retained after the search with the results alive and after they are released.
`--csv results.csv --label baseline` appends a row so runs before and after a change can be compared.
The exit code is 2 if the match count differs from what the generated files contain.
