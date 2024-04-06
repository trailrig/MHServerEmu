﻿using Google.ProtocolBuffers;

namespace MHServerEmu.Core.Network
{
    /// <summary>
    /// Contains a deserialized <see cref="IMessage"/>.
    /// </summary>
    public readonly struct MailboxMessage
    {
        private readonly IMessage _message;

        public uint Id { get; }

        /// <summary>
        /// Constructs a new <see cref="MailboxMessage"/> from the provided <see cref="MessagePackage"/>.
        /// </summary>
        public MailboxMessage(MessagePackage message)
        {
            Id = message.Id;
            _message = message.Deserialize();
        }

        /// <summary>
        /// Returns the contents of this <see cref="MailboxMessage"/> as <typeparamref name="T"/>.
        /// </summary>
        public T As<T>() where T: class, IMessage
        {
            return _message as T;
        }
    }
}
