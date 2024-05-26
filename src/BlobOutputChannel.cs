using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Ecowitt.EcowittDevice;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure;
using Azure.Storage.Blobs.Specialized;
using Azure.Identity;

namespace Ecowitt
{
    internal class BlobOutputChannel : OutputChannel, IChannelMetaData,IOutputChannel
    {
        const string METADATABLOBNAME = "metadata";
        
        private string metaDataFileName;
        private Uri SAUri;        
        private uint OriginalLastTimeStamp = 0;        
        private DefaultAzureCredential _credentials;
        private string blobBase;

        public new ChannelTypes ChannelType = ChannelTypes.Blob;


        public BlobOutputChannel(string? blobURL, DefaultAzureCredential credentials, 
            OutputChannelMetadata sourceMetadata, OutputChannelBehaviorConfiguration config) 
            : base(sourceMetadata, config)
        {
            if (string.IsNullOrWhiteSpace(blobURL)) throw new NullReferenceException("Blob URL is empty");
            if (credentials == null) throw new NullReferenceException("No Azure credentials provided");
            UriCreationOptions options = new UriCreationOptions();
            if (!Uri.TryCreate(blobURL, in options, out SAUri)) throw new ArgumentException("Invalid URL provided");
            blobBase = blobURL;
            metaData = sourceMetadata;            
            _credentials = credentials;
        }

        public bool InitChannel(out string message)
        {
            timeRows = null;
            dataColumns = null;            
            message = "";
            BlobClient client = new BlobClient(new Uri(blobBase + ChannelName + "/" + METADATABLOBNAME),_credentials);
            try
            {
                string? fileContent;                
                using (StreamReader sr = new StreamReader(client.OpenRead()))
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
                if (ex is RequestFailedException)
                {
                    using (StreamWriter sw = new StreamWriter(client.OpenWrite(true)))
                    {                        
                        JsonSerializerOptions options = new JsonSerializerOptions() { WriteIndented = true };
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
            BlobClient client = new BlobClient(new Uri(blobBase + ChannelName + "/" + METADATABLOBNAME),_credentials);
            using (StreamWriter sw = new StreamWriter(client.OpenWrite(true)))
            {
                var s = JsonSerializer.Serialize(metaData);
                sw.WriteLine(s);
                sw.Flush();
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
                using (StreamWriter sw = new StreamWriter(new MemoryStream()))
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
                    sw.BaseStream.Seek(0, SeekOrigin.Begin);
                    string blobName = string.Format("{0}", year);
                    AppendBlobClient client = new AppendBlobClient(new Uri(blobBase + ChannelName + "/" + blobName),_credentials);
                    AppendBlobAppendBlockOptions options = new AppendBlobAppendBlockOptions();
                    client.CreateIfNotExists();
                    client.AppendBlock(sw.BaseStream,options);
                }              
            }
            if (year > originalDataStartTime.Year || firstRowOfNextYear > 0)
            {
                if (firstRowOfNextYear > 0) year = year + 1;                
                using (StreamWriter sw = new StreamWriter(new MemoryStream()))
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
                    sw.BaseStream.Seek(0, SeekOrigin.Begin);    
                    string blobName = string.Format("{0}", year);
                    AppendBlobClient client = new AppendBlobClient(new Uri(blobBase + ChannelName + "/" + blobName),_credentials);
                    AppendBlobAppendBlockOptions options = new AppendBlobAppendBlockOptions();
                    client.CreateIfNotExists();
                    client.AppendBlock(sw.BaseStream, options);
                }
            }
            UpdateMetadata();
        }
        
    }
}
