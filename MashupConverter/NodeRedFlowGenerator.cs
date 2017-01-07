﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Presentation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MashupConverter
{
    public class NodeRedFlowGenerator : IDisposable
    {
        private List<Activity> _activities;
        private JsonWriter _writer;

        public NodeRedFlowGenerator(JsonWriter writer)
        {
            _writer = writer;
        }

        public void Add(Activity activity)
        {
            _activities.Add(activity);
        }

        public void Generate()
        {
            // Start a JSON array in the writer.
            _writer.WriteStartArray();

            // Following is how we expect a non-blocked flow to be executed.
            //
            // 1. Accept a HTTP request.
            // 2. Go through the switch node for activities.
            // 3. For selected activity, go through the corresponding flow.
            // 4. Return a HTTP response.
            //
            // However, since the representation for a Node-RED node contains the identifier of the next node,
            // it is better to generate the node and the flow first which are executed later.

            // Place HTTP response node last.
            var nidHttpRes = generateHttpResNode();
            // For each activity, generate its flow.
            var nidsActivity = _activities.Select(a => new ActivityNRFlowGenerator(a, _writer).generate(nidHttpRes));
            // Place switch node for activity index then.
            var nidSwitchActivity = generateSwitchActivityFlow(nidsActivity);
            // Place HTTP input node first.
            generateHttpInNode(nidSwitchActivity);

            // End a JSON array in the writer.
            _writer.WriteEndArray();
        }

        private string generateHttpResNode()
        {
            var node = new NRNode(type: "http response");
            node.WriteTo(_writer);
            return node.Id;
        }

        private string generateSwitchActivityFlow(IEnumerable<string> nidsActivity)
        {
            var switchNode = new NRSwitchNode(NRSwitchNode.PropertyType.MSG, property: "body.activityIdx", checkall: false);
            var i = 0u;
            foreach (var nid in nidsActivity)
            {
                switchNode.Wire(nid, i);
                ++i;
            }
            switchNode.WriteTo(_writer);
            return switchNode.Id;
        }

        private void generateHttpInNode(string nidSwitchActivity)
        {
            var node = new NRHttpInNode(url: "/activities/:activityIdx/nbfs/:nbfIdx/execute",
                method: NRHttpInNode.HttpMethod.POST);
            node.Wire(nidSwitchActivity);
            node.WriteTo(_writer);
        }

        public void Dispose()
        {
        }
    }

    class ActivityNRFlowGenerator
    {
        private readonly Activity _activity;
        private readonly JsonWriter _writer;

        public ActivityNRFlowGenerator(Activity activity, JsonWriter writer)
        {
            _activity = activity;
            _writer = writer;
        }

        public string generate(string nidHttpRes)
        {
            // TODO: generate a switch node for non-blocked flows in the activity here.
            var returnNode = new NRFunctionNode();
            var switchNode = new NRSwitchNode(NRSwitchNode.PropertyType.MSG, property: "body.nbfIdx", checkall: false);
            var i = 0u;
            var timing = _activity.Timing;
            foreach (var stepTiming in timing.StepTimings)
            {
                var nidStep = generateStepFlow(stepTiming, returnNode.Id);
                switchNode.Wire(nidStep, i);
                ++i;
            }
            returnNode.Wire(nidHttpRes);
            returnNode.WriteTo(_writer);
            switchNode.WriteTo(_writer);
            return switchNode.Id;
        }

        private string generateStepFlow(StepTiming timing, string nidReturn)
        {
            var firstNode = new NRFunctionNode();
            var prev = firstNode;
            foreach (var parTiming in timing.ParallelTimings)
            {
                var next = new NRJoinFunctionNode();
                // prev will be written in this invocation.
                generateParallelFlow(parTiming, prev, next);
                prev = next;
            }
            // For understandability.
            var last = prev;
            last.Wire(nidReturn);
            last.WriteTo(_writer);
            return firstNode.Id;
        }

        private void generateParallelFlow(ParallelTiming timing, NRFunctionNode prev, NRJoinFunctionNode next)
        {
            var svcmap = _activity.ServiceMap;
            foreach (var item in timing.ShapeIds.Select((sid, i) => new {service = svcmap.Lookup(sid), i}))
            {
                var service = item.service;
                if (null == service)
                {
                    // No available service for the shape.
                    continue;
                }

                JObject flowObj;
                using (var flowReader = service.NodeRedFlow.Reader)
                {
                    flowObj = (JObject) JToken.ReadFrom(flowReader);
                }
                var meta = (JObject) flowObj["meta"];
                var nidIn = (string) meta["in"];
                var nidOut = (string) meta["out"];

                foreach (var node in flowObj["flow"])
                {
                    var id = (string) node["id"];
                    if (nidIn.Equals(id))
                    {
                        prev.Wire(id, (uint) item.i);
                    }
                    if (nidOut.Equals(id))
                    {
                        NRNode.Wire((JObject) node, next.Id);
                    }
                    node.WriteTo(_writer);
                }
            }
            prev.WriteTo(_writer);
        }
    }

    public class NRNode : JObject {
        private static readonly Random _random = new Random();

        public string Id;
        private JArray _wires;

        public NRNode(string type)
        {
            this["id"] = Id = GenerateId();
            this["type"] = type;
            this["wires"] = _wires = new JArray();
        }

        public static string GenerateId()
        {
            // This one is similar to the random ID generation from Node-RED, but not the same.
            return _random.Next().ToString("x8") + '.' + (_random.Next(0xffffff) + 1).ToString("x6");
        }

        public static void Wire(JObject node, string nid, uint outputIdx = 0)
        {
            JToken _wires;
            if (!node.TryGetValue("wires", out _wires))
            {
                node["wires"] = _wires = new JArray();
            }
            var wires = (JArray) _wires;

            if (wires.Count <= outputIdx)
            {
                var i = outputIdx - wires.Count;
                do
                {
                    wires.Add(new JArray());
                } while (i-- > 0);
            }
            Debug.Assert(wires[outputIdx] != null);
            ((JArray) wires[outputIdx]).Add(nid);
        }

        public void Wire(string nid, uint outputIdx=0)
        {
            Wire(this, nid, outputIdx);
        }

        public void Wire(NRNode node, uint outputIdx=0)
        {
            Wire(node.Id, outputIdx);
        }
    }

    public class NRSwitchRule : JObject
    {
        public enum OperatorType
        {
            // TODO: fully support the operators available in switch nodes
            EQ
        }

        private static readonly string[] _tValues = {"eq"};

        public NRSwitchRule(OperatorType t)
        {
            this["t"] = _tValues[(uint) t];
        }
    }

    public class NRSwitchNode : NRNode
    {
        public enum PropertyType
        {
            MSG,
            FLOW,
            GLOBAL
        }

        private static readonly string[] _ptypeValues = {"msg", "flow", "global"};
        private JArray _rules;

        public NRSwitchNode(PropertyType ptype, string property = null, bool checkall = true) : base("switch")
        {
            this["propertyType"] = _ptypeValues[(uint) ptype];
            this["property"] = property;
            this["checkall"] = checkall;
            this["outputs"] = 0;
            this["rules"] = _rules = new JArray();
        }

        public void AddRule(NRSwitchRule rule)
        {
            _rules.Add(rule);
            this["outputs"] = _rules.Count;
        }

        public bool RemoveRule(NRSwitchRule rule)
        {
            var ret = _rules.Remove(rule);
            this["outputs"] = _rules.Count;
            return ret;
        }

        public void RemoveRuleAt(int index)
        {
            _rules.RemoveAt(index);
            this["outputs"] = _rules.Count;
        }
    }

    public class NRFunctionNode : NRNode
    {
        public NRFunctionNode(string func = "return msg;", int outputs = 1) : base("function")
        {
            this["func"] = func;
            this["outputs"] = outputs;
        }
    }

    public class NRJoinFunctionNode : NRFunctionNode
    {
        private readonly List<string> _topics;

        public NRJoinFunctionNode(IReadOnlyCollection<string> topics = null)
        {
            _topics = null == topics ? new List<string>() : new List<string>(topics);
        }

        public void AddTopic(string topic)
        {
            _topics.Add(topic);
        }

        public void UpdateFunc()
        {
            var sb = new StringBuilder();
            sb.Append(@"let p = context.get('p') || undefined;
                    if (undefined !== p) {
                    return;
                    }
                    p = {};
                    let ps = [];
                    ");
            foreach (var topic in _topics)
            {
                sb.AppendFormat("ps.push(p['{0}'] = new Promise((resolve, reject) => undefined));\n", topic);
            }
            sb.Append(@"let pAll = Promise.all(ps);
                    pAll.then((...msgs) => node.send(msgs[0]));
                    context.set('p', p);
                    ");
            this["func"] = sb.ToString();
        }

        public override void WriteTo(JsonWriter writer, params JsonConverter[] converters)
        {
            UpdateFunc();
            base.WriteTo(writer, converters);
        }
    }

    public class NRHttpInNode : NRNode
    {
        public enum HttpMethod {GET, POST, PUT, DELETE, PATCH}

        private static readonly string[] _methodValues = {"get", "post", "put", "delete", "patch"};

        public NRHttpInNode(string url, HttpMethod method, string swaggerDoc = "") : base("http in")
        {
            this["url"] = url;
            this["method"] = _methodValues[(uint) method];
            this["swaggerDoc"] = swaggerDoc;
        }
    }
}
