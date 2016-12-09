using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Packaging;
namespace MashupConverter
{
	public class ServiceTiming
	{
		private SlidePart slide;

		public ServiceTiming(SlidePart slide)
		{
			this.slide = slide;
		}

		public IEnumerable<NonBlockedFlow> NonBlockedFlows
		{
			get
			{
				Timing pptTiming = slide.Slide.Timing;
				var ctnQuery =
					from ctn in pptTiming.Descendants<CommonTimeNode>()
					where ctn.NodeType != null && ctn.NodeType == TimeNodeValues.ClickEffect
					select ctn;
				foreach (var ctn in ctnQuery)
				{
					// At the checkpoint where the user should click to play the animation.
					var flow = new NonBlockedFlow(ctn);
					yield return flow;
				}
			}
		}

		public class NonBlockedFlow
		{
			private CommonTimeNode ctn;

			public NonBlockedFlow(CommonTimeNode ctn)
			{
				this.ctn = ctn;
			}

			public IEnumerable<ParallelTiming> ParallelTimings
			{
				get
				{
					var outerPar = ctn.Parent.Parent.Parent.Parent.Parent;
					var timeNodes = outerPar.Parent;
					foreach (var par in outerPar.Elements<ParallelTimeNode>())
					{
						var innerCtn = (CommonTimeNode)par.FirstChild;
						var parallel = new ParallelTiming(innerCtn);
						yield return parallel;
					}
				}
			}
		}

		public class ParallelTiming
		{
			private CommonTimeNode ctn;

			public ParallelTiming(CommonTimeNode ctn)
			{
				this.ctn = ctn;
			}

			public IEnumerable<uint> ShapeIds
			{
				get
				{
					var shapePars = ctn.ChildTimeNodeList.Elements<ParallelTimeNode>();
					foreach (ParallelTimeNode shapePar in shapePars)
					{
						var shapeSet = shapePar.CommonTimeNode.ChildTimeNodeList.Elements<SetBehavior>().First();
						var shapeId = shapeSet.CommonBehavior.TargetElement.ShapeTarget.ShapeId;
						yield return Convert.ToUInt32(shapeId);
					}
				}
			}
		}
	}
}
