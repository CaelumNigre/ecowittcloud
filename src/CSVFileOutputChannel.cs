using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Ecowitt.EcowittDevice;

namespace Ecowitt
{
    internal class OutputChannelMetadata
    {
        public required string ChannelName { get; set; } = "";
        public uint LastTimestamp { get; set; }
        public required string DeviceName { get; set; } = "";
        public required string StationType { get; set; } = "";
        public required string MAC { get; set; } = "00:00:00:00:00:00";
        public uint DeviceCreationTime { get;set; }
        public int DeviceCloudId { get; set; }
        public double DeviceLongitude { get; set; }
        public double DeviceLatitude { get; set; }
        public EcowittTemperatureUnits TemperatureUnit { get; set; } = EcowittTemperatureUnits.Celsius;
        public EcowittPressureUnits PressureUnit { get; set; } = EcowittPressureUnits.hPa;
        public EcowittWindSpeedUnits WindSpeedUnit { get; set; } = EcowittWindSpeedUnits.mps;
        public EcowittRainfallUnits RainfallUnit { get; set; } = EcowittRainfallUnits.mm;
        public EcowittSolarIrradianceUnits SolarIrradianceUnit { get; set; } = EcowittSolarIrradianceUnits.Wpm;
        public OutputChannelConfiguration OutputChannel { get; set; } = new OutputChannelConfiguration();


        public bool Validate()
        {
            if (MAC.Length!=17) return false;
            if (string.IsNullOrWhiteSpace(ChannelName)) return false;
            if (DeviceLatitude > 90.0 || DeviceLatitude < -90.0) return false;
            if (DeviceLongitude > 180.0 || DeviceLongitude < -180.0) return false;            
            return true;
        }
    }

    internal class OutputChannelBehaviorConfiguration
    {
        public bool AllowLocationChanges { get; set; } = false;
        public bool AllowStationTypeChange { get; set; } = false;
    }

    internal interface IOutputChannel
    {
        public bool InitChannel(out string message);
        public void AddData(InputDataChannel channel);
        public void SaveData();
    }

    internal class CSVFileOutputChannel : IChannelMetaData,IOutputChannel
    {
        const string METADATAFILESUFFIX = "metadata";

        public uint ChannelStartDate { get; private set; }

        public uint ChannelEndDate { get; private set; }

        public uint Count { get; private set; }

        public string ChannelName { get; private set; }

        public ChannelTypes ChannelType { get; private set; } = ChannelTypes.Blob;

        public uint LastTimeStamp { get; private set; }
        public uint FirstTimeStamp { get ; private set; }
        
        private Dictionary<string, string[]>? dataColumns = null;
        private string[]? timeRows = null;        
        private string metaDataFileName;
        private string filePath;
        private uint OriginalLastTimeStamp = 0;
        private OutputChannelMetadata metaData;
        private OutputChannelBehaviorConfiguration configuration;
        

        public CSVFileOutputChannel(string? folderPath, OutputChannelMetadata sourceMetadata, OutputChannelBehaviorConfiguration config) {
            if (!sourceMetadata.Validate()) throw new ArgumentException("Invalid output channel configuration");
            if (string.IsNullOrWhiteSpace(folderPath)) filePath = "";
                else filePath = folderPath;
            ChannelName = sourceMetadata.ChannelName;
            ChannelStartDate = 0;
            ChannelEndDate = 0;
            Count = 0;            
            metaDataFileName = string.Format("{0}_{1}", sourceMetadata.ChannelName, METADATAFILESUFFIX);
            if (filePath != "") metaDataFileName = filePath + "\\" + metaDataFileName;
            metaData = sourceMetadata;
            configuration = config;
        }

        public bool InitChannel(out string message)
        {
            timeRows = null;
            dataColumns = null;            
            message = "";
            try
            {
                string? fileContent;
                using (StreamReader sr = new StreamReader(metaDataFileName))
                {
                    fileContent = sr.ReadToEnd();                    
                }
                var existingMetaData = JsonSerializer.Deserialize<OutputChannelMetadata>(fileContent);
                if (existingMetaData == null)
                {
                    message = "Deserialization failed to produce non-null data";
                    return false;
                }
                if (existingMetaData.TemperatureUnit != metaData.TemperatureUnit)
                {
                    message = "Temperature units changed";
                    return false;
                }
                if (existingMetaData.PressureUnit != metaData.PressureUnit)
                {
                    message = "Pressure units changed";
                    return false;
                }
                if (existingMetaData.RainfallUnit != metaData.RainfallUnit)
                {
                    message = "Rainfall units changed";
                    return false;
                }
                if (existingMetaData.WindSpeedUnit!= metaData.WindSpeedUnit)
                {
                    message = "Wind speed units changed";
                    return false;
                }
                if (existingMetaData.SolarIrradianceUnit != metaData.SolarIrradianceUnit)
                {
                    message = "Solar irradiance units changed";
                    return false;
                }
                if (!configuration.AllowLocationChanges)
                {
                    if (existingMetaData.DeviceLatitude != metaData.DeviceLatitude || existingMetaData.DeviceLongitude != metaData.DeviceLongitude)
                    {
                        message = "Station location changed";
                        return false;
                    }
                }
                if (!configuration.AllowStationTypeChange)
                {
                    if (existingMetaData.StationType != metaData.StationType)
                    {
                        message = "Station type changed";
                        return false;
                    }
                }
                metaData = existingMetaData;
                OriginalLastTimeStamp = existingMetaData.LastTimestamp;
                return true;
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is InvalidDataException)
                {                    
                    using (StreamWriter sw = new StreamWriter(metaDataFileName,
                        new FileStreamOptions() { Access = FileAccess.Write, Mode = FileMode.Create} ))
                    {
                        JsonSerializerOptions options = new JsonSerializerOptions() {  WriteIndented = true };
                        var s = JsonSerializer.Serialize(metaData, options);
                        sw.WriteLine(s);    
                    }
                    return true;
                }
                else throw;
            }
        }

        private void UpdateMetadata()
        {
            metaData.LastTimestamp = LastTimeStamp;
            using (StreamWriter sw = new StreamWriter(metaDataFileName,
                new FileStreamOptions() { Access = FileAccess.Write, Mode = FileMode.Truncate }))
            {
                var s = JsonSerializer.Serialize(metaData);
                sw.WriteLine(s);
                sw.Flush();
            }
        }

        public void AddData(InputDataChannel channel)
        {
            // assumptions about data series
            // 1. all sensors have the same number of data points
            // 2. all sensors have the same timestamps at the same positions in data set
            // 3. data series sorted on timestamp
            // 4. data is added in sequence (oldest data added first)
            if (channel == null || channel.Data == null || !channel.Data.Any()) return;            
            
            var currentTimeRows = new string[channel.Data.Values.First().Data?.Count ?? 0];                
            var currentDataColumns = new Dictionary<string, string[]>();
            bool firstDataColumn = true;            
            string firstColumnKey = "";
            foreach (var sensor in channel.Data)
            {
                if (sensor.Value.Data == null || !sensor.Value.Data.Any()) continue;
                int i = 0;
                var columnName = string.Format("{0}_{1}",sensor.Key,sensor.Value.Unit);
                currentDataColumns.Add(columnName, new string[sensor.Value.Data.Count]);
                if (firstDataColumn)
                {
                    firstColumnKey = sensor.Key;
                    foreach (var value in sensor.Value.Data)
                    {
                        if (string.IsNullOrWhiteSpace(value.Item1)) throw new NullReferenceException("Null timestamp in dataset");
                        uint ts = 0;
                        if (!UInt32.TryParse(value.Item1, out ts)) throw new InvalidDataException("Timestamp format error");
                        if (ts <= LastTimeStamp) continue;
                        currentTimeRows[i] = value.Item1;
                        currentDataColumns[columnName][i] = value.Item2 ?? "";
                        i++;
                    }                    
                }
                else
                {
                    foreach (var value in sensor.Value.Data)
                    {
                        uint ts = 0;
                        if (!UInt32.TryParse(value.Item1, out ts)) throw new InvalidDataException("Timestamp format error");
                        if (ts <= LastTimeStamp) continue;
                        if (currentTimeRows[i] != value.Item1)
                        {
                            var msg = string.Format("Timestamp value in sensor {0} at position {1} differs from sensor {2} timestamp",
                                currentDataColumns,i,firstColumnKey);
                            throw new InvalidDataException(msg);
                        }
                        currentDataColumns[columnName][i] = value.Item2 ?? "";
                        i++;
                    }
                }                    
            }
            if (timeRows == null)
            {
                timeRows = currentTimeRows;
                dataColumns = currentDataColumns;
                if (currentTimeRows.Any())
                {
                    FirstTimeStamp = UInt32.Parse(currentTimeRows[0]);
                    LastTimeStamp = UInt32.Parse(currentTimeRows.Last());
                }                
            }
            else
            {
                var newLength = timeRows.Length + currentTimeRows.Length;
                var newTimeRows = new string[newLength];
                for (int i = 0; i < newLength; i++)
                {
                    if (i < timeRows.Length) newTimeRows[i] = timeRows[i];
                    else newTimeRows[i] = currentTimeRows[i - timeRows.Length];
                }
                timeRows = newTimeRows;
                if (dataColumns != null)
                {
                    var newDataColumns = new Dictionary<string, string[]>();
                    foreach (var column in dataColumns)
                    {
                        if (!currentDataColumns.ContainsKey(column.Key))
                        {
                            var msg = string.Format("Newly added values do not contain column named {0}",
                                    column.Key);
                            throw new InvalidDataException(msg);
                        }
                        newLength = column.Value.Length + currentDataColumns[column.Key].Length;
                        var newDataRows = new string[newLength];
                        for (int i = 0; i < newLength; i++)
                        {
                            if (i < column.Value.Length) newDataRows[i] = column.Value[i];
                            else newDataRows[i] = currentDataColumns[column.Key][i - column.Value.Length];
                        }
                        newDataColumns.Add(column.Key, newDataRows);
                    }
                    dataColumns = newDataColumns;
                }
                LastTimeStamp = UInt32.Parse(currentTimeRows.Last());
            }
        }

        public void SaveData()
        {
            if (timeRows == null) throw new InvalidOperationException("No data added to channel");
            if (dataColumns == null || !dataColumns.Any()) throw new InvalidOperationException("Empty data columns");
            DateTime dataStartTime = Controler.UnixTimeStampToDateTime(FirstTimeStamp);
            DateTime originalDataStartTime = Controler.UnixTimeStampToDateTime(OriginalLastTimeStamp);            
            int year = dataStartTime.Year;
            int firstRowOfNextYear = -1;
            if (year == originalDataStartTime.Year)
            {
                string fileName = string.Format("{0}_{1}", ChannelName, year);
                if (filePath != "") fileName = filePath + "\\" + fileName;
                using (StreamWriter sw = new StreamWriter(fileName,
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
                    for (int i = 0; i < timeRows.Length; i++)
                    {
                        uint currentRowTimestamp = UInt32.Parse(timeRows[i]);
                        DateTime currentRowTime = Controler.UnixTimeStampToDateTime(currentRowTimestamp);
                        if (currentRowTime.Year == year)
                        {
                            sb.Clear();
                            sb.Append("\"" + timeRows[i] + "\"");
                            foreach (var column in dataColumns)
                            {
                                sb.Append(",\"" + column.Value[i] + "\"");
                            }
                            sw.WriteLine(sb.ToString());
                        }
                        else
                        {
                            firstRowOfNextYear = i;
                            break;
                        }                            
                    }
                    sw.Flush();
                }
            }
            if (year > originalDataStartTime.Year || firstRowOfNextYear > 0)
            {
                if (firstRowOfNextYear > 0) year = year + 1;
                string fileName = string.Format("{0}_{1}", ChannelName, year);
                if (filePath != "") fileName = filePath + "\\" + fileName;
                using (StreamWriter sw = new StreamWriter(fileName,
                    new FileStreamOptions() { Access = FileAccess.Write, Mode = FileMode.CreateNew }))
                {
                    StringBuilder sb = new StringBuilder();                    
                    sb.Append(@"""Timestamp""");
                    foreach (var column in dataColumns)
                    {
                        sb.Append(",\"" + column.Key + "\"");
                    }
                    sw.WriteLine(sb.ToString());                    
                    for (int i = 0; i < timeRows.Length; i++)
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
            }
            UpdateMetadata();
        }
        
    }
}
