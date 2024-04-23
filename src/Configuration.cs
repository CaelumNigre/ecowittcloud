using Ecowitt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cmdline
{
    internal class Configuration
    {
        public Configuration() { }

        public List<DataChannelMetaData> GetConfiguredInputChannels()
        {
            var result = new List<DataChannelMetaData>();
            DataChannelMetaData a = new DataChannelMetaData("indoor");
            result.Add(a);
            a = new DataChannelMetaData("outdoor");
            result.Add(a);
            a = new DataChannelMetaData("rainfall");
            result.Add(a);
            a = new DataChannelMetaData("wind");
            result.Add(a);
            a = new DataChannelMetaData("lightning");
            result.Add(a);
            return result;
        }


    }
}
