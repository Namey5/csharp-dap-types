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
    [JsonObject, JsonConverter(typeof(ProtocolMessage.Converter))]
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

        /// <summary>
        /// Parse a json object as a known DAP message.
        /// <br/>
        /// Equivalent to calling <see cref="JsonConvert.DeserializeObject{T}(string)"/> specialized to <see cref="ProtocolMessage"/>.
        /// </summary>
        /// <returns>
        /// A subtype of <see cref="Request"/>, <see cref="Response"/> or <see cref="Event"/> depending on <see cref="MessageType"/>.
        /// </returns>
        /// <exception cref="Exception">
        /// Throws miscellaneous exception types if <paramref name="json"/> could not be parsed as a valid <see cref="ProtocolMessage"/>.
        /// </exception>
        public static ProtocolMessage Parse(string json)
        {
            return JsonConvert.DeserializeObject<ProtocolMessage>(json);
        }

        private sealed class Converter : JsonConverter<ProtocolMessage>
        {
            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, ProtocolMessage value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override ProtocolMessage ReadJson(
                JsonReader reader,
                Type objectType,
                ProtocolMessage existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                JObject message = JObject.Load(reader);
                Dap.MessageType messageType = message.Property<Dap.MessageType>("type", serializer);
                // [JsonConverterAttribute] is automatically inherited - need to use Populate() to ignore outer converter when
                // deserializing a child class otherwise we will get stuck recursively trying to deserialize the same object.
                // Technically this isn't necessary here as we do explicitly override the converter on derived message types,
                // but we will need to use this same logic on those as the generated subtypes need the default converter.
                if (objectType != typeof(ProtocolMessage))
                {
                    existingValue = (ProtocolMessage)Activator.CreateInstance(objectType);
                    if (existingValue.MessageType != messageType)
                    {
                        throw new InvalidOperationException($"invalid message type (expected: '{existingValue.MessageType}', was: '{messageType}')");
                    }
                    serializer.Populate(message.CreateReader(), existingValue);
                    return existingValue;
                }

                switch (messageType)
                {
                    case Dap.MessageType.Request:
                        return message.ToObject<Request>(serializer);
                    case Dap.MessageType.Response:
                        return message.ToObject<Response>(serializer);
                    case Dap.MessageType.Event:
                        return message.ToObject<Event>(serializer);
                    default:
                        throw new InvalidOperationException($"unknown message type: {messageType}");
                }
            }
        }
    }

    /// <summary>
    /// A client or debug adapter initiated request.
    /// </summary>
    [JsonObject, JsonConverter(typeof(Request.Converter))]
    public abstract partial class Request : ProtocolMessage
    {
        public sealed override Dap.MessageType MessageType => MessageType.Request;

        /// <summary>
        /// The command to execute.
        /// </summary>
        [JsonProperty("command")]
        public abstract Dap.Command Command { get; }

        private sealed class Converter : JsonConverter<Request>
        {
            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, Request value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override Request ReadJson(
                JsonReader reader,
                Type objectType,
                Request existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                JObject message = JObject.Load(reader);
                Dap.MessageType messageType = message.Property<Dap.MessageType>("type", serializer);
                if (messageType != Dap.MessageType.Request)
                {
                    throw new InvalidOperationException($"invalid message type (expected: '{Dap.MessageType.Request}', was: '{messageType}')");
                }

                Dap.Command command = message.Property<Dap.Command>("command", serializer);
                if (objectType != typeof(Request))
                {
                    existingValue = (Request)Activator.CreateInstance(objectType);
                    if (existingValue.Command != command)
                    {
                        throw new InvalidOperationException($"invalid request command (expected: '{existingValue.Command}', was: '{command}')");
                    }
                    serializer.Populate(message.CreateReader(), existingValue);
                    return existingValue;
                }

                return Request.Parse(command, message, serializer);
            }
        }
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
    [JsonObject, JsonConverter(typeof(Response.Converter))]
    public abstract partial class Response : ProtocolMessage
    {
        public sealed override Dap.MessageType MessageType => MessageType.Response;

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

        private sealed partial class Converter : JsonConverter<Response>
        {
            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, Response value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override Response ReadJson(
                JsonReader reader,
                Type objectType,
                Response existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                JObject message = JObject.Load(reader);
                Dap.MessageType messageType = message.Property<Dap.MessageType>("type", serializer);
                if (messageType != Dap.MessageType.Response)
                {
                    throw new InvalidOperationException($"invalid message type (expected: '{Dap.MessageType.Response}', was: '{messageType}')");
                }

                bool success = message.Property<bool>("success", serializer);
                if (!success)
                {
                    existingValue = new ErrorResponse();
                    serializer.Populate(message.CreateReader(), existingValue);
                    return existingValue;
                }

                Dap.Command command = message.Property<Dap.Command>("command", serializer);
                if (objectType != typeof(Response))
                {
                    existingValue = (Response)Activator.CreateInstance(objectType);
                    if (existingValue.Command != command)
                    {
                        throw new InvalidOperationException($"invalid response command (expected: '{existingValue.Command}', was: '{command}')");
                    }
                    serializer.Populate(message.CreateReader(), existingValue);
                    return existingValue;
                }

                return Response.Parse(command, message, serializer);
            }
        }
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
        public sealed override bool Success => false;
        public sealed override Dap.Command Command { get; }

        /// <summary>
        /// Default constructor - should only be used when deserializing from json.
        /// </summary>
        internal ErrorResponse() { }

        /// <summary>
        /// Default constructor with read-only fields
        /// (you probably want to use one of the other overloads).
        /// </summary>
        public ErrorResponse(Dap.Command command)
        {
            Command = command;
        }

        /// <summary>
        /// Create a new error response with a specific message.
        /// </summary>
        public ErrorResponse(Request request, string message, in Dap.Message error)
        {
            this.Command = request.Command;
            this.requestSeq = request.seq;
            this.message = message;
            this.body = new ErrorResponseBody { error = error };
        }

        /// <summary>
        /// Create a new error response by resolving <see cref="Response.message"/> from
        /// <paramref name="error"/> via <see cref="Extensions.FormatMessage(in Message)"/>.
        /// </summary>
        /// <exception cref="FormatException">
        /// <paramref name="error"/> could not be resolved to a message.
        /// </exception>
        public ErrorResponse(Request request, in Dap.Message error)
        {
            try
            {
                message = error.FormatMessage();
            }
            catch (Exception e)
            {
                throw new FormatException("failed to format error", e);
            }
            Command = request.Command;
            requestSeq = request.seq;
            body = new ErrorResponseBody { error = error };
        }
    }

    /// <summary>
    /// A debug adapter initiated event.
    /// </summary>
    [JsonObject, JsonConverter(typeof(Event.Converter))]
    public abstract partial class Event : ProtocolMessage
    {
        public sealed override Dap.MessageType MessageType => MessageType.Event;

        /// <summary>
        /// Type of event.
        /// </summary>
        [JsonProperty("event")]
        public abstract Dap.EventType EventType { get; }

        private sealed partial class Converter : JsonConverter<Event>
        {
            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, Event value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override Event ReadJson(
                JsonReader reader,
                Type objectType,
                Event existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                JObject message = JObject.Load(reader);
                Dap.MessageType messageType = message.Property<Dap.MessageType>("type", serializer);
                if (messageType != Dap.MessageType.Event)
                {
                    throw new InvalidOperationException($"invalid message type (expected: '{Dap.MessageType.Event}', was: '{messageType}')");
                }

                Dap.EventType eventType = message.Property<Dap.EventType>("event", serializer);
                if (objectType != typeof(Event))
                {
                    existingValue = (Event)Activator.CreateInstance(objectType);
                    if (existingValue.EventType != eventType)
                    {
                        throw new InvalidOperationException($"invalid event type (expected: '{existingValue.EventType}', was: '{eventType}')");
                    }
                    serializer.Populate(message.CreateReader(), existingValue);
                    return existingValue;
                }

                return Event.Parse(eventType, message, serializer);
            }
        }
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

    public static class Extensions
    {
        /// <summary>
        /// Retrieve the value of the json property named '<paramref name="propertyName"/>' converted to type <typeparamref name="T"/>.
        /// <br/>
        /// This is similar to <see cref="JToken.Value{T}(object)"/> but will
        /// fully deserialize using applicable converters instead of just trying a primitive cast.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="self"/> or <paramref name="propertyName"/> is null.
        /// </exception>
        /// <exception cref="MissingFieldException">
        /// <paramref name="self"/> did not contain a property named '<paramref name="propertyName"/>'.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// Property could not be converted to type <typeparamref name="T"/>.
        /// </exception>
        public static T Property<T>(this JObject self, string propertyName, JsonSerializer serializer = null)
        {
            try
            {
                return (T)(
                    (self ?? throw new ArgumentNullException(nameof(self)))
                        [propertyName]?
                        .ToObject(typeof(T), serializer ?? JsonSerializer.CreateDefault())
                        ?? throw new MissingFieldException(propertyName));
            }
            catch (ArgumentNullException) { throw; }
            catch (MissingFieldException) { throw; }
            catch (Exception e)
            {
                throw new InvalidCastException($"failed to cast property '{propertyName}' to type '{typeof(T)}'", e);
            }
        }

        /// <summary>
        /// Resolve this <see cref="Message"/> using its <see cref="Message.format"/> and <see cref="Message.variables"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// <see cref="Message.format"/> or <see cref="Message.variables"/> was null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <see cref="Message.variables"/> was not convertible to <see cref="JObject"/>.
        /// </exception>
        /// <exception cref="MissingFieldException">
        /// <see cref="Message.format"/> contained a specifier not present in <see cref="Message.variables"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// <see cref="Message.format"/> was not a valid format string.
        /// </exception>
        /// <exception cref="AggregateException">
        /// An unhandled exception occurred during formatting.
        /// </exception>
        public static string FormatMessage(this in Message self)
        {
            string format = self.format ?? throw new ArgumentNullException($"{nameof(self)}.{nameof(self.format)}");
            JObject variables = (self.variables ?? throw new ArgumentNullException($"{nameof(self)}.{nameof(self.variables)}")) as JObject;
            if (variables == null)
            {
                try
                {
                    variables = JObject.FromObject(self.variables);
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        $"{self.variables.GetType()} is not convertable to Newtonsoft.Json.JObject",
                        $"{nameof(self)}.{nameof(self.variables)}",
                        e);
                }
            }

            try
            {
                System.Text.StringBuilder message = new System.Text.StringBuilder(format.Length);
                int? specifierStart = null;
                for (var i = 0; i < format.Length; i++)
                {
                    if (specifierStart.HasValue)
                    {
                        switch (format[i])
                        {
                            case '{':
                                throw new FormatException($"invalid '{{' inside format specifier at character {i}");

                            case '}':
                            {
                                int start = specifierStart.Value + 1;
                                int length = i - start;
                                if (length <= 0)
                                {
                                    throw new FormatException($"empty format specifier at character {specifierStart}");
                                }

                                string specifier = format.Substring(start, length);
                                message.Append(
                                    variables[specifier]?
                                        .ToString()
                                        ?? throw new MissingFieldException(specifier));
                                specifierStart = null;
                            }
                            break;

                            default:
                                break;
                        }
                    }
                    else
                    {
                        switch (format[i])
                        {
                            case '{':
                            {
                                if (++i < format.Length && format[i] == '{')
                                {
                                    goto default;
                                }
                                specifierStart = --i;
                            }
                            break;

                            case '}':
                            {
                                if (++i < format.Length && format[i] == '}')
                                {
                                    goto default;
                                }
                            }
                            throw new FormatException($"invalid '}}' outside format specifier at character {--i}");

                            default:
                            {
                                message.Append(format[i]);
                            }
                            break;
                        }
                    }
                }
                if (specifierStart.HasValue)
                {
                    throw new FormatException($"incomplete format specifier at character {specifierStart}");
                }
                return message.ToString();
            }
            catch (MissingFieldException) { throw; }
            catch (FormatException) { throw; }
            catch (Exception e)
            {
                throw new AggregateException(e);
            }
        }
    }
}
