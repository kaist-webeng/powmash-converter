using System;
using System.Linq;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using System.Text;

namespace MashupConverter
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			String filename = @"/Users/nyangkun/workspace/juventino/sandbox.pptx";
			using (PresentationDocument ppt = PresentationDocument.Open(filename, false))
			{
				PresentationPart part = ppt.PresentationPart;
				OpenXmlElementList slideIds = part.Presentation.SlideIdList.ChildElements;
				string relId = (slideIds[0] as SlideId).RelationshipId;

				SlidePart slide = (SlidePart)part.GetPartById(relId);
				ServiceTiming st = new ServiceTiming(slide);
				StringBuilder sb = new StringBuilder();
				Console.WriteLine(st.timing);
			}
		}
	}

}
