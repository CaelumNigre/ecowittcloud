using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecowitt
{
    internal class OutputChannel : IChannelMetaData
    {
        const string METADATAFILESUFFIX = "metadata";

        public uint ChannelStartDate { get; private set; }

        public uint ChannelEndDate { get; private set; }

        public uint Count { get; private set; }

        public string ChannelName { get; private set; }

        public ChannelTypes ChannelType { get; private set; } = ChannelTypes.Blob;

        public uint LastTimeStamp { get; private set; }
        
        private Dictionary<string, string[]>? dataColumns = null;
        private string[]? timeRows = null;
        private int rowCount = -1;
        private string metaDataFileName;

        public OutputChannel(string channelName) {
            if (string.IsNullOrWhiteSpace(channelName)) throw new ArgumentNullException("Channel name can't be empty");
            ChannelStartDate = 0;
            ChannelEndDate = 0;
            Count = 0;
            ChannelName = channelName;
            metaDataFileName = string.Format("{0}_{1}", ChannelName, METADATAFILESUFFIX);
        }

        public void InitChannel()
        {
            timeRows = null;
            dataColumns = null;
            rowCount = -1;
            try
            {
                using (StreamReader sr = new StreamReader(metaDataFileName))
                {
                    var ts = sr.ReadLine();
                    uint x = 0;
                    if (!UInt32.TryParse(ts, out x)) throw new InvalidDataException();
                    LastTimeStamp = x;
                }
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is InvalidDataException)
                {
                    
                    using (StreamWriter sw = new StreamWriter(metaDataFileName,
                        new FileStreamOptions() { Access = FileAccess.Write, Mode = FileMode.Create} ))
                    {
                        LastTimeStamp = 0;
                        sw.WriteLine("0");
                        sw.Close();
                    }
                }
                else throw;
            }
        }

        public void AddData(InputDataChannel channel)
        {
            // assumptions about data series
            // 1. all sensors have the same number of data points
            // 2. all sensors have the same timestamps at the same positions in data set
            // 3. data series sorted on timestamp
            if (channel == null || channel.Data == null || !channel.Data.Any()) return;
            if (timeRows == null)
            {           
                timeRows = new string[channel.Data.Values.First().Data?.Count ?? 0];                
                dataColumns = new Dictionary<string, string[]>();                
                bool firstDataColumn = true;
                string firstColumnKey = "";
                foreach (var sensor in channel.Data)
                {
                    if (sensor.Value.Data == null || !sensor.Value.Data.Any()) continue;
                    int i = 0;
                    var columnName = string.Format("{0}_{1}",sensor.Key,sensor.Value.Unit);
                    dataColumns.Add(columnName, new string[sensor.Value.Data.Count]);
                    if (firstDataColumn)
                    {
                        firstColumnKey = sensor.Key;
                        foreach (var value in sensor.Value.Data)
                        {
                            if (string.IsNullOrWhiteSpace(value.Item1)) throw new NullReferenceException("Null timestamp in dataset");
                            uint ts = 0;
                            if (!UInt32.TryParse(value.Item1, out ts)) throw new InvalidDataException("Timestamp format error");
                            if (ts <= LastTimeStamp) continue;
                            timeRows[i] = value.Item1;
                            dataColumns[columnName][i] = value.Item2 ?? "";
                            i++;
                        }
                        rowCount = i;
                    }
                    else
                    {
                        foreach (var value in sensor.Value.Data)
                        {
                            uint ts = 0;
                            if (!UInt32.TryParse(value.Item1, out ts)) throw new InvalidDataException("Timestamp format error");
                            if (ts <= LastTimeStamp) continue;
                            if (timeRows[i] != value.Item1)
                            {
                                var msg = string.Format("Timestamp value in sensor {0} at position {1} differs from sensor {2} timestamp",
                                    dataColumns,i,firstColumnKey);
                                throw new InvalidDataException(msg);
                            }
                            dataColumns[columnName][i] = value.Item2 ?? "";
                            i++;
                        }
                    }                    
                }
                if (rowCount>0) LastTimeStamp = UInt32.Parse(timeRows[rowCount-1]);
            }
            else
            {
                throw new NotImplementedException("Adding more than one data channel to output channel not implemented yet");
            }
        }

        public void SaveData()
        {
            if (timeRows == null) throw new InvalidOperationException("No data added to channel");
            if (dataColumns == null || !dataColumns.Any()) throw new InvalidOperationException("Empty data columns");
            using (StreamWriter sw = new StreamWriter(ChannelName, 
                new FileStreamOptions() { Access = FileAccess.Write, Mode = FileMode.Append }))
            {
                StringBuilder sb = new StringBuilder();
                if (sw.BaseStream.Length == 0)
                {
                    sb.Append(@"""Timestamp""");
                    foreach (var column in dataColumns)
                    {
                        sb.Append(",\"" + column.Key + "\"");
                    }
                    sw.WriteLine(sb.ToString());
                }
                for (int i = 0; i<rowCount; i++) 
                {
                    sb.Clear();
                    sb.Append("\"" + timeRows[i] + "\"");
                    foreach (var column in dataColumns)
                    {
                        sb.Append(",\"" + column.Value[i] + "\"");
                    }
                    sw.WriteLine(sb.ToString());
                }
                sw.Flush();
            }
            using (StreamWriter sw = new StreamWriter(metaDataFileName,
                        new FileStreamOptions() { Access = FileAccess.Write, Mode = FileMode.Truncate }))
            {
                string s = LastTimeStamp.ToString();
                sw.WriteLine(s);
                sw.Close();
            }
        }
        
    }
}
