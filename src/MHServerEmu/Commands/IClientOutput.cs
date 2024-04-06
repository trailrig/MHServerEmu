﻿using MHServerEmu.Core.Network.Tcp;

namespace MHServerEmu.Commands
{
    /// <summary>
    /// Exposes <see cref="string"/> output for an <see cref="ITcpClient"/>.
    /// </summary>
    public interface IClientOutput
    {
        /// <summary>
        /// Outputs the provided <see cref="string"/> to the specified <see cref="ITcpClient"/>.
        /// </summary>
        public void Output(string output, ITcpClient client);
    }
}
