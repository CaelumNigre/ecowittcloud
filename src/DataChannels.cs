#nullable enable

namespace Ecowitt
{
	public interface IChannelMetaData
	{
        public uint ChannelStartDate { get; }
        public uint ChannelEndDate { get; }
        public uint Count { get; }
        public string ChannelName { get; }
    }


    public class DataChannel : IChannelMetaData
	{
		public uint ChannelStartDate { get; private set; }
		public uint ChannelEndDate { get; private set; }
		public uint Count { get; private set; }
		public string ChannelName { get; private set; }	

		private string _rawData;

		public DataChannel(string rawData)
		{
			_rawData = rawData;
			ChannelStartDate = 0;
			ChannelEndDate = 0;
			Count = 0;
			ChannelName = "";
		}

		public void ProcessChannel()
		{

		}
	}

	public class DataChannelMetaData : IChannelMetaData
	{
        public uint ChannelStartDate { get; private set; }
        public uint ChannelEndDate { get; private set; }
        public uint Count { get; private set; }
        public string ChannelName { get; private set; }
                
        public DataChannelMetaData(DataChannel source)
        {            
            ChannelStartDate = source.ChannelStartDate;
            ChannelEndDate = source.ChannelEndDate;
            Count = source.Count;
            ChannelName = source.ChannelName;
        }

    }

}