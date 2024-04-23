﻿using cmdline;
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

        public Controler() { 
            _configuration = new Configuration();
        }

        public void RunProcessing()
        {
            List<DataChannelMetaData> configuredchannels = _configuration.GetConfiguredInputChannels();
            List<DataChannelMetaData> dataInputChannels = new List<DataChannelMetaData>();
            List<DataChannelMetaData> channelsToBeProcessed = new List<DataChannelMetaData>();
            var s = ReadJsonFromFile("historical_data.json");
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
                var outputChannel = new OutputChannel(channel.ChannelName);
                outputChannel.InitChannel();
                var channelData = inputData.GetChannel(channel.ChannelName);
                if (channelData == null) continue;
                outputChannel.AddData(channelData);
                outputChannel.SaveData();
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
