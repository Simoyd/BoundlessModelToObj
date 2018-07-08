using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BoundlessModelToObj
{
    class Program
    {
        class OptionsObj
        {
            [Option('i', "input", Required = true, HelpText = "Specifies the input MsgPack or JSON file to read.")]
            public string InputFile { get; set; }

            [Option('o', "output", Required = true, HelpText = "Specifies the output directory to write the OBJ file.")]
            public string OutputDir { get; set; }

            [Option('g', "gradient", Required = true, HelpText = "Gradient specifier to use in format [[R,G,B],[R,G,B],[R,G,B]] where R, G and B are values between 0 and 255 inclusive.")]
            public string Gradient { get; set; }

            [Option('e', "emissive", Required = true, HelpText = "Emissive color to use in format [R,G,B] where R, G and B are values between 0 and 255 inclusive.")]
            public string Emissive { get; set; }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<OptionsObj>(args)
                .WithParsed(cur =>
                {
                    ObjFileMaker.DoMakeObjFile(cur.OutputDir, cur.InputFile, cur.Gradient, cur.Emissive);
                });

            if (System.Diagnostics.Debugger.IsAttached)
            {
                using (ManualResetEvent waitForever = new ManualResetEvent(false))
                {
                    waitForever.WaitOne();
                }
            }
        }
    }
}
