﻿using DocumentFormat.OpenXml.Packaging;
using MashupConverter.AbstractService;
using MashupConverter.ServiceTiming;

namespace MashupConverter
{
    public class Activity
    {
        public readonly ActivityTiming Timing;
        public readonly SlideServiceMap ServiceMap;
        private readonly SlidePart _slidePart;

        public Activity(SlidePart slidePart)
        {
            _slidePart = slidePart;
            Timing = new ActivityTiming(slidePart);
            ServiceMap = new SlideServiceMap(slidePart);
        }
    }
}
