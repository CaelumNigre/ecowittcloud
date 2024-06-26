﻿#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;

namespace Ecowitt
{
    public enum ChannelTypes { Invalid, Input, Blob, Table, Meta, File };

	public interface IChannelMetaData
	{
        public uint ChannelStartDate { get; }
        public uint ChannelEndDate { get; }
        public uint Count { get; }
        public string ChannelName { get; }
        public ChannelTypes ChannelType { get; }
    }

    internal class DataSeries
    {
        public readonly string Unit;
        public readonly List<(string?,string?)>? Data;
        public readonly uint StartTime = uint.MaxValue;
        public readonly uint EndTime = uint.MinValue;

        public DataSeries(string unit, List<(string?,string?)>? data)
        {
            if (data == null || !data.Any()) throw new ArgumentNullException("No data provided for data series");
            Data = data;            
            Unit = unit;            
            foreach (var item in data)
            {
                uint timestamp = 0;
                if (UInt32.TryParse(item.Item1, out timestamp))
                {
                    if (timestamp < StartTime) StartTime = timestamp;
                    if (timestamp > EndTime) EndTime = timestamp;
                }
                else throw new InvalidDataException("Failed to convert data series item timestamp. Value was: "+item.Item1);
            }
        }
    }

    internal class InputDataChannel : IChannelMetaData
	{
		public uint ChannelStartDate { get; private set; }
		public uint ChannelEndDate { get; private set; }
		public uint Count { get; private set; }
		public string ChannelName { get; private set; }	
        public Dictionary<string,DataSeries> Data { get; private set; }

        public ChannelTypes ChannelType { get; private set; } = ChannelTypes.Input;

        public InputDataChannel(string name)
		{
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException("Can't create channel with empty name");
			ChannelStartDate = uint.MaxValue;
			ChannelEndDate = uint.MinValue;
			Count = 0;
			ChannelName = name;
            Data = new Dictionary<string, DataSeries>();
		}

		public void AddSensorData(string sensorName, DataSeries data)
        {
            if (string.IsNullOrWhiteSpace(sensorName)) throw new ArgumentNullException("Can't create sensor with empty name");
            if (data == null || data.Data == null || !data.Data.Any()) throw new ArgumentNullException("Can't add empty data series");
            if (Data.ContainsKey(sensorName)) throw new ArgumentException("Duplicate sensor name: " + sensorName);
            Data.Add(sensorName, data);
            Count = Count + (uint) data.Data.Count;
            if (data.StartTime<ChannelStartDate) ChannelStartDate = data.StartTime;
            if (data.EndTime>ChannelEndDate) ChannelEndDate = data.EndTime;
        }
	}

	internal class DataChannelMetaData : IChannelMetaData
	{
        public uint ChannelStartDate { get; private set; }
        public uint ChannelEndDate { get; private set; }
        public uint Count { get; private set; }
        public string ChannelName { get; private set; }
        public ChannelTypes ChannelType { get; private set; } = ChannelTypes.Meta;

        public DataChannelMetaData(InputDataChannel source)
        {            
            ChannelStartDate = source.ChannelStartDate;
            ChannelEndDate = source.ChannelEndDate;
            Count = source.Count;
            ChannelName = source.ChannelName;
        }

        public DataChannelMetaData(string  channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName)) throw new ArgumentNullException("Channel name can't be empty");
            ChannelStartDate = 0;
            ChannelEndDate = 0;
            Count = 0;
            ChannelName = channelName;
        }

    }

}