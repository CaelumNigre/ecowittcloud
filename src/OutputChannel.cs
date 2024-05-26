using Azure.Storage.Blobs.Models;
using Ecowitt;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecowitt
{
    internal class OutputChannelMetadata
    {
        public required string ChannelName { get; set; } = "";        
        public required string DeviceName { get; set; } = "";
        public required string StationType { get; set; } = "";
        public required string MAC { get; set; } = "00:00:00:00:00:00";
        public uint LastTimestamp { get; set; }
        public uint DeviceCreationTime { get; set; }
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
            if (MAC.Length != 17) return false;
            if (string.IsNullOrWhiteSpace(ChannelName)) return false;
            if (DeviceLatitude > 90.0 || DeviceLatitude < -90.0) return false;
            if (DeviceLongitude > 180.0 || DeviceLongitude < -180.0) return false;
            return true;
        }

        public IDictionary<string,string> ConvertToBlobMetadata()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            result.Add(nameof(ChannelName),ChannelName);
            result.Add(nameof(DeviceName), DeviceName);
            result.Add(nameof(StationType), StationType);
            result.Add(nameof(MAC), MAC);
            result.Add(nameof(LastTimestamp), LastTimestamp.ToString());
            result.Add(nameof(DeviceCreationTime), DeviceCreationTime.ToString());
            result.Add(nameof(DeviceCloudId), DeviceCloudId.ToString());
            result.Add(nameof(DeviceLongitude), DeviceLongitude.ToString());
            result.Add(nameof(DeviceLatitude), DeviceLatitude.ToString());
            result.Add(nameof(TemperatureUnit), TemperatureUnit.ToString());
            result.Add(nameof(PressureUnit), PressureUnit.ToString());
            result.Add(nameof(WindSpeedUnit), WindSpeedUnit.ToString());
            result.Add(nameof(RainfallUnit), RainfallUnit.ToString());
            result.Add(nameof(SolarIrradianceUnit), SolarIrradianceUnit.ToString());

            return result;
        }

        public static OutputChannelMetadata? ParseBlobMetadata(IDictionary<string,string> blobMeta)
        {
            if (blobMeta == null || !blobMeta.Any()) return null;
            OutputChannelMetadata result = new OutputChannelMetadata() { ChannelName = "", DeviceName = "", MAC = "", StationType = ""};
            string? dictValue = "";
            if (!blobMeta.TryGetValue(nameof(ChannelName), out dictValue)) return null;
            if (dictValue == null || string.IsNullOrEmpty(dictValue)) return null;
            result.ChannelName = dictValue;
            if (!blobMeta.TryGetValue(nameof(MAC), out dictValue)) return null;
            if (dictValue == null || string.IsNullOrEmpty(dictValue)) return null;
            result.MAC = dictValue;
            if (!blobMeta.TryGetValue(nameof(DeviceName), out dictValue)) return null;
            if (dictValue == null || string.IsNullOrEmpty(dictValue)) return null;
            result.DeviceName = dictValue;
            if (!blobMeta.TryGetValue(nameof(StationType), out dictValue)) return null;
            if (dictValue == null || string.IsNullOrEmpty(dictValue)) return null;
            result.StationType = dictValue;
            uint numericalValue = 0;
            if (!blobMeta.TryGetValue(nameof(LastTimestamp), out dictValue))
                if (!(dictValue == null || string.IsNullOrEmpty(dictValue))) UInt32.TryParse(dictValue, out numericalValue);
            result.LastTimestamp = numericalValue;
            numericalValue = 0;
            if (!blobMeta.TryGetValue(nameof(DeviceCreationTime), out dictValue))
                if (!(dictValue == null || string.IsNullOrEmpty(dictValue))) UInt32.TryParse(dictValue, out numericalValue);
            result.DeviceCreationTime = numericalValue;
            int id = 0;
            if (!blobMeta.TryGetValue(nameof(DeviceCloudId), out dictValue))
                if (!(dictValue == null || string.IsNullOrEmpty(dictValue))) Int32.TryParse(dictValue, out id);
            result.DeviceCloudId = id;
            double doubleValue = 0.0;
            if (!blobMeta.TryGetValue(nameof(DeviceLatitude), out dictValue))
                if (!(dictValue == null || string.IsNullOrEmpty(dictValue))) Double.TryParse(dictValue, out doubleValue);
            result.DeviceLatitude = doubleValue;
            doubleValue = 0.0;
            if (!blobMeta.TryGetValue(nameof(DeviceLongitude), out dictValue)) 
                if (!(dictValue == null || string.IsNullOrEmpty(dictValue))) Double.TryParse(dictValue, out doubleValue);
            result.DeviceLongitude = doubleValue;
            EcowittTemperatureUnits tUnit = EcowittTemperatureUnits.Celsius;
            if (!blobMeta.TryGetValue(nameof(TemperatureUnit), out dictValue)) 
                if (dictValue == null || string.IsNullOrEmpty(dictValue)) Enum.TryParse<EcowittTemperatureUnits>(dictValue, out tUnit);
            result.TemperatureUnit = tUnit;
            EcowittPressureUnits pUnit = EcowittPressureUnits.hPa;
            if (!blobMeta.TryGetValue(nameof(PressureUnit), out dictValue))
                if (dictValue == null || string.IsNullOrEmpty(dictValue)) Enum.TryParse<EcowittPressureUnits>(dictValue, out pUnit);
            result.PressureUnit= pUnit;
            EcowittWindSpeedUnits wUnit = EcowittWindSpeedUnits.mps;
            if (!blobMeta.TryGetValue(nameof(WindSpeedUnit), out dictValue))
                if (dictValue == null || string.IsNullOrEmpty(dictValue)) Enum.TryParse<EcowittWindSpeedUnits>(dictValue, out wUnit);
            result.PressureUnit = pUnit;
            EcowittRainfallUnits rUnit = EcowittRainfallUnits.mm;
            if (!blobMeta.TryGetValue(nameof(RainfallUnit), out dictValue))
                if (dictValue == null || string.IsNullOrEmpty(dictValue)) Enum.TryParse<EcowittRainfallUnits>(dictValue, out rUnit);
            result.PressureUnit = pUnit;
            EcowittSolarIrradianceUnits sUnit = EcowittSolarIrradianceUnits.Wpm;
            if (!blobMeta.TryGetValue(nameof(SolarIrradianceUnit), out dictValue))
                if (dictValue == null || string.IsNullOrEmpty(dictValue)) Enum.TryParse<EcowittSolarIrradianceUnits>(dictValue, out sUnit);
            result.PressureUnit = pUnit;
            return result;
        }
    }

    internal abstract class OutputChannel
    {
        public string ChannelName { get; private set; }
        public uint ChannelStartDate { get; private set; }
        public uint ChannelEndDate { get; private set; }
        public uint Count { get; private set; }
        public uint LastTimeStamp { get; protected set; }
        public uint FirstTimeStamp { get; protected set; }
        protected OutputChannelBehaviorConfiguration configuration;
        protected Dictionary<string, string[]>? dataColumns = null;
        protected string[]? timeRows = null;
        protected OutputChannelMetadata metaData;

        public virtual ChannelTypes ChannelType { get; private set; } = ChannelTypes.Invalid;

        public OutputChannel(OutputChannelMetadata sourceMetadata, OutputChannelBehaviorConfiguration config)
        {
            if (!sourceMetadata.Validate()) throw new ArgumentException("Invalid output channel configuration");
            ChannelName = sourceMetadata.ChannelName;
            ChannelStartDate = 0;
            ChannelEndDate = 0;
            Count = 0;
            configuration = config;
            metaData = sourceMetadata;
        }

        public virtual void AddData(InputDataChannel channel)
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
                var columnName = string.Format("{0}_{1}", sensor.Key, sensor.Value.Unit);
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
                                currentDataColumns, i, firstColumnKey);
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

    }
}
