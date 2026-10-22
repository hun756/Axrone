using Axrone.AssetGltf.Benchmarks;
using BenchmarkDotNet.Running;

string[] effectiveArgs = args.Length == 0 ? ["--filter", "*"] : args;
BenchmarkSwitcher
    .FromAssembly(typeof(GltfImportBenchmarks).Assembly)
    .Run(effectiveArgs);
