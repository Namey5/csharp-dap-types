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

        /// <summary>
        /// Parse a json object as a known DAP message.
        /// </summary>
        /// <returns>
        /// A subtype of <see cref="Request"/>, <see cref="Response"/> or <see cref="Event"/> depending on <see cref="MessageType"/>.
        /// </returns>
        /// <exception cref="JsonException">
        /// Failed to deserialize <paramref name="json"/>.
        /// </exception>
        /// <exception cref="MissingFieldException">
        /// Message was missing a required field (eg. <see cref="MessageType"/>).
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Required discriminant field (eg. <see cref="MessageType"/>) had an invalid value.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// The message could not be parsed as its reported type.
        /// </exception>
        public static ProtocolMessage Parse(string json)
        {
            JObject message;
            try
            {
                message = JObject.Parse(json);
            }
            catch (Exception e)
            {
                throw new JsonException("failed to parse json", e);
            }

            try
            {
                Dap.MessageType messageType = message.Property<Dap.MessageType>("type");
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
            catch (MissingFieldException) { throw; }
            catch (ArgumentException) { throw; }
            catch (Exception e)
            {
                throw new InvalidCastException("could not parse a valid ProtocolMessage from json", e);
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

        /// <summary>
        /// Default constructor - should only be used when deserializing from json.
        /// </summary>
        public ErrorResponse() { }

        /// <summary>
        /// Create a new error response with a specific message.
        /// </summary>
        public ErrorResponse(Dap.Command command, string message, in Dap.Message error)
        {
            this.Command = command;
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
        public ErrorResponse(Dap.Command command, in Dap.Message error)
        {
            try
            {
                message = error.FormatMessage();
            }
            catch (Exception e)
            {
                throw new FormatException("failed to format error", e);
            }
            Command = command;
            body = new ErrorResponseBody { error = error };
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

    public static class Extensions
    {
        /// <summary>
        /// Retrieve the value of the json property named '<paramref name="propertyName"/>' converted to type <typeparamref name="T"/>.
        /// <br/>
        /// This is similar to <see cref="Newtonsoft.Json.Linq.JToken.Value{T}(object)"/> but will
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
        public static T Property<T>(this JToken self, string propertyName)
        {
            try
            {
                return (T)(
                    (self ?? throw new ArgumentNullException(nameof(self)))
                        [propertyName ?? throw new ArgumentNullException(nameof(propertyName))]?
                        .ToObject(typeof(T)) ?? throw new MissingFieldException(propertyName));
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
        /// <see cref="Message.variables"/> was not convertible to <see cref="Newtonsoft.Json.Linq.JObject"/>.
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
                    throw new ArgumentException("Message.variables must be an object", e);
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
                                if ((i + 1) < format.Length && format[i + 1] == '{')
                                {
                                    i++;
                                    goto default;
                                }
                                specifierStart = i;
                            }
                            break;

                            case '}':
                            {
                                if ((i + 1) < format.Length && format[i + 1] == '}')
                                {
                                    i++;
                                    goto default;
                                }
                            }
                            throw new FormatException($"invalid '}}' outside format specifier at character {i}");

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
