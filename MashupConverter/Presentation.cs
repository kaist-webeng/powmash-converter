using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;

namespace MashupConverter
{
    public class Presentation
    {
        private readonly IEnumerable<SlidePart> _slideParts;

        public Presentation(PresentationDocument ppt)
        {
            var prezPart = ppt.PresentationPart;
            var slideIds = prezPart.Presentation.SlideIdList.ChildElements;
            _slideParts = from sid in slideIds
                select (SlidePart) prezPart.GetPartById(((SlideId) sid).RelationshipId);
        }

        public IEnumerable<Activity> Activities => from sp in _slideParts select new Activity(sp);
    }
}