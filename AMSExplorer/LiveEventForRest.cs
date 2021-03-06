﻿//----------------------------------------------------------------------------------------------
//    Copyright 2020 Microsoft Corporation
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//---------------------------------------------------------------------------------------------


using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Microsoft.Azure.Management.Media.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Linq;

namespace AMSExplorer
{
    /// <summary>
    /// Rest call for live transcription preview
    /// https://docs.microsoft.com/en-us/azure/media-services/latest/live-transcription
    /// 
    /// </summary>
    public class AmsClientRestLiveTranscript
    {
        private const string liveEventApiUrl = "subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Media/mediaservices/{2}/liveEvents/{3}?api-version=2019-05-01-preview";

        private AMSClientV3 _amsClient;

        /// <summary>
        /// Initialize the Video Indexer library
        /// </summary>
        /// <param name="accountId"></param>
        /// <param name="location"></param>
        /// <param name="subscriptionKey"></param>
        public AmsClientRestLiveTranscript(AMSClientV3 amsClient)
        {
            _amsClient = amsClient;
        }



        public async Task<string> CreateLiveEventAsync(LiveEventForRest liveEventSettings, bool startLiveEventNow)
        {
            string URL = GenerateApiUrl(liveEventSettings.Name) + string.Format("&autoStart={0}", startLiveEventNow.ToString());

            string token = _amsClient.accessToken != null ? _amsClient.accessToken.AccessToken :
                TokenCache.DefaultShared.ReadItems()
                    .Where(t => t.ClientId == _amsClient.credentialsEntry.ADSPClientId)
                    .OrderByDescending(t => t.ExpiresOn)
                    .First().AccessToken;

            var client = _amsClient.AMSclient.HttpClient;
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var _requestContent = liveEventSettings.ToJson(); // Microsoft.Rest.Serialization.SafeJsonConvert.SerializeObject(liveEvent, serializationSettings);
            var httpContent = new StringContent(_requestContent, System.Text.Encoding.UTF8);
            httpContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json; charset=utf-8");

            HttpResponseMessage amsRequestResult = await client.PutAsync(URL, httpContent).ConfigureAwait(false);
            string responseContent = await amsRequestResult.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!amsRequestResult.IsSuccessStatusCode)
            {
                dynamic error = JsonConvert.DeserializeObject(responseContent);
                throw new Exception((string)error?.error?.message);
            }

            return responseContent;
        }


        public LiveEventForRest GetLiveEvent(string liveEventName)
        {
            return GetLiveEventAsync(liveEventName).GetAwaiter().GetResult();
        }


        public async Task<LiveEventForRest> GetLiveEventAsync(string liveEventName)
        {
            string URL = GenerateApiUrl(liveEventName);

            string token = _amsClient.accessToken != null ? _amsClient.accessToken.AccessToken :
                TokenCache.DefaultShared.ReadItems()
                    .Where(t => t.ClientId == _amsClient.credentialsEntry.ADSPClientId)
                    .OrderByDescending(t => t.ExpiresOn)
                    .First().AccessToken;

            var client = _amsClient.AMSclient.HttpClient;
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            HttpResponseMessage amsRequestResult = await client.GetAsync(URL).ConfigureAwait(false);

            string responseContent = await amsRequestResult.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!amsRequestResult.IsSuccessStatusCode)
            {
                dynamic error = JsonConvert.DeserializeObject(responseContent);
                throw new Exception((string)error?.error?.message);
            }

            return LiveEventForRest.FromJson(responseContent);
        }

        private string GenerateApiUrl(string liveEventName)
        {
            return _amsClient.environment.ArmEndpoint
                                       + string.Format(liveEventApiUrl,
                                                          _amsClient.credentialsEntry.AzureSubscriptionId,
                                                          _amsClient.credentialsEntry.ResourceGroup,
                                                          _amsClient.credentialsEntry.AccountName,
                                                          liveEventName
                                                  );
        }
    }


    public class LiveEventForRest
    {
        public static LiveEventForRest FromJson(string json) => JsonConvert.DeserializeObject<LiveEventForRest>(json, AMSExplorer.ConverterLE.Settings);

        public LiveEventForRest(string name, string location, string description, bool? vanityUrl, LiveEventEncoding encoding, LiveEventInput input, LiveEventPreview preview, IList<StreamOptionsFlag?> streamOptions, IList<TranscriptionForRest> transcriptions)
        {
            this.Name = name;
            this.Location = location;
            this.Properties = new PropertiesForRest { Description = description, VanityUrl = vanityUrl, Encoding = encoding, Input = input, Preview = preview, StreamOptions = streamOptions, Transcriptions = transcriptions };
        }

        [JsonProperty("properties")]
        public PropertiesForRest Properties { get; set; }

        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

    }

    public class PropertiesForRest
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("input")]
        public LiveEventInput Input { get; set; }

        [JsonProperty("preview")]
        public LiveEventPreview Preview { get; set; }

        [JsonProperty("encoding")]
        public LiveEventEncoding Encoding { get; set; }

        [JsonProperty("transcriptions")]
        public IList<TranscriptionForRest> Transcriptions { get; set; }

        [JsonProperty("vanityUrl")]
        public bool? VanityUrl { get; set; }

        [JsonProperty("streamOptions")]
        public IList<StreamOptionsFlag?> StreamOptions { get; set; }
    }


    public class TranscriptionForRest
    {
        [JsonProperty("language")]
        public string Language { get; set; }

        public TranscriptionForRest(string language)
        {
            this.Language = language;
        }
    }


    public static class SerializeForRest
    {
        public static string ToJson(this LiveEventForRest self) => JsonConvert.SerializeObject(self, AMSExplorer.ConverterLE.Settings);
    }

    internal static class ConverterLE
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }
}