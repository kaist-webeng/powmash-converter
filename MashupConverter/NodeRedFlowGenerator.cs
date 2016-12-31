using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using MashupConverter.ServiceTiming;
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
            var nidsActivity = _activities.Select(a => generateActivityFlow(a.Timing, nidHttpRes));
            // Place switch node for activity index then.
            var nidSwitchActivity = generateSwitchActivityFlow(nidsActivity);
            // Place HTTP request node first.
            generateHttpReqNode(nidSwitchActivity);

            // End a JSON array in the writer.
            _writer.WriteEndArray();
        }

        private string generateHttpResNode()
        {
            var node = new NRNode(type: "http response");
            node.WriteTo(_writer);
            return node.Id;
        }

        private string generateActivityFlow(ActivityTiming timing, string nidHttpRes)
        {
            // TODO: generate a switch node for non-blocked flows in the activity here.
            var returnNode = new NRFunctionNode();
            var switchNode = new NRSwitchNode(NRSwitchNode.PropertyType.MSG, property: "body.nbfIdx", checkall: false);
            var i = 0u;
            foreach (var seqTiming in timing.SequenceTimings)
            {
                var nidSequence = generateSequenceFlow(seqTiming, returnNode.Id);
                switchNode.Wire(nidSequence, i);
                ++i;
            }
            returnNode.Wire(nidHttpRes);
            returnNode.WriteTo(_writer);
            switchNode.WriteTo(_writer);
            return switchNode.Id;
        }

        private string generateSequenceFlow(SequenceTiming timing, string nidReturn)
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
            // TODO: generate a flow which executes multiple services in parallel with the message from prev as input and writes the message to next as the output, and wire the nodes.
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

        private void generateHttpReqNode(string nidSwitchActivity)
        {
            // TODO: generate a HTTP request node here.
            var node = new NRNode(type: "http request");
            node.WriteTo(_writer);
        }

        public void Dispose()
        {
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

        public void Wire(string nid, uint outputIdx=0)
        {
            if (_wires.Count <= outputIdx)
            {
                var i = outputIdx - _wires.Count;
                do
                {
                    _wires.Add(new JArray());
                } while (i-- > 0);
            }
            Debug.Assert(_wires[outputIdx] != null);
            ((JArray) _wires[outputIdx]).Add(nid);
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
}
