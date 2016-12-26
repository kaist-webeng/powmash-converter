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
			var filename = @"/Users/nyangkun/workspace/juventino/sandbox2.pptx";
			using (PresentationDocument ppt = PresentationDocument.Open(filename, false))
			{
				PresentationPart part = ppt.PresentationPart;
				OpenXmlElementList slideIds = part.Presentation.SlideIdList.ChildElements;
				var relId = (slideIds[0] as SlideId).RelationshipId;

				SlidePart slide = (SlidePart)part.GetPartById(relId);
				ServiceTiming.ActivityTiming at = new ServiceTiming.ActivityTiming(slide);
				StringBuilder sb = new StringBuilder();

				sb.Append('[');
				foreach (var seqTiming in at.SequenceTimings)
				{
					sb.Append('[');
					foreach (var parTiming in seqTiming.ParallelTimings)
					{
						sb.Append('[');
						foreach (var sid in parTiming.ShapeIds)
						{
							sb.AppendFormat("{0},", sid);
						}
						sb.Append("],");
					}
					sb.Append("],");
				}
				sb.Append(']');

				Console.WriteLine(sb);
			}
		}
	}

}
