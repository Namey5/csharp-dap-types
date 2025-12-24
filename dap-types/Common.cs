using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Dap
{
    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum MessageType
    {
        Request,
        Response,
        Event,
    }

    [JsonObject]
    public abstract class ProtocolMessage
    {
        [JsonProperty("type")]
        public abstract MessageType MessageType { get; }

        public ulong seq;

        public static ProtocolMessage Parse(string json)
        {
            JObject message = JObject.Parse(json);
            MessageType messageType = message.Value<MessageType>("type");
            switch (messageType)
            {
                case MessageType.Request:
                    return Request.ParseMessage(message);
                case MessageType.Response:
                    return Response.ParseMessage(message);
                case MessageType.Event:
                    return Event.ParseMessage(message);
                default:
                    throw new Exception($"unknown message type: {messageType}");
            }
        }
    }

    public partial abstract class Request : ProtocolMessage
    {
        public override Dap.MessageType MessageType => MessageType.Request;

        public partial static Request ParseMessage(JObject message);
    }

    public abstract class Request<T> : Request
        where T : struct
    {
        public T? arguments;
    }

    public abstract class RequestDynamic : Request
    {
        public JObject arguments;
    }

    public partial abstract class Response : ProtocolMessage
    {
        public override Dap.MessageType MessageType => MessageType.Response;

        [JsonProperty("request_seq")]
        public ulong requestSeq;
        public bool success;
        public string? message;

        public partial static Response ParseMessage(JObject message);
    }

    public abstract class Response<T> : Response
        where T : struct
    {
        public T? body;
    }

    public abstract class ResponseDynamic : Response
    {
        public JObject body;
    }

    public partial abstract class Event : ProtocolMessage
    {
        public override Dap.MessageType MessageType => MessageType.Event;

        public partial static Event ParseMessage(JObject message);
    }

    public abstract class Event<T> : Event
        where T : struct
    {
        public T? body;
    }

    public abstract class EventDynamic : Event
    {
        public JObject body;
    }
}
