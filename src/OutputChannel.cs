using Azure.Storage.Blobs.Models;
using Ecowitt;
using System;
using System.Collections.Generic;
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
