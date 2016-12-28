using System;
using System.Collections.Generic;
using MashupConverter.ServiceTiming;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MashupConverter
{
	public class NodeRedFlowGenerator : IDisposable
	{
	    private List<ActivityTiming> _activityTimings;
	    private JsonWriter _writer;

		public NodeRedFlowGenerator(JsonWriter writer)
		{
		    _writer = writer;
		}

		public void Add(ActivityTiming timing)
		{
		    _activityTimings.Add(timing);
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
	        var nidsActivity = new List<string>();
	        foreach (var timing in _activityTimings)
	        {
	            var nid = generateActivityNode(timing, nidHttpRes);
	            nidsActivity.Add(nid);
	        }
	        // Place switch node for activity index then.
	        var nidSwitchActivity = generateSwitchActivityNode(nidsActivity);
	        // Place HTTP request node first.
	        generateHttpReqNode(nidSwitchActivity);

	        // End a JSON array in the writer.
	        _writer.WriteEndArray();
	    }

	    private string generateHttpResNode()
	    {
	        // TODO: generate a HTTP response node here.
	        var node = new NRNode(type: "http response");
	        node.WriteTo(_writer);
	        return node.Id;
	    }

	    private string generateActivityNode(ActivityTiming timing, string nidHttpRes)
	    {
	        // TODO: generate a switch node for non-blocked flows in the activity here.
	        var node = new NRNode(type: "switch");
	        node.WriteTo(_writer);
	        return node.Id;
	    }

	    private string generateSwitchActivityNode(List<string> nidsActivity)
	    {
	        // TODO: generate a switch node for activities here.
	        var node = new NRNode(type: "switch");
	        node.WriteTo(_writer);
	        return node.Id;
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

        public NRNode(string type)
        {
            this["id"] = Id = GenerateId();
            this["type"] = type;
        }

        public static string GenerateId()
        {
            // This one is similar to the random ID generation from Node-RED, but not the same.
            return _random.Next().ToString("x8") + '.' + (_random.Next(0xffffff) + 1).ToString("x6");
        }
    }
}
