using DocumentFormat.OpenXml.Packaging;

namespace MashupConverter
{
    public class Activity
    {
        public readonly ActivityBehavior Behavior;
        public readonly SlideServiceMap ServiceMap;
        private readonly SlidePart _slidePart;

        public Activity(SlidePart slidePart)
        {
            _slidePart = slidePart;
            Behavior = new ActivityBehavior(slidePart);
            ServiceMap = new SlideServiceMap(slidePart);
        }
    }
}
