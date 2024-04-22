#nullable enable

namespace Ecowitt
{

	public class EcowittInputData
	{
		private string _rawData;

		public EcowittInputData(string rawData)
		{
			_rawData = rawData;
		}

		public void ProcessInput()
		{

		}

		public List<DataChannelMetaData> GetChannels()
		{
			var result = new List<DataChannelMetaData>();
			return result;
		}

	}
}
