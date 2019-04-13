using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;

namespace SpirV
{
	class Options
	{
		[Option('t', HelpText="Show types")]
		public bool ShowTypes { get; set; } = false;

		[Option ('n', HelpText = "Show object names if possible")]
		public bool ShowNames { get; set; } = false;

		[Value(0, MetaName="Input", HelpText ="Binary SPIR-V file to disassemble", Required = true)]
		public string InputFile { get; set; }
	}

	class Program
	{
		static int Run (Options options)
		{
			Module module;
			using (FileStream fs = File.OpenRead(options.InputFile))
			{
				module = Module.ReadFrom(fs);
			}

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

			string listing = ds.Disassemble(module, settings);
			Console.Write (listing);
			return 0;
		}

		private static int HandleError (IEnumerable<Error> errs)
		{
			return 1;
		}

		static void Main(string[] args)
		{
			CommandLine.Parser.Default.ParseArguments<Options> (args)
				.WithParsed<Options> (opts => Run (opts))
				.WithNotParsed<Options> ((errs) => HandleError (errs));
		}
	}
}
