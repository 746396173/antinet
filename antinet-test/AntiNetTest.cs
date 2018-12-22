// antinet test code.
// This is in the public domain

using System;
using System.Diagnostics;
using System.Reflection;
using Antinet;

namespace antinet_test {
	public class AntiNetTest {
		const string sep = "\n\n\n";
		const string seperr = "********************************************************";
		public static int Main(string[] args) {
			Console.WriteLine();
			Console.WriteLine("CLR: {0} - {1}", Environment.Version, IntPtr.Size == 4 ? "x86" : "x64");
			Console.WriteLine();

			CheckAll();
			Console.WriteLine("Press any key to initialize anti-managed debugger code...");
			Console.ReadKey(true);
			Console.WriteLine(sep);

			if (AntiDebugger.PreventManagedDebugger())
				Console.WriteLine("Anti-managed debugger code has been successfully initialized");
			else {
				Console.Error.WriteLine(seperr);
				Console.Error.WriteLine("FAILED TO INITIALIZE ANTI-DEBUGGER CODE");
				Console.Error.WriteLine(seperr);
			}
			Console.WriteLine();

			CheckAll();
			Console.WriteLine("Try to attach a debugger and try setting a breakpoint in \"BreakpointTest()\".");
			Console.ReadKey(true);
			BreakpointTest();
			Console.WriteLine(sep);

			CheckAll();
			Console.WriteLine("Let's exit. Press any key (again!)...");
			Console.ReadKey(true);
			Console.WriteLine(sep);

			return 0;
		}

		private static void CheckAll() {
			Console.WriteLine("Debugger.IsAttached: {0}", Debugger.IsAttached);
			Console.WriteLine("AntiDebugger.HasUnmanagedDebugger(): {0}", AntiDebugger.HasUnmanagedDebugger());
			Console.WriteLine("AntiDebugger.HasManagedDebugger(): {0}", AntiDebugger.HasManagedDebugger());
			Console.WriteLine("AntiDebugger.HasDebugger(): {0}", AntiDebugger.HasDebugger());
			Console.WriteLine("AntiPatcher.VerifyClrPEHeader(): {0}", AntiPatcher.VerifyClrPEHeader());
		}

		[Obfuscation(Exclude = true)]
		public static void BreakpointTest() {
			Console.WriteLine("BreakpointTest1");
			Console.WriteLine("BreakpointTest2");
			Console.WriteLine("BreakpointTest3");
		}
	}
}
