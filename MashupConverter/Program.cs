using System.Linq;
using System.IO;
using CommandLine;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Packaging;
using Newtonsoft.Json;

namespace MashupConverter
{
    internal class Options
    {
        [Value(0, Required = true, HelpText = "Input file to read.")]
        public string InputFile { get; set; }

        [Value(1, Required = true, HelpText = "Output file to write.")]
        public string OutputFile { get; set; }
    }

	class MainClass
	{
		public static void Main(string[] args)
		{
		    var pathInput = "";
		    var pathOutput = "";
		    var result = Parser.Default.ParseArguments<Options>(args)
		        .WithParsed(options =>
		        {
		            pathInput = options.InputFile;
		            pathOutput = options.OutputFile;
		        });

			using (var ppt = PresentationDocument.Open(pathInput, false))
			using (var sw = new StreamWriter(pathOutput))
            using (var writer = new JsonTextWriter(sw))
            using (var generator = new NodeRedFlowGenerator(writer))
			{
				var prezPart = ppt.PresentationPart;
				var slideIds = prezPart.Presentation.SlideIdList.ChildElements;
			    var slideParts = from sid in slideIds
			        select (SlidePart) prezPart.GetPartById(((SlideId) sid).RelationshipId);
			    foreach (var sp in slideParts)
			    {
			        var activity = new Activity(sp);
			        generator.Add(activity);
			    }
			    generator.Generate();
			}
		}
	}

}
