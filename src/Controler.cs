using cmdline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecowitt
{
    internal class Controler
    {
        private Configuration _configuration;
        static readonly string[] data_samples = ["historical_data.json", "historical_data_2.json"];

        public Controler() { 
            _configuration = new Configuration();
        }

        public static DateTime UnixTimeStampToDateTime(uint unixTimeStamp)
        {            
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToUniversalTime();
            return dateTime;
        }

        public void RunProcessing()
        {
            List<DataChannelMetaData> configuredchannels = _configuration.GetConfiguredInputChannels();
            List<DataChannelMetaData> dataInputChannels = new List<DataChannelMetaData>();
            List<DataChannelMetaData> channelsToBeProcessed = new List<DataChannelMetaData>();
            

            for (int i = 0; i < 2; i++)
            {
                var s = ReadJsonFromFile(data_samples[i]);
                var inputData = new EcowittInputData(s);
                try
                {
                    inputData.ProcessInput();
                    dataInputChannels = inputData.GetChannels();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return;
                }
                foreach (var channel in configuredchannels)
                {
                    var inputChannel = dataInputChannels.Where(x => x.ChannelName == channel.ChannelName).FirstOrDefault();
                    if (inputChannel != null)
                    {
                        channelsToBeProcessed.Add(inputChannel);
                    }
                }
                foreach (var channel in channelsToBeProcessed)
                {
                    var outputChannel = new CSVFileOutputChannel(channel.ChannelName);
                    outputChannel.InitChannel();
                    var channelData = inputData.GetChannel(channel.ChannelName);
                    if (channelData == null) continue;
                    outputChannel.AddData(channelData);
                    outputChannel.SaveData();
                }
            }
        }

        private string ReadJsonFromFile(string fName)
        {
            string s;
            using (StreamReader reader = new StreamReader(fName, Encoding.UTF8))
            {
                s = reader.ReadToEnd();
                reader.Close();
            }
            return s;
        }
    }
}
