using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
namespace MashupConverter
{
	public class ServiceTiming
	{
		private SlidePart slide;
		public List<List<List<uint>>> timing = new List<List<List<uint>>>();

		public ServiceTiming(SlidePart slide)
		{
			this.slide = slide;
			Extract();
		}

		private void Extract()
		{
			Timing pptTiming = slide.Slide.Timing;
			var ctnQuery =
				from ctn in pptTiming.Descendants<CommonTimeNode>()
				where ctn.NodeType != null && ctn.NodeType == TimeNodeValues.ClickEffect
				select ctn;
			foreach (var ctn in ctnQuery)
			{
				// At the checkpoint where the user should click to play the animation.
				var parallels = PlayCheckpoint(ctn);
				timing.Add(parallels);
			}
		}

		private List<List<uint>> PlayCheckpoint(CommonTimeNode ctn)
		{
			var outerPar = ctn.Parent.Parent.Parent.Parent.Parent;
			var timeNodes = outerPar.Parent;
			var parallels = new List<List<uint>>();
			foreach (var par in outerPar.Elements<ParallelTimeNode>())
			{
				var innerCtn = (CommonTimeNode) par.FirstChild;
				var parallel = PlayParallel(innerCtn);
				parallels.Add(parallel);
			}
			return parallels;
		}

		private List<uint> PlayParallel(CommonTimeNode ctn)
		{
			var parallel = new List<uint>();
			var shapePars = ctn.ChildTimeNodeList.Elements<ParallelTimeNode>();
			foreach (ParallelTimeNode shapePar in shapePars)
			{
				var shapeSet = shapePar.CommonTimeNode.ChildTimeNodeList.Elements<SetBehavior>().First();
				var shapeId = shapeSet.CommonBehavior.TargetElement.ShapeTarget.ShapeId;
				parallel.Add(Convert.ToUInt32(shapeId));
			}
			return parallel;
		}
	}
}
