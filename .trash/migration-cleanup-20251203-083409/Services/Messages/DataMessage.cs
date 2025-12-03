using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WileyWidget.Services.Messages
{
    /// <summary>
    /// Strongly-typed message wrapper for data payloads.
    /// Consumers can register for `DataMessage{T}` to receive values of type T.
    /// </summary>
    public sealed class DataMessage<T> : ValueChangedMessage<T>
    {
        public DataMessage(T value) : base(value) { }
    }
}
