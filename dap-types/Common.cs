using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Dap
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MessageType
    {
        [EnumMember(Value = "request")]
        Request,
        [EnumMember(Value = "response")]
        Response,
        [EnumMember(Value = "event")]
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

        [JsonProperty("command")]
        public abstract Dap.Command Command { get; }

        public static Request ParseMessage(JObject message)
        {
            return ParseInternal(message);
        }

        private partial static Request ParseInternal(JObject message);
    }

    public abstract class Request<T> : Request
        where T : struct
    {
        public T? arguments;
    }

    public abstract class GenericRequest : Request
    {
        public JObject arguments;
    }

    public partial abstract class Response : ProtocolMessage
    {
        public override Dap.MessageType MessageType => MessageType.Response;

        [JsonProperty("command")]
        public abstract Dap.Command Command { get; }
        [JsonProperty("success")]
        public virtual bool Success => true;

        [JsonProperty("request_seq")]
        public ulong requestSeq;
        public string? message;

        public static Response ParseMessage(JObject message)
        {
            if (!message.Value<bool>("success"))
            {
                return message.ToObject<ErrorResponse>();
            }
            return ParseInternal(message);
        }

        private partial static Response ParseInternal(JObject message);
    }

    public abstract class Response<T> : Response
        where T : struct
    {
        public T? body;
    }

    public abstract class GenericResponse : Response
    {
        public JObject body;
    }

    public sealed class ErrorResponse : Response<ErrorResponseBody>
    {
        public override bool Success => false;
        public override Dap.Command Command { get; set; }

        public ErrorResponse() { }

        public ErrorResponse(Dap.Request request, Dap.Message message)
        {
            Command = request.Command;
            body = new ErrorResponseBody { error = message };
        }
    }

    public partial abstract class Event : ProtocolMessage
    {
        public override Dap.MessageType MessageType => MessageType.Event;

        [JsonProperty("event")]
        public abstract Dap.EventType EventType { get; }

        public static Event ParseMessage(JObject message)
        {
            return ParseInternal(message);
        }

        private partial static Event ParseInternal(JObject message);
    }

    public abstract class Event<T> : Event
        where T : struct
    {
        public T? body;
    }

    public abstract class GenericEvent : Event
    {
        public JObject body;
    }
}
