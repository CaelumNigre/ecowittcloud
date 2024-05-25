using Ecowitt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cmdline
{
    internal abstract class OutputChannel
    {
        public string ChannelName { get; private set; }
        public uint ChannelStartDate { get; private set; }
        public uint ChannelEndDate { get; private set; }
        public uint Count { get; private set; }
        public uint LastTimeStamp { get; protected set; }
        public uint FirstTimeStamp { get; protected set; }
        protected OutputChannelBehaviorConfiguration configuration;

        public virtual ChannelTypes ChannelType { get; private set; } = ChannelTypes.Invalid;

        public OutputChannel(OutputChannelMetadata sourceMetadata, OutputChannelBehaviorConfiguration config)
        {
            if (!sourceMetadata.Validate()) throw new ArgumentException("Invalid output channel configuration");
            ChannelName = sourceMetadata.ChannelName;
            ChannelStartDate = 0;
            ChannelEndDate = 0;
            Count = 0;
            configuration = config;
        }

    }
}
