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
				ServiceTiming st = new ServiceTiming(slide);
				StringBuilder sb = new StringBuilder();

				sb.Append('[');
				foreach (var flow in st.NonBlockedFlows)
				{
					sb.Append('[');
					foreach (var timing in flow.ParallelTimings)
					{
						sb.Append('[');
						foreach (var sid in timing.ShapeIds)
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
