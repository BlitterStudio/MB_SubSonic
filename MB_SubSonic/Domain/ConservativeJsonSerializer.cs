using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using JsonSerializer = RestSharp.Portable.Serializers.JsonSerializer;

namespace MusicBeePlugin.Domain
{
    internal class ConservativeJsonSerializer : JsonSerializer
    {
        /// <summary>
        ///     Configure the <see cref="T:RestSharp.Portable.Serializers.JsonSerializer" />
        /// </summary>
        /// <param name="serializer">The serializer to configure</param>
        protected override void ConfigureSerializer(Newtonsoft.Json.JsonSerializer serializer)
        {
            base.ConfigureSerializer(serializer);
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            serializer.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            serializer.DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ";
            serializer.ContractResolver = new CamelCasePropertyNamesContractResolver();
        }
    }
}