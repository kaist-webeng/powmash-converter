using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MashupConverter
{
    public class NodeRedFlowGenerator : IDisposable
    {
        private readonly List<Activity> _activities = new List<Activity>();
        private readonly JsonWriter _writer;

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
            // 2. Increment the execution counter which holds the current activity index and activity step index.
            // 3. Go through the switch node for activities.
            // 4. For selected activity, go through the corresponding flow.
            // 5. Return a HTTP response.
            //
            // However, since the representation for a Node-RED node contains the identifier of the next node,
            // it is better to generate the node and the flow first which are executed later.

            // Place HTTP response node last.
            var nidHttpRes = generateHttpResNode();
            // Place a function node which sets the message to the execution result.
            var nidHttpResFunc = generateHttpResFuncNode(nidHttpRes, null, null, false);
            // For each activity, generate its flow.
            var nidsActivity = _activities
                .Select(a => new ActivityNRFlowGenerator(a, _writer)
                .generate(nidHttpResFunc));
            // Place switch node for activity index then.
            var nidSwitchActivity = generateSwitchActivityFlow(nidsActivity, nidHttpRes);
            // Place a function node which increments the execution counter.
            var nidExecCounter = generateExecCounterNode(nidSwitchActivity);
            // Place HTTP input node first.
            generateHttpInNode(nidExecCounter);

            // End a JSON array in the writer.
            _writer.WriteEndArray();
        }

        private string generateHttpResNode()
        {
            var node = new NRNode(type: "http response");
            node.WriteTo(_writer);
            return node.Id;
        }

        private string generateHttpResFuncNode(string nidHttpRes, int? currActivity, int? currStep, bool taskFinished)
        {
            var _currActivity = currActivity?.ToString() ?? @"global.get('currActivity')";
            var _currStep = currStep?.ToString() ?? @"global.get('currStep')";
            string func = $@"
msg.payload = {{
    status: '{JsonConvert.SerializeObject(HttpResponseBody.StatusType.Success)}',
    curr_activity: {_currActivity},
    curr_step: {_currStep},
    task_finished: {taskFinished}
}};
msg.headers = msg.headers || {{}};
msg.headers['Content-Type'] = 'application/json';
return msg;
";
            var node = new NRFunctionNode(func);
            node.Wire(nidHttpRes);
            node.WriteTo(_writer);
            return node.Id;
        }

        private string generateSwitchActivityFlow(IEnumerable<string> nidsActivity, string nidHttpRes)
        {
            var switchNode = new NRSwitchNode(NRSwitchNode.PropertyType.GLOBAL, property: "currActivity",
                checkall: false);
            var i = 0;
            NRSwitchRule rule;
            foreach (var nid in nidsActivity)
            {
                rule = new NRSwitchRule(NRSwitchRule.OperatorType.EQ, (i + 1).ToString(),
                    NRSwitchRule.ValueType.Num);
                switchNode.AddRule(rule);
                switchNode.Wire(nid, i);
                ++i;
            }

            var nidEndOfTask = generateHttpResFuncNode(nidHttpRes, i, 1, true);
            rule = new NRSwitchRule(NRSwitchRule.OperatorType.EQ, (i + 1).ToString(), NRSwitchRule.ValueType.Num);
            switchNode.AddRule(rule);
            switchNode.Wire(nidEndOfTask, i);
            switchNode.WriteTo(_writer);
            return switchNode.Id;
        }

        private string generateExecCounterNode(string nidSwitchActivity)
        {
            var sb = new StringBuilder();
            var nSteps =
                from a in _activities
                select a.Timing.StepTimings.Count();
            sb.AppendFormat("const step = [{0}];\n", string.Join(", ", nSteps));
            sb.Append(@"
let currActivity = global.get('currActivity') || 1;
let currStep = global.get('currStep') || 0;  // for the first execution.
// Increment the counter.
++currStep;
// Check if the task is over.
if (currActivity === 1 + step.length) {
    return msg;
}
if (step[currActivity - 1] < currStep) {
    ++currActivity;
    currStep = 1;
}
global.set('currActivity', currActivity);
global.set('currStep', currStep);
return msg;
");
            var node = new NRFunctionNode(sb.ToString());
            node.Wire(nidSwitchActivity);
            node.WriteTo(_writer);
            return node.Id;
        }

        private void generateHttpInNode(string nidExecCounter)
        {
            var node = new NRHttpInNode(url: "/execute_next",
                method: NRHttpInNode.HttpMethod.POST);
            node.Wire(nidExecCounter);
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

        public string generate(string nidHttpResFunc)
        {
            var returnNode = new NRFunctionNode();
            var switchNode = new NRSwitchNode(NRSwitchNode.PropertyType.GLOBAL, property: "currStep",
                checkall: false);
            var i = 0;
            var timing = _activity.Timing;
            foreach (var stepTiming in timing.StepTimings)
            {
                var nidStep = generateStepFlow(stepTiming, returnNode.Id);
                var rule = new NRSwitchRule(NRSwitchRule.OperatorType.EQ, (i + 1).ToString(),
                    NRSwitchRule.ValueType.Num);
                switchNode.AddRule(rule);
                switchNode.Wire(nidStep, i);
                ++i;
            }
            returnNode.Wire(nidHttpResFunc);
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

                var nodeTopic = new NRFunctionNode(func: $"msg.topic = '{service.nodeName}';return msg;");
                nodeTopic.Wire(next.Id);
                nodeTopic.WriteTo(_writer);

                foreach (var node in flowObj["flow"])
                {
                    var id = (string) node["id"];
                    if (nidIn.Equals(id))
                    {
                        prev.Wire(id, item.i);
                    }
                    if (nidOut.Equals(id))
                    {
                        NRNode.Wire((JObject) node, nodeTopic.Id);
                    }
                    node.WriteTo(_writer);
                }
            }
            prev.WriteTo(_writer);
        }
    }

    internal struct HttpResponseBody
    {
        public enum StatusType
        {
            Success, Error
        }

        [JsonProperty("status"), JsonConverter(typeof(StringEnumConverter), true)]
        public StatusType Status;
        [JsonProperty("curr_activity")]
        public int CurrActivity;
        [JsonProperty("curr_step")]
        public int CurrStep;
        [JsonProperty("task_finished")]
        public bool TaskFinished;

        [Obsolete]
        public NRTemplateNode TemplateNode
        {
            get
            {
                var body = JsonConvert.SerializeObject(this);
                var node = new NRTemplateNode(
                        template: body,
                        field: "payload",
                        format: NRTemplateNode.Format.Json,
                        syntax: NRTemplateNode.Syntax.Plain
                );
                return node;
            }
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

        public static void Wire(JObject node, string nid, int outputIdx = 0)
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

        public void Wire(string nid, int outputIdx=0)
        {
            Wire(this, nid, outputIdx);
        }

        public void Wire(NRNode node, int outputIdx=0)
        {
            Wire(node.Id, outputIdx);
        }
    }

    public class NRSwitchRule : JObject
    {
        // TODO: fully support the operators and the value types available in switch nodes.
        public enum OperatorType {EQ}
        public enum ValueType {Num}

        private static readonly string[] TValues = {"eq"};
        private static readonly string[] VtValues = {"num"};

        public NRSwitchRule(OperatorType t, string v, ValueType vt)
        {
            this["t"] = TValues[(uint) t];
            this["v"] = v;
            this["vt"] = VtValues[(uint) vt];
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
            sb.Append(@"
let resolve = context.get('resolve') || undefined;
if (undefined === resolve) {
    resolve = {};
    let promises = [];
");
            _topics.ForEach(topic =>
                sb.AppendFormat("promises.push(new Promise((res, rej) => {{ resolve['{0}'] = res; }}));", topic));
            sb.Append(@"
    let pAll = Promise.all(promises);
    pAll.then((...msgs) => node.send(msgs[0]));
    context.set('resolve', resolve);
}
resolve[msg.topic](msg);
return null;
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

    public class NRTemplateNode : NRNode
    {
        public enum FieldType {Msg, Flow, Global}
        // Syntax highlighting option.
        public enum Format {Mustache, Html, Json, Javascript, Css, Markdown, None}
        // Template format.
        public enum Syntax {Mustache, Plain}

        private static readonly string[] FieldTypeValues = {"msg", "flow", "global"};
        private static readonly string[] FormatValues = {"mustache", "html", "json", "javascript", "css", "markdown",
            "none"};
        private static readonly string[] SyntaxValues = {"mustache", "plain"};

        public NRTemplateNode(
            string template,
            string field,
            FieldType fieldType = FieldType.Msg,
            Format format = Format.Mustache,
            Syntax syntax = Syntax.Mustache) : base("template")
        {
            this["template"] = template;
            this["field"] = field;
            this["fieldType"] = FieldTypeValues[(uint) fieldType];
            this["format"] = FormatValues[(uint) format];
            this["syntax"] = SyntaxValues[(uint) syntax];
        }
    }
}
