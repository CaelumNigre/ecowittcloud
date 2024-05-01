#nullable enable

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Ecowitt
{

	internal class EcowittInputData
	{
        private class RawChannelMeta : IChannelMetaData
        {
            public uint ChannelStartDate { get; set; } = uint.MaxValue;

            public uint ChannelEndDate { get; set; } = uint.MinValue;

            public uint Count { get; set; }

            public string ChannelName { get; set; } = string.Empty;

            public List<string> SensorNames { get; set; } = new List<string>();

            public ChannelTypes ChannelType { get; private set; } = ChannelTypes.Meta;
        };

        private string _rawData;
        private Dictionary<string, InputDataChannel> _channelsList;

		public EcowittInputData(string rawData)
		{
			_rawData = rawData;
            _channelsList = new Dictionary<string, InputDataChannel>();
		}

		public void ProcessInput()
		{
            Encoding encoding = Encoding.UTF8;
            ReadOnlySpan<byte> bytes = encoding.GetBytes(_rawData);
            Utf8JsonReader reader = new Utf8JsonReader(bytes);
            bool inDataSection = false;
            bool channelLevel = false;
            bool sensorLevel = false;
            bool sensorDataLevel = false;
            bool seriesLevel = false;
            string? currentChannel = null;
            string? currentSensor = null;
            string? sensorUnit = null;
            List<(string?, string?)>? dataseries = null;
            int CurrentLevel = 0;            
            while (reader.Read())
            {
                JsonTokenType tokenType = reader.TokenType;
                switch (tokenType)
                {
                    case JsonTokenType.StartObject:
                        {                            
                            if (inDataSection && CurrentLevel == 1)
                            {
                                channelLevel = true;
                            }
                            if (channelLevel && CurrentLevel == 2)
                            {
                                sensorLevel = true;
                                channelLevel = false;
                            }
                            if (inDataSection && CurrentLevel == 3)
                            {
                                sensorDataLevel = true;
                            }
                            CurrentLevel++;
                            break;
                        }
                    case JsonTokenType.EndObject:
                        {
                            if (inDataSection && CurrentLevel == 3)
                            {
                                channelLevel = true;
                                sensorLevel = false;
                                currentChannel = null;
                            }
                            if (inDataSection && CurrentLevel == 2) inDataSection = false;
                            if (inDataSection && CurrentLevel == 4)
                            {
                                sensorDataLevel = false;
                                currentSensor = null;
                            }
                            if (seriesLevel && CurrentLevel == 5)
                            {
                                seriesLevel = false;
#pragma warning disable CS8604 // Possible null reference argument.
                                DataSeries series = new DataSeries(sensorUnit, dataseries);
                                _channelsList[currentChannel].AddSensorData(currentSensor, series);
#pragma warning restore CS8604 // Possible null reference argument.
                                dataseries = null;
                                sensorUnit = null;
                            }
                            CurrentLevel--;
                            break;   
                        }
                    case JsonTokenType.PropertyName:
                        if (CurrentLevel == 1 && reader.ValueTextEquals("msg"))
                        {
                            if (!reader.Read()) throw new InvalidDataException("Unexpected end of data");
                            var message = reader.GetString();
                            if (message != "success") throw new InvalidDataException("Invalid result: " + message);
                        }
                        if (CurrentLevel>2 && currentChannel == null) throw new InvalidDataException("Sensor data without channel");
                        if (!inDataSection && CurrentLevel == 1 && reader.ValueTextEquals("data"))
                        {
                            inDataSection = true;
                        }
                        if (sensorDataLevel && CurrentLevel == 4 && reader.ValueTextEquals("unit"))
                        {
                            if (!reader.Read()) throw new InvalidDataException("Unexpected end of data");
                            sensorUnit = reader.GetString();                            
                        }
                        if (sensorDataLevel && CurrentLevel == 4 && reader.ValueTextEquals("list"))
                        {
                            seriesLevel = true;
                            dataseries = [];
                        }
                        if (sensorLevel && CurrentLevel == 3)
                        {                        
                            // new sensor in data
                            var s = reader.GetString();
                            if (s == null) throw new ArgumentNullException("Sensor name is null");
                            currentSensor = s;
                        }
                        if (seriesLevel && CurrentLevel == 5)
                        {
                            var s = reader.GetString();
                            if (!reader.Read()) throw new InvalidDataException("Unexpected end of data");
                            var v = reader.GetString();
                            if (dataseries == null) throw new NullReferenceException("Data series object not initalized");
                            dataseries.Add(new(s, v));
                        }
                        if (channelLevel)
                        {
                            // new channel in data
                            var s = reader.GetString();
                            if (s == null) throw new ArgumentNullException("Channel name is null");
                            var c = new InputDataChannel(s);                            
                            currentChannel = s;
                            _channelsList.Add(s,c);
                        }
                        break;
                }
            }
        }

		public List<DataChannelMetaData> GetChannels()
		{
			var result = new List<DataChannelMetaData>();            
            foreach (var channel in _channelsList)
            {
                var meta = new DataChannelMetaData(channel.Value);
                result.Add(meta);
            }
			return result;
		}

        public InputDataChannel? GetChannel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException("Channel name cannot be empty");
            if (!_channelsList.ContainsKey(name)) return null;
            return _channelsList[name];
        }

	}
}
