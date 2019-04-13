using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using CommandLine;

namespace SpirV
{
	internal class Options
	{
		[Option('t', HelpText="Show types")]
		public bool ShowTypes { get; set; } = false;

		[Option ('n', HelpText = "Show object names if possible")]
		public bool ShowNames { get; set; } = false;

		[Option('d', HelpText = "Debug mode")]
		public bool DebugMode { get; set; } = false;

		[Value(0, MetaName="Input", HelpText ="Binary SPIR-V file to disassemble", Required = true)]
		public string InputFile { get; set; }
	}

	public static class Program
	{
		public static void Main(string[] args)
		{
			CommandLine.Parser.Default.ParseArguments<Options>(args)
				.WithParsed(opts => Run(opts))
				.WithNotParsed((errs) => HandleError(errs));
		}

		private static int Run(Options options)
		{
			string listing = null;
			long elapsed = 0;
			Disassembler ds = new Disassembler();
			DisassemblyOptions settings = DisassemblyOptions.None;
			if (options.ShowNames)
			{
				settings |= DisassemblyOptions.ShowNames;
			}
			if (options.ShowTypes)
			{
				settings |= DisassemblyOptions.ShowTypes;
			}

			if (options.DebugMode)
			{
				byte[] bytes = File.ReadAllBytes(options.InputFile);
				using (MemoryStream stream = new MemoryStream(bytes))
				{
					// idle run just to compile code
					Module.ReadFrom(stream);

					Stopwatch stopwatch = new Stopwatch();
					for (int i = 0; i < 5; i++)
					{
						stream.Position = 0;
						stopwatch.Start();
						Module module = Module.ReadFrom(stream);
						listing = ds.Disassemble(module, settings);
						stopwatch.Stop();

						elapsed = stopwatch.ElapsedMilliseconds;
						Console.WriteLine($"{i + 1}) Time elapsed {elapsed}ms");
						stopwatch.Reset();
					}
					Console.WriteLine();
				}
			}
			else
			{
				using (FileStream stream = File.OpenRead(options.InputFile))
				{
					Module module = Module.ReadFrom(stream);
					listing = ds.Disassemble(module, settings);
				}
			}

			Console.WriteLine(listing);
			return 0;
		}

		private static int HandleError(IEnumerable<Error> errs)
		{
			return 1;
		}
	}
}
