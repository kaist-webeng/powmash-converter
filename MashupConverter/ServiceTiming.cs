using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Packaging;
namespace MashupConverter
{
    public class ActivityTiming
    {
        private readonly SlidePart _slide;

        public ActivityTiming(SlidePart slide)
        {
            _slide = slide;
        }

        public IEnumerable<StepTiming> StepTimings
        {
            get
            {
                var pptTiming = _slide.Slide.Timing;
                var ctnQuery =
                    from ctn in pptTiming.Descendants<CommonTimeNode>()
                    where ctn.NodeType != null && ctn.NodeType == TimeNodeValues.ClickEffect
                    select ctn;
                foreach (var ctn in ctnQuery)
                {
                    // At the checkpoint where the user should click to play the animation.
                    var timing = new StepTiming(ctn);
                    yield return timing;
                }
            }
        }
    }

    public class StepTiming
    {
        private readonly CommonTimeNode _ctn;

        public StepTiming(CommonTimeNode ctn)
        {
            _ctn = ctn;
        }

        public IEnumerable<ParallelTiming> ParallelTimings
        {
            get
            {
                var outerPar = _ctn.Parent.Parent.Parent.Parent.Parent;
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
        private readonly CommonTimeNode _ctn;

        public ParallelTiming(CommonTimeNode ctn)
        {
            _ctn = ctn;
        }

        public IEnumerable<uint> ShapeIds
        {
            get
            {
                var shapePars = _ctn.ChildTimeNodeList.Elements<ParallelTimeNode>();
                foreach (var shapePar in shapePars)
                {
                    var shapeSet = shapePar.CommonTimeNode.ChildTimeNodeList.Elements<SetBehavior>().First();
                    var shapeId = shapeSet.CommonBehavior.TargetElement.ShapeTarget.ShapeId;
                    yield return Convert.ToUInt32(shapeId);
                }
            }
        }
    }
}
