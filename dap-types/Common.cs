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

    /// <summary>
    /// Base class of requests, responses, and events.
    /// </summary>
    [JsonObject]
    public abstract class ProtocolMessage
    {
        /// <summary>
        /// Message type.
        /// </summary>
        [JsonProperty("type")]
        public abstract Dap.MessageType MessageType { get; }

        /// <summary>
        /// Sequence number of the message (also known as message ID).
        /// <br/>
        /// The `seq` for the first message sent by a client or debug adapter is 1,
        /// and for each subsequent message is 1 greater than the previous message sent by that actor.
        /// <br/>
        /// `seq` can be used to order requests, responses, and events,
        /// and to associate requests with their corresponding responses.
        /// <br/>
        /// For protocol messages of type `request` the sequence number can be used to cancel the request.
        /// </summary>
        public ulong seq;

        public static ProtocolMessage Parse(string json)
        {
            JObject message = JObject.Parse(json);
            Dap.MessageType messageType = message["type"]?
                .ToObject<Dap.MessageType>()
                ?? throw new MissingFieldException("type");
            switch (messageType)
            {
                case Dap.MessageType.Request:
                    return Request.Parse(message);
                case Dap.MessageType.Response:
                    return Response.Parse(message);
                case Dap.MessageType.Event:
                    return Event.Parse(message);
                default:
                    throw new ArgumentException($"unknown message type: {messageType}");
            }
        }
    }

    /// <summary>
    /// A client or debug adapter initiated request.
    /// </summary>
    public abstract partial class Request : ProtocolMessage
    {
        public override Dap.MessageType MessageType => MessageType.Request;

        /// <summary>
        /// The command to execute.
        /// </summary>
        [JsonProperty("command")]
        public abstract Dap.Command Command { get; }
    }

    /// <summary>
    /// A client or debug adapter initiated request.
    /// </summary>
    public abstract class Request<T> : Request
        where T : struct
    {
        /// <summary>
        /// Object containing arguments for the command.
        /// </summary>
        public T? arguments;
    }

    /// <summary>
    /// A client or debug adapter initiated request.
    /// </summary>
    public abstract class GenericRequest : Request
    {
        /// <summary>
        /// Object containing arguments for the command.
        /// </summary>
        public JObject arguments;
    }

    /// <summary>
    /// Response for a request.
    /// </summary>
    public abstract partial class Response : ProtocolMessage
    {
        public override Dap.MessageType MessageType => MessageType.Response;

        /// <summary>
        /// The command requested.
        /// </summary>
        [JsonProperty("command")]
        public abstract Dap.Command Command { get; }
        /// <summary>
        /// Outcome of the request.
        /// <br/>
        /// If true, the request was successful and the `body` attribute may contain the result of the request.
        /// <br/>
        /// If the value is false, the attribute `message` contains the error in short form and the `body` may
        /// contain additional information (see `ErrorResponse.body.error`).
        /// </summary>
        [JsonProperty("success")]
        public virtual bool Success => true;

        /// <summary>
        /// Sequence number of the corresponding request.
        /// </summary>
        [JsonProperty("request_seq")]
        public ulong requestSeq;
        /// <summary>
        /// Contains the raw error in short form if `success` is false.
        /// <br/>
        /// This raw error might be interpreted by the client and is not shown in the UI.
        /// Some predefined values exist.
        /// </summary>
        public string message = null;
    }

    /// <summary>
    /// Response for a request.
    /// </summary>
    public abstract class Response<T> : Response
        where T : struct
    {
        /// <summary>
        /// Contains request result if success is true and error details if success is false.
        /// </summary>
        public T? body;
    }

    /// <summary>
    /// Response for a request.
    /// </summary>
    public abstract class GenericResponse : Response
    {
        /// <summary>
        /// Contains request result if success is true and error details if success is false.
        /// </summary>
        public JObject body;
    }

    /// <summary>
    /// On error (whenever `success` is false), the body can provide more details.
    /// </summary>
    public sealed class ErrorResponse : Response<ErrorResponseBody>
    {
        public override bool Success => false;
        public override Dap.Command Command { get; }

        public ErrorResponse() { }

        public ErrorResponse(Dap.Command command, Dap.Message message)
        {
            Command = command;
            body = new ErrorResponseBody { error = message };
        }
    }

    /// <summary>
    /// A debug adapter initiated event.
    /// </summary>
    public abstract partial class Event : ProtocolMessage
    {
        public override Dap.MessageType MessageType => MessageType.Event;

        /// <summary>
        /// Type of event.
        /// </summary>
        [JsonProperty("event")]
        public abstract Dap.EventType EventType { get; }
    }

    /// <summary>
    /// A debug adapter initiated event.
    /// </summary>
    public abstract class Event<T> : Event
        where T : struct
    {
        /// <summary>
        /// Event-specific information.
        /// </summary>
        public T? body;
    }

    /// <summary>
    /// A debug adapter initiated event.
    /// </summary>
    public abstract class GenericEvent : Event
    {
        /// <summary>
        /// Event-specific information.
        /// </summary>
        public JObject body;
    }
}
