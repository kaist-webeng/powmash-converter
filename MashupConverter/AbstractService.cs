using System;
using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Newtonsoft.Json;
using PresetGeometry = DocumentFormat.OpenXml.Drawing.PresetGeometry;
using ShapeTypeValues = DocumentFormat.OpenXml.Drawing.ShapeTypeValues;

namespace MashupConverter
{
    public class Service
    {
        public string nodeName;

        private static readonly Dictionary<ShapeTypeValues, Service> _dict;

        public Service(string nodeName)
        {
            this.nodeName = nodeName;
        }

        static Service()
        {
            _dict = new Dictionary<ShapeTypeValues, Service>
            {
                { ShapeTypeValues.Rectangle, new Service("display") },
                { ShapeTypeValues.FoldedCorner, new Service("latest-news") },
                { ShapeTypeValues.Sun, new Service("lightning") }
            };
        }

        public static bool IsAvailable(ShapeTypeValues shapeType)
        {
            return _dict.ContainsKey(shapeType);
        }

        public static Service Of(ShapeTypeValues shapeType)
        {
            return _dict[shapeType];
        }

        public ServiceNode NodeRedNode => new ServiceNode(this);
    }

    public class ServiceNode : IDisposable
    {
        private Stream _s;
        private StreamReader _sr;
        public JsonReader Reader;

        public ServiceNode(Service svc)
        {
            var filePath = svc.nodeName + ".json";
            _s = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            _sr = new StreamReader(_s);
            Reader = new JsonTextReader(_sr);
        }

        public void Dispose()
        {
            ((IDisposable) Reader).Dispose();
            _sr.Dispose();
            _s.Dispose();
        }
    }

    public class SlideServiceMap
    {
        private Dictionary<uint, Service> _dict = new Dictionary<uint, Service>();
        private readonly SlidePart _slidePart;

        public SlideServiceMap(SlidePart slidePart)
        {
            _slidePart = slidePart;
            populate();
        }

        private void populate()
        {
            var slide = _slidePart.Slide;
            foreach (var sp in slide.Descendants<Shape>())
            {
                var uid = sp.NonVisualShapeProperties.NonVisualDrawingProperties.Id;
                var shapeType = sp.ShapeProperties.GetFirstChild<PresetGeometry>().Preset;
                _dict.Add(uid, Service.Of(shapeType));
            }
        }
    }
}
