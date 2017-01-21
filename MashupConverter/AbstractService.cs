using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                { ShapeTypeValues.Sun, new Service("lightening") }
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

        public ServiceFlow ActivationFlow => new ServiceFlow(this, "activation");
        public ServiceFlow DeactivationFlow => new ServiceFlow(this, "deactivation");
    }

    public abstract class ServiceRepo
    {
        protected readonly Dictionary<string, Service> Dict = new Dictionary<string, Service>();

        public bool IsAvailable(string serviceType)
        {
            return serviceType != null && Dict.ContainsKey(serviceType);
        }

        public Service Find(string serviceType)
        {
            return IsAvailable(serviceType) ? Dict[serviceType] : null;
        }

        public abstract bool Add(string serviceType);

        public bool Add(IEnumerable<string> serviceTypes) => !serviceTypes.SkipWhile(Add).Any();

        public bool AddOneTime(string serviceType) => AddToDict(serviceType);

        protected bool AddToDict(string serviceType)
        {
            if (Dict.ContainsKey(serviceType))
            {
                return false;
            }
            Dict[serviceType] = new Service(serviceType);
            return true;
        }
    }

    public class ServiceFlow : IDisposable
    {
        private Stream _s;
        private StreamReader _sr;
        public JsonReader Reader;

        public ServiceFlow(Service svc, string msg)
        {
            var filePath = $@"{svc.nodeName}-{msg}.json";
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
        private static readonly JsonFileServiceRepo Repo = new JsonFileServiceRepo();

        private Dictionary<uint, Service> _dict = new Dictionary<uint, Service>();
        private readonly SlidePart _slidePart;

        public SlideServiceMap(SlidePart slidePart)
        {
            _slidePart = slidePart;
            populate();
        }

        public Service Lookup(uint uid)
        {
            Service s;
            _dict.TryGetValue(uid, out s);
            return s;
        }

        public static bool LoadRepoFrom(FileStream stream)
        {
            return Repo.AddFrom(stream);
        }

        private void populate()
        {
            var slide = _slidePart.Slide;
            foreach (var dp in slide.Descendants<NonVisualDrawingProperties>())
            {
                var uid = dp.Id;
                var altTextTitle = dp.Title;
                _dict.Add(uid, Repo.Find(altTextTitle));
            }
        }
    }
}
