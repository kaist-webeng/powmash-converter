using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Packaging;
namespace MashupConverter
{
    public class ActivityBehavior
    {
        private readonly SlidePart _slide;

        public ActivityBehavior(SlidePart slide)
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

        public Transition Transition => new Transition(_slide.Slide.Transition);
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

    public class Transition
    {
        private readonly DocumentFormat.OpenXml.Presentation.Transition _trans;

        public Transition(DocumentFormat.OpenXml.Presentation.Transition trans)
        {
            _trans = trans;
        }

        public bool OnClick => _trans.AdvanceOnClick;
        public bool Auto => _trans.AdvanceAfterTime.HasValue;
        public uint Delay => Auto ? Convert.ToUInt32(_trans.AdvanceAfterTime.Value) : 0;
        public TimeSpan? Duration => _trans.Duration.HasValue ? ParseUniversalTimeOffset(_trans.Duration.Value) : null;

        private static TimeSpan? ParseUniversalTimeOffset(string offset)
        {
            var _offset = Regex.Replace(offset, @"\s", string.Empty);
            var m = Regex.Match(_offset, @"(?<number>\d+(\.\d+)?)(?<unit>h|min|s|ms|µs|ns)");
            if (!m.Success)
            {
                return null;
            }
            var number = Convert.ToDouble(m.Groups["number"]);
            var unit = m.Groups["unit"].Value;
            var hours = 0;
            var minutes = 0;
            var seconds = 0;
            var milliseconds = 0;
            switch (unit)
            {
                case "h":
                    hours = (int) number;
                    number = 60 * (number - hours);
                    goto case "min";

                case "min":
                    minutes = (int) number;
                    number = 60 * (number - minutes);
                    goto case "s";

                case "s":
                    seconds = (int) number;
                    number = 1000 * (number - seconds);
                    goto case "ms";

                case "ms":
                    milliseconds = (int) number;
                    break;

                default:
                    break;
            }
            return new TimeSpan(0, hours, minutes, seconds, milliseconds);
        }
    }
}
