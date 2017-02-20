using System;
using System.Linq;
using System.IO;
using CommandLine;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Packaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MashupConverter
{
    internal class Options
    {
        [Option('i', "input", Required = false, HelpText = "Input file to read.")]
        public string InputFile { get; set; }

        [Option('o', "output", Required = false, HelpText = "Output file to write.")]
        public string OutputFile { get; set; }
    }

    internal class SeekableTempStream : MemoryStream
    {
        public SeekableTempStream(Stream s)
        {
            s.CopyTo(this);
            Position = 0;
        }
    }

    public class JsonFileServiceRepo : ServiceRepo
    {
        // FIXME: persistence to service repository JSON file.
        public override bool Add(string serviceType) => AddOneTime(serviceType);

        public bool AddFrom(FileStream stream)
        {
            if (!stream.CanRead)
            {
                return false;
            }

            using (var sr = new StreamReader(stream))
            using (var reader = new JsonTextReader(sr))
            {
                var _serviceTypes = JToken.Load(reader);
                if (_serviceTypes.Type != JTokenType.Array)
                {
                    return false;
                }
                return _serviceTypes.All(st => st.Type == JTokenType.String && Add(((JValue) st).ToString()));
            }
        }
    }

    class MainClass
    {
        public static void Main(string[] args)
        {
            string pathInput = null;
            string pathOutput = null;
            var result = Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    pathInput = options.InputFile;
                    pathOutput = options.OutputFile;
                });

            using (var istream =
                null == pathInput ?
                    // If the standard input is used, make a temporary copy of this stream inside the memory.
                    // This should be done because the input stream to PresentationDocument should be seekable.
                    (Stream) new SeekableTempStream(Console.OpenStandardInput()) :
                    File.Open(pathInput, FileMode.Open))
            using (var ppt = PresentationDocument.Open(istream, false))
            using (var ostream =
                null == pathOutput ? Console.OpenStandardOutput() : File.Open(pathOutput, FileMode.Create))
            using (var sw = new StreamWriter(ostream))
            using (var writer = new JsonTextWriter(sw))
            using (var repoIndex = File.Open("services.json", FileMode.Open, FileAccess.Read))
            using (var generator = new NodeRedFlowGenerator(writer))
            {
                // Load service index from the service repository.
                SlideServiceMap.LoadRepoFrom(repoIndex);

                var prez = new Presentation(ppt);

                generator.Add(prez.Activities);
                generator.Generate();
            }
        }
    }
}