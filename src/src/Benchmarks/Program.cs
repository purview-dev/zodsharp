using BenchmarkDotNet.Running;

Console.WriteLine("=== ZodSharp Performance Tests ===\n");
Console.WriteLine("Running performance benchmarks...\n");

BenchmarkRunner.Run(typeof(Program).Assembly);

Console.WriteLine("\n=== Performance Test Summary ===");
Console.WriteLine("Check the 'BenchmarkDotNet.Artifacts' folder for detailed results.");
